﻿namespace Taskr
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    using ColoredConsole;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    using NuGet.Common;
    using NuGet.Protocol;
    using NuGet.Protocol.Core.Types;

    public class Worker : BackgroundService
    {
        internal const int Padding = 17;
        internal const string Tab = "    ";

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
        private readonly IHostApplicationLifetime appLifetime;
        private readonly AutoCompletionHandler autoCompletionHandler;
        private readonly IServiceProvider services;
        private AccountSettings settings = null;

        public Worker(IOptionsMonitor<AccountSettings> settingsMonitor, ILogger<Worker> logger, IServiceProvider services, IHostApplicationLifetime appLifetime)
        {
            this.settings = settingsMonitor.CurrentValue;
            settingsMonitor.OnChange(changedSettings =>
            {
                this.settings = changedSettings;
                this.AccountsData = new AccountsData { Items = this.settings.Accounts.Where(a => a.Enabled).ToList() };
            });
            this.logger = logger;
            this.services = services;
            this.appLifetime = appLifetime;
            this.autoCompletionHandler = new AutoCompletionHandler(this);
        }

        internal int Index { get; set; }

        internal FlowStep FlowStep { get; set; }

        internal AccountsData AccountsData { get; set; }

        protected async override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (this.settings.CheckUpdates)
            {
                await this.CheckForUpdates(stoppingToken);
            }

            ReadLine.HistoryEnabled = true;
            ReadLine.AutoCompletionHandler = this.autoCompletionHandler;

            this.AccountsData = new AccountsData { Items = this.settings.Accounts.Where(a => a.Enabled).ToList() };
            await this.ProcessAsync(stoppingToken);
        }

        private async Task ProcessAsync(CancellationToken stoppingToken)
        {
            this.PrintHelp();
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (this.FlowStep == FlowStep.Accounts)
                    {
                        this.PrintAccounts();
                    }

                    ColorConsole.Write("> ".Color(this.FlowStep == FlowStep.Accounts ? ConsoleColor.Cyan : ConsoleColor.Blue));
                    var key = ReadLine.Read()?.Trim(new[] { '\0', ' ', '\t' });

                    // Proceed with all Work-item details if the input is blank
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        if (this.FlowStep == FlowStep.Slicers)
                        {
                            var currentAccount = this.AccountsData.Items.ElementAt(this.Index - 1);
                            this.PrintAllWorkItems(currentAccount);
                            ColorConsole.WriteLine();
                        }
                        else
                        {
                            this.FlowStep = FlowStep.Accounts;
                        }
                    }
                    else if (key.EqualsIgnoreCase("q") || key.EqualsIgnoreCase("quit"))
                    {
                        break;
                    }
                    else if (key.Equals("?") || key.EqualsIgnoreCase("help"))
                    {
                        this.PrintHelp();
                    }
                    else if (key.EqualsIgnoreCase("c") || key.EqualsIgnoreCase("cls"))
                    {
                        Console.Clear();
                    }
                    else if (key.Equals("+") || key.EqualsIgnoreCase("update"))
                    {
                        await this.UpdateTool(stoppingToken);
                    }
                    else if (key.Equals("up"))
                    {
                        ColorConsole.Write("This will update the 'Description' of all the Work-items from Plain-text to HTML. Continue? (Y/N) ");
                        if (Console.ReadLine().EqualsIgnoreCase("Y"))
                        {
                            var acc = this.AccountsData.Items.ElementAt(this.Index - 1);
                            await (this.services.GetServices<IBacklogService>().SingleOrDefault(x => x.AccountType.Equals(acc.Type)) as AzDoService).UpdateWorkItems(acc, stoppingToken);
                        }
                    }
                    else
                    {
                        if (int.TryParse(key, out var index))
                        {
                            if (index > 0 && index <= this.settings.Accounts.Count())
                            {
                                this.Index = index;
                                await this.Execute(index, stoppingToken); // Fetch details for the Account based on the numeric index provided
                            }
                            else
                            {
                                if (this.Index > 0)
                                {
                                    var currentAccount = this.AccountsData.Items.ElementAt(this.Index - 1);
                                    var workItem = currentAccount.WorkItems.SingleOrDefault(wi => wi.Id.Equals(index));
                                    if (this.PrintWorkItemDetails(workItem, 1))
                                    {
                                        ColorConsole.WriteLine();
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (this.Index > 0)
                            {
                                var currentAccount = this.AccountsData.Items.ElementAt(this.Index - 1);
                                this.PrintWorkItems(key, currentAccount.WorkItems);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ColorConsole.WriteLine(ex.Message.Red());
                }
            }

            Environment.ExitCode = 0;
            this.appLifetime.StopApplication();
        }

        private async Task Execute(int index, CancellationToken cancellationToken)
        {
            var accounts = index > 0 && index <= this.settings.Accounts.Count() ? new[] { this.settings.Accounts[index - 1] } : this.settings.Accounts.Where(a => a.Enabled).ToArray();
            var results = this.GetTasks(cancellationToken, accounts);
            await foreach (var items in results)
            {
                var account = items.account;
                account.WorkItems = items.workItems ?? new List<WorkItem>();
                var workItems = account.WorkItems;

                ColorConsole.WriteLine();
                this.FlowStep = FlowStep.Slicers;
                if (workItems?.Count > 0)
                {
                    this.PrintSlicers(account);
                }
                else
                {
                    ColorConsole.WriteLine("No Work-items found for the given Query!".DarkYellow());
                }
            }
        }

        private async Task<bool> UpdateTool(CancellationToken cancellationToken)
        {
            var check = await this.CheckForUpdates(cancellationToken);
            if (check)
            {
                Parallel.ForEach(
                    new[]
                    {
                        (cmd: "cmd", args: $"/c \"dotnet tool update -g --no-cache --ignore-failed-sources {nameof(Taskr).ToLowerInvariant()}\" & {nameof(Taskr).ToLowerInvariant()}", hide: false),
                        (cmd: "taskkill", args: $"/im {nameof(Taskr).ToLowerInvariant()}.exe /f", hide: true),
                    },
                    task => Process.Start(new ProcessStartInfo { FileName = task.cmd, Arguments = task.args, CreateNoWindow = task.hide, UseShellExecute = !task.hide }));
            }

            return check;
        }

        private async Task<bool> CheckForUpdates(CancellationToken cancellationToken)
        {
            ColorConsole.WriteLine("\nChecking for updates", "...".Cyan());
            var update = false;
            var repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
            var resource = await repository.GetResourceAsync<FindPackageByIdResource>();
            var version = (await resource.GetAllVersionsAsync(nameof(Taskr), new SourceCacheContext(), NullLogger.Instance, cancellationToken)).OrderByDescending(v => v.Version).FirstOrDefault();
            if (version.Version > this.settings.Version)
            {
                ColorConsole.WriteLine($"Update available: ", $"{version}".Cyan());
                update = true;
            }
            else
            {
                ColorConsole.WriteLine($"You are on the latest version: ", $"{version}".Cyan());
            }

            ColorConsole.WriteLine("-------------------------------------------------------".Cyan());
            return update;
        }

        private void PrintHelp()
        {
            ColorConsole.WriteLine("USAGE (", $"{this.settings.Version}".Cyan(), ")", ":".Cyan());
            ColorConsole.WriteLine("-------------------------------------------------------".Cyan());
            ColorConsole.WriteLine("> ".Cyan(), "field:<TAB>", " // For Auto-completion".DarkGreen());
            ColorConsole.WriteLine("> ".Cyan(), "2", " // Index of the Account to fetch the Work-items for".DarkGreen());
            ColorConsole.WriteLine("> ".Cyan(), "<ENTER>", " // Display all Work-items for the Account".DarkGreen());
            ColorConsole.WriteLine("> ".Cyan(), "5680", " // ID of the Work-item to print the details for".DarkGreen());
            ColorConsole.WriteLine("> ".Cyan(), "secure practices", " // Phrase to filter the Work-items (searches across all 'fields')".DarkGreen());
            ColorConsole.WriteLine("> ".Cyan(), "tags=security", " // 'field-name' and 'value' to filter the Work-items (searches the specified 'field' for the provided 'value')".DarkGreen());
            ColorConsole.WriteLine("> ".Cyan(), "open 5680", " // Opens the Work-item (ID: 5680) in the default browser".DarkGreen());
            ColorConsole.WriteLine("> ".Cyan(), "cls", " // Clears the console".DarkGreen());
            ColorConsole.WriteLine("> ".Cyan(), "quit", " // Quits the app".DarkGreen());
            ColorConsole.WriteLine("> ".Cyan(), "+", " // Updates Taskr to latest version".DarkGreen());
            ColorConsole.WriteLine("> ".Cyan(), "?", " // Print Help".DarkGreen());
            ColorConsole.WriteLine("-------------------------------------------------------".Cyan());
        }

        private void PrintAccounts()
        {
            ColorConsole.WriteLine();
            this.FlowStep = FlowStep.Accounts;
            foreach (var item in this.settings.Accounts.Where(a => a.Enabled).Select((x, i) => (index: i, account: x)))
            {
                ColorConsole.WriteLine($"{item.index + 1}".Cyan(), $" {(string.IsNullOrWhiteSpace(item.account.Name) ? item.account.Org + " / " + item.account.Project : item.account.Name)}");
            }
        }

        private async IAsyncEnumerable<(Account account, List<WorkItem> workItems)> GetTasks([EnumeratorCancellation] CancellationToken cancellationToken, params Account[] accounts)
        {
            foreach (var account in accounts)
            {
                ColorConsole.WriteLine($" {account.Org} / {account.Project} ".Black().OnCyan());
                await this.SetAuthTokenAsync(account);
                yield return (account, workItems: await this.services.GetServices<IBacklogService>().SingleOrDefault(x => x.AccountType.Equals(account.Type)).GetWorkItems(account, cancellationToken).ConfigureAwait(false));
            }
        }

        private async Task SetAuthTokenAsync(Account account)
        {
            if (account.Type.Equals(AccountType.AzDo) && string.IsNullOrWhiteSpace(account.Token))
            {
                var tenant = await account.Org.GetTenantId();
                account.Token = await AuthService.GetAuthTokenAsync(tenant); // SetSettings(Settings);
            }
        }

        private void PrintSlicers(Account account)
        {
            var slicers = (string.IsNullOrWhiteSpace(account.Slicers) ? this.settings.Slicers : account.Slicers).Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)?.ToList();
            if (slicers?.Count > 0)
            {
                // var headers = slicers.SelectMany(s => workItems.GroupBy(x => this.GetPropertyValue(x, s, account)).OrderBy(x => x.Key));
                var headers = slicers.SelectMany(s => this.GetHeaders(account, s));
                var padding = headers.Max(h => h.Key?.ToString()?.Replace($"{account.Project}\\", string.Empty)?.Length ?? 0) + Tab.Length;
                foreach (var slicer in slicers)
                {
                    this.PrintSlicer(account, slicer, padding);
                    ColorConsole.WriteLine();
                }
            }
        }

        private void PrintSlicer(Account account, string slicer, int padding)
        {
            // var headers = workItems.GroupBy(x => this.GetPropertyValue(x, slicer, account)).OrderBy(x => x.Key);
            var headers = this.GetHeaders(account, slicer);
            var max = headers.Max(x => x.Count());
            ColorConsole.WriteLine(Tab.PadLeft(padding + 1), $" {slicer.ToUpperInvariant()} ".Black().OnWhite());
            foreach (var header in headers)
            {
                ColorConsole.Write($" {header.Key?.ToString()?.Replace($"{account.Project}\\", string.Empty) ?? string.Empty} ".PadLeft(padding).Color(ConsoleColor.Blue), HorizontalChar, VerticalChar);
                var states = header.Select(x => x.Item.Fields.State).OrderBy(s => s);
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

        private void PrintAllWorkItems(Account account)
        {
            this.FlowStep = FlowStep.Details;
            var slicer = (string.IsNullOrWhiteSpace(account.Slicers) ? this.settings.Slicers : account.Slicers).Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)?.FirstOrDefault() ?? "Tags";
            ColorConsole.WriteLine(Tab, $" {slicer.ToUpperInvariant()} ".Black().OnWhite());

            // foreach (var group in workItems.GroupBy(x => this.GetPropertyValue(x, slicer ?? nameof(x.Fields.Tags), account)).OrderBy(x => x.Key))
            foreach (var group in this.GetHeaders(account, slicer))
            {
                ColorConsole.WriteLine();
                ColorConsole.WriteLine(Tab, $" {group.Key} ".White().OnDarkBlue());
#pragma warning disable S3267 // Loops should be simplified with "LINQ" expressions
                foreach (var item in group.OrderBy(x => x.Item.Fields.State))
#pragma warning restore S3267 // Loops should be simplified with "LINQ" expressions
                {
                    var workItem = item.Item;
                    ColorConsole.WriteLine(Tab, $"[{workItem.Fields.State.FirstOrDefault()}] ".Color(StateColors[workItem.Fields.State]), $"{workItem.Id} - {workItem.Fields.Title}", $" ({workItem.Fields.AssignedTo.Split(' ').FirstOrDefault()} - {workItem.Fields.StateDurationDays}d)".Color(StateColors[workItem.Fields.State]));
                }
            }
        }

        private void PrintWorkItems(string input, List<WorkItem> workItems)
        {
            this.FlowStep = FlowStep.Details;
            var split = input.Split(this.autoCompletionHandler.Separators, 2)?.Select(x => x.Trim()).ToArray();
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

        private bool PrintWorkItemDetails(WorkItem workItem, int index)
        {
            this.FlowStep = FlowStep.Details;
            if (workItem != null)
            {
                ColorConsole.WriteLine();
                ColorConsole.WriteLine(Tab, $"{index}. ".PadLeft(Padding + 2), workItem.Id.ToString().Color(StateColors[workItem.Fields.State]), " - ", workItem.Fields.Title.Color(StateColors[workItem.Fields.State]));
                ColorConsole.WriteLine(Tab, string.Empty.PadLeft(Padding), "  ", $" P{workItem.Fields.Priority} / OE = {workItem.Fields.OriginalEstimate} / CW = {workItem.Fields.CompletedWork} / RW = {workItem.Fields.RemainingWork} / {workItem.Fields.State} ({workItem.Fields.Reason} - {workItem.Fields.StateDurationDays}d) ".Black().OnGray());
                ColorConsole.WriteLine(Tab, nameof(workItem.Fields.Tags).PadLeft(Padding).Blue(), ": ", workItem.Fields.Tags);
                ColorConsole.WriteLine(Tab, nameof(workItem.Fields.AssignedTo).PadLeft(Padding).Blue(), ": ", workItem.Fields.AssignedTo);
                ColorConsole.WriteLine(Tab, nameof(workItem.Fields.IterationPath).PadLeft(Padding).Blue(), ": ", workItem.Fields.IterationPath);
                ColorConsole.WriteLine(Tab, nameof(workItem.Fields.AreaPath).PadLeft(Padding).Blue(), ": ", workItem.Fields.AreaPath);
                ColorConsole.WriteLine(Tab, nameof(workItem.Fields.Description).PadLeft(Padding).Blue(), ": ", workItem.Fields.Description ?? string.Empty);
            }

            return workItem != null;
        }

        private IOrderedEnumerable<IGrouping<string, (string Key, WorkItem Item)>> GetHeaders(Account account, string slicer)
        {
            // return account.WorkItems.SelectMany(x => x.Fields.GetPropertyValues(slicer).Select(y => new { Key = y, Item = x })).GroupBy(x => x.Key).OrderBy(x => x.Key);
            var exclusions = account.Exclusions?.Count > 0 ? account.Exclusions : this.settings.Exclusions;
            return account.WorkItems.SelectMany(x => x.Fields.GetPropertyValues(slicer).Select(y => (Key: y, Item: x))).Where(x => !(exclusions?.Count > 0) || exclusions.All(e => !x.Key.ContainsIgnoreCase(e))).GroupBy(x => x.Key).OrderBy(x => x.Key);
        }
    }
}
