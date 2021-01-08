namespace Taskr
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;

    using ColoredConsole;

    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    public class Worker : BackgroundService
    {
        private const int Padding = 17;
        private const string Tab = "    ";
        private const string VerticalChar = "│";
        private const string HorizontalChar = "─";
        private const string BorderChar = "└";

        private static readonly Dictionary<string, ConsoleColor> StateColors = new Dictionary<string, ConsoleColor>
        {
            { "New", ConsoleColor.Red },
            { "Active", ConsoleColor.DarkYellow },
            { "Closed", ConsoleColor.DarkGreen },
            { "To Do", ConsoleColor.Red },
            { "In Progress", ConsoleColor.DarkYellow },
            { "Done", ConsoleColor.DarkGreen },
            { "Removed", ConsoleColor.DarkGray },
        };

        private readonly ILogger<Worker> logger;

        private AccountSettings settings = null;

        private string continuationkey = null;

        public Worker(IOptionsMonitor<AccountSettings> settingsMonitor, ILogger<Worker> logger)
        {
            this.settings = settingsMonitor.CurrentValue;
            settingsMonitor.OnChange(changedSettings => { this.settings = changedSettings; });
            this.logger = logger;
        }

        protected async override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await this.ProcessAsync(stoppingToken);
        }

        private async Task ProcessAsync(CancellationToken stopToken)
        {
            while (!stopToken.IsCancellationRequested)
            {
                this.PrintAccounts();

                // If key was typed during Continue...
                var key = this.continuationkey;
                this.continuationkey = null;

                if (string.IsNullOrWhiteSpace(key))
                {
                    ColorConsole.Write("> ".Cyan());
                    key = Console.ReadLine()?.Trim();
                }

                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (key.Equals("q", StringComparison.OrdinalIgnoreCase) || key.Equals("quit", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
                else if (key.Equals("?"))
                {
                    this.PrintAccounts();
                }
                else if (key.Equals("c", StringComparison.OrdinalIgnoreCase) || key.Equals("cls", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Clear();
#pragma warning disable S3626 // Jump statements should not be redundant
                    continue;
#pragma warning restore S3626 // Jump statements should not be redundant
                }
                else
                {
                    if (int.TryParse(key, out var index))
                    {
                        try
                        {
                            await this.Execute(index);
                        }
                        catch (Exception ex)
                        {
                            ColorConsole.WriteLine(ex.Message.Red());
                        }
                    }
                }
            }
        }

        private void PrintAccounts()
        {
            foreach (var item in this.settings.Accounts.Where(a => a.Enabled).Select((x, i) => (index: i, account: x)))
            {
                ColorConsole.WriteLine($"{item.index + 1}".Cyan(), $" {(string.IsNullOrWhiteSpace(item.account.Name) ? item.account.Org + " / " + item.account.Project : item.account.Name)}");
            }
        }

        private async IAsyncEnumerable<(Account account, List<WorkItem> workItems)> GetTasks(params Account[] accounts)
        {
            var defaultWiql = this.settings.Query ?? Program.DefaultQuery;
            foreach (var account in accounts)
            {
                ColorConsole.WriteLine($" {account.Org} / {account.Project} ".Black().OnCyan());
                await this.SetAuthTokenAsync(account);
                yield return (account, workItems: await new AzDOService().GetWorkItems(account, defaultWiql).ConfigureAwait(false));
            }
        }

        private async Task SetAuthTokenAsync(Account account)
        {
            if (string.IsNullOrWhiteSpace(account.Token))
            {
                var tenant = await account.Org.GetTenantId();
                account.Token = await AuthHelper.GetAuthTokenAsync(tenant); // SetSettings(Settings);
            }
        }

        private async Task Execute(int index)
        {
            var accounts = index > 0 && index <= this.settings.Accounts.Count() ? new[] { this.settings.Accounts[index - 1] } : this.settings.Accounts.Where(a => a.Enabled).ToArray();
            var results = this.GetTasks(accounts);
            await foreach (var items in results)
            {
                var account = items.account;
                var workItems = items.workItems;
                ColorConsole.WriteLine();
                if (workItems?.Count > 0)
                {
                    var slicers = (string.IsNullOrWhiteSpace(account.Slicers) ? this.settings.Slicers : account.Slicers).Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)?.ToList();
                    if (slicers?.Count > 0)
                    {
                        var headers = slicers.SelectMany(s => workItems.GroupBy(x => x.Fields.GetPropertyValue(s)).OrderBy(x => x.Key));
                        var padding = headers.Max(h => h.Key?.ToString()?.Replace($"{account.Project}\\", string.Empty)?.Length ?? 0) + Tab.Length;
                        foreach (var slicer in slicers)
                        {
                            this.PrintSlicer(account, workItems, slicer, padding);
                            ColorConsole.WriteLine();
                        }
                    }

                    ColorConsole.Write("> ".Blue());
                    var input = Console.ReadLine();

                    // Proceed with all Work-item details if the input is blank
                    if (string.IsNullOrWhiteSpace(input))
                    {
                        this.PrintAllWorkItems(account, workItems);
                        ColorConsole.WriteLine();
                    }
                    else if (int.TryParse(input, out index)) // Fetch details for the Account based on the numeric index provided
                    {
                        await this.Execute(index);
                        break;
                    }
                    else
                    {
                        this.PrintWorkItems(input, workItems);
                    }

                    do
                    {
                        ColorConsole.Write("> ".Blue());
                        input = Console.ReadLine();

                        // Fetch details for the Work-item based on the text provided
                        if (!string.IsNullOrWhiteSpace(input))
                        {
                            this.PrintWorkItems(input, workItems);
                        }
                    }
                    while (!string.IsNullOrWhiteSpace(input));
                }
                else
                {
                    ColorConsole.WriteLine("No Work-items found for the given Query!".DarkYellow());
                }
            }
        }

        private void PrintSlicer(Account account, List<WorkItem> workItems, string slicer, int padding)
        {
            var headers = workItems.GroupBy(x => x.Fields.GetPropertyValue(slicer)).OrderBy(x => x.Key);
            var max = headers.Max(x => x.Count());
            ColorConsole.WriteLine(Tab.PadLeft(padding + 1), $" {slicer.ToUpperInvariant()} ".Black().OnWhite());
            foreach (var header in headers)
            {
                ColorConsole.Write($" {header.Key?.ToString()?.Replace($"{account.Project}\\", string.Empty) ?? string.Empty} ".PadLeft(padding).Color(ConsoleColor.Blue), HorizontalChar, VerticalChar);
                var states = header.Select(x => x.Fields.State).OrderBy(s => s);
                foreach (var state in states.GroupBy(x => x))
                {
                    foreach (var item in state)
                    {
                        ColorConsole.Write(" ".On(StateColors[state.Key]));
                    }
                }

                var statusTexts = new List<ColorToken>();
                ColorConsole.Write($" {header.Count()} ");
                foreach (var state in states.GroupBy(x => x))
                {
                    statusTexts.Add($"{state.Key}: {state.Count()} ({state.Count() * 100 / states.Count()}%) ".Color(StateColors[state.Key]));
                }

                ColorConsole.WriteLine(statusTexts.ToArray());
            }

            ColorConsole.WriteLine($" {BorderChar}".PadLeft(padding + 2), string.Join(HorizontalChar, Enumerable.Range(0, max + 1).Select(x => string.Empty)), $" {max}".Blue());
        }

        private void PrintAllWorkItems(Account account, List<WorkItem> workItems)
        {
            var slicer = (string.IsNullOrWhiteSpace(account.Slicers) ? this.settings.Slicers : account.Slicers).Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)?.FirstOrDefault();
            ColorConsole.WriteLine(Tab, $" {slicer.ToUpperInvariant()} ".Black().OnWhite());
            foreach (var group in workItems.GroupBy(x => x.Fields.GetPropertyValue(slicer ?? nameof(x.Fields.Tags))).OrderBy(x => x.Key))
            {
                ColorConsole.WriteLine();
                ColorConsole.WriteLine(Tab, $" {group.Key} ".White().OnDarkBlue());
                foreach (var workItem in group.OrderBy(x => x.Fields.State))
                {
                    ColorConsole.WriteLine(Tab, $"[{workItem.Fields.State.FirstOrDefault()}] ".Color(StateColors[workItem.Fields.State]), $"{workItem.Id} - {workItem.Fields.Title}");
                }
            }
        }

        private void PrintWorkItems(string input, List<WorkItem> workItems)
        {
            if (int.TryParse(input, out var index))
            {
                var workItem = workItems.SingleOrDefault(wi => wi.Id.Equals(index));
                this.PrintWorkItemDetails(workItem, 1);
                ColorConsole.WriteLine();
            }
            else
            {
                var split = input.Split(new[] { ' ', ':', '=' }, 2);
                if (split.Length == 2 && split.Contains("open", StringComparer.OrdinalIgnoreCase))
                {
                    var workItem = workItems.SingleOrDefault(item => item.Id.ToString().Equals(split.SingleOrDefault(s => !s.Equals("open", StringComparison.OrdinalIgnoreCase))));
                    Process.Start(new ProcessStartInfo { FileName = workItem.Url.Replace("_apis/wit/workItems", "_workitems/edit"), UseShellExecute = true });
                }
                else
                {
                    var items = workItems.Where(item => item.Flatten().Any(x => x.Key.Contains(split.FirstOrDefault(), StringComparison.OrdinalIgnoreCase) && x.Value.Contains(split.LastOrDefault(), StringComparison.OrdinalIgnoreCase)))?.ToList();
                    if (items?.Count <= 0)
                    {
                        items = workItems.Where(item => item.Flatten().Any(x => x.Value.Contains(input, StringComparison.OrdinalIgnoreCase)))?.ToList();
                    }

                    if (items?.Count > 0)
                    {
                        foreach (var workItem in items.Select((x, i) => (x, i: i + 1)))
                        {
                            this.PrintWorkItemDetails(workItem.x, workItem.i);
                        }

                        ColorConsole.WriteLine();
                    }
                }
            }
        }

        private void PrintWorkItemDetails(WorkItem workItem, int index)
        {
            if (workItem != null)
            {
                ColorConsole.WriteLine();
                ColorConsole.WriteLine(Tab, $"{index}. ".PadLeft(Padding + 2), workItem.Id.ToString().Color(StateColors[workItem.Fields.State]), " - ", workItem.Fields.Title.Color(StateColors[workItem.Fields.State]));
                ColorConsole.WriteLine(Tab, string.Empty.PadLeft(Padding), "  ", $" {workItem.Fields.State} / P{workItem.Fields.Priority} / OE = {workItem.Fields.OriginalEstimate} / CW = {workItem.Fields.CompletedWork} / RW = {workItem.Fields.RemainingWork} ".Black().OnGray());
                ColorConsole.WriteLine(Tab, nameof(workItem.Fields.Tags).PadLeft(Padding).Blue(), ": ", workItem.Fields.Tags);
                ColorConsole.WriteLine(Tab, nameof(workItem.Fields.AssignedTo).PadLeft(Padding).Blue(), ": ", workItem.Fields.AssignedTo);
                ColorConsole.WriteLine(Tab, nameof(workItem.Fields.IterationPath).PadLeft(Padding).Blue(), ": ", workItem.Fields.IterationPath);
                ColorConsole.WriteLine(Tab, nameof(workItem.Fields.AreaPath).PadLeft(Padding).Blue(), ": ", workItem.Fields.AreaPath);
                ColorConsole.WriteLine(Tab, nameof(workItem.Fields.Description).PadLeft(Padding).Blue(), ": ", string.IsNullOrWhiteSpace(workItem.Fields.Description) ? string.Empty : HttpUtility.HtmlDecode(Regex.Replace(workItem.Fields.Description.Replace(Environment.NewLine, Environment.NewLine + Tab + Tab.PadLeft(Padding + 1)), "<.*?>", string.Empty)));
            }
        }
    }
}
