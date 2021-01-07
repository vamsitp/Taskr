namespace Taskr
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Web;

    using ColoredConsole;

    using Newtonsoft.Json;

    class Program
    {
        private const string Tab = "    ";
        private const string VerticalChar = "│";
        private const string HorizontalChar = "─";
        private const string BorderChar = "└";
        private const string DefaultQuery = "SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{0}' AND [System.WorkItemType] = 'Task' ORDER BY [System.Id] ASC";

        private static AccountSettings Settings;
        private static string SettingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Taskr.json");
        private static Dictionary<string, ConsoleColor> StateColors = new Dictionary<string, ConsoleColor>
        {
            { "New", ConsoleColor.Red },
            { "Active", ConsoleColor.DarkYellow },
            { "Closed", ConsoleColor.DarkGreen },
            { "To Do", ConsoleColor.Red },
            { "In Progress", ConsoleColor.DarkYellow },
            { "Done", ConsoleColor.DarkGreen },
            { "Removed", ConsoleColor.DarkGray },
        };

        static async Task Main(string[] args)
        {
            try
            {
                // Console.SetWindowPosition(0, 0);
                Console.SetWindowSize(Console.LargestWindowWidth - 10, Console.LargestWindowHeight - 10);
            }
            catch
            {
            }

            Settings = JsonConvert.DeserializeObject<AccountSettings>(GetSettings());
            while (true)
            {
                foreach (var item in Settings.Accounts.Select((x, i) => (index: i, account: x)))
                {
                    ColorConsole.WriteLine($"{item.index + 1}".Cyan(), $" {(string.IsNullOrWhiteSpace(item.account.Name) ? item.account.Org + " / " + item.account.Project : item.account.Name)}");
                }

                Debug.WriteLine("input-0");
                ColorConsole.Write("> ".Cyan());
                var input = Console.ReadLine();
                if (int.TryParse(input, out var index))
                {
                    try
                    {
                        await Execute(index);
                    }
                    catch (Exception ex)
                    {
                        ColorConsole.WriteLine(ex.Message.Red());
                    }
                }
                else if (input.Equals("clear", StringComparison.OrdinalIgnoreCase) || input.Equals("cls", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Clear();
                }
                else if (input.Equals("quit", StringComparison.OrdinalIgnoreCase) || input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }
        }

        private static string GetSettings()
        {
            if (!File.Exists(SettingsFile))
            {
                var settings = new AccountSettings { Query = DefaultQuery, Slicers = "Tags,Priority,IterationPath", Accounts = new[] { new Account { Name = "Account-1", Org = "Org-1", Project = "Project-1", Token = "PAT Token for Org-1/Project-1", Enabled = true }, new Account { Name = "Account-2", Org = "Org-2", Project = "Project-2", Token = "PAT Token for Org-2/Project-2", Enabled = true } } };
                SetSettings(settings);
                ColorConsole.WriteLine("Update settings here: ".Red(), SettingsFile);
            }

            return File.ReadAllText(SettingsFile);
        }

        private static void SetSettings(AccountSettings settings)
        {
            File.WriteAllText(SettingsFile, JsonConvert.SerializeObject(settings, new JsonSerializerSettings { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore }));
        }

        private static async IAsyncEnumerable<(Account account, List<WorkItem> workItems)> GetTasks(params Account[] accounts)
        {
            var defaultWiql = Settings.Query ?? DefaultQuery;
            foreach (var account in accounts)
            {
                ColorConsole.WriteLine($" {account.Org} / {account.Project} ".Black().OnCyan());
                yield return (account, workItems: await AzDOService.GetWorkItems(account, defaultWiql).ConfigureAwait(false));
            }
        }

        private static async Task SetAuthTokenAsync(Account account)
        {
            if (string.IsNullOrWhiteSpace(account.Token))
            {
                var tenant = await account.Org.GetTenantId();
                account.Token = await AuthHelper.GetAuthTokenAsync(tenant);
                // SetSettings(Settings);
            }
        }

        private static async Task Execute(int index)
        {
            var accounts = index > 0 && index <= Settings.Accounts.Count() ? new[] { Settings.Accounts[index - 1] } : Settings.Accounts.Where(a => a.Enabled).ToArray();
            var results = GetTasks(accounts);
            await foreach (var items in results)
            {
                var account = items.account;
                var workItems = items.workItems;
                ColorConsole.WriteLine();
                if (workItems?.Count > 0)
                {
                    var slicers = (string.IsNullOrWhiteSpace(account.Slicers) ? Settings.Slicers : account.Slicers).Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)?.ToList();
                    if (slicers?.Count > 0)
                    {
                        var headers = slicers.SelectMany(s => workItems.GroupBy(x => x.Fields.GetPropertyValue(s)).OrderBy(x => x.Key));
                        var padding = headers.Max(h => h.Key?.ToString()?.Replace($"{account.Project}\\", string.Empty)?.Length ??  0) + Tab.Length;
                        foreach (var slicer in slicers)
                        {
                            PrintSlicer(account, workItems, slicer, padding);
                            ColorConsole.WriteLine();
                        }
                    }

                    Debug.WriteLine("input-1");
                    ColorConsole.Write("> ".Blue());
                    var input = Console.ReadLine();

                    // Proceed with all Work-item details if the input is blank
                    if (string.IsNullOrWhiteSpace(input))
                    {
                        PrintAllWorkItems(account, workItems);
                        ColorConsole.WriteLine();
                    }
                    else if (int.TryParse(input, out index)) // Fetch details for the Account based on the numeric index provided
                    {
                        await Execute(index);
                        break;
                    }
                    else
                    {
                        PrintWorkItems(input, workItems);
                    }

                    do
                    {
                        Debug.WriteLine("input-2");
                        ColorConsole.Write("> ".Blue());
                        input = Console.ReadLine();

                        // Fetch details for the Work-item based on the text provided
                        if (!string.IsNullOrWhiteSpace(input))
                        {
                            PrintWorkItems(input, workItems);
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

        private static void PrintSlicer(Account account, List<WorkItem> workItems, string slicer, int padding)
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

        private static void PrintAllWorkItems(Account account, List<WorkItem> workItems)
        {
            var slicer = (string.IsNullOrWhiteSpace(account.Slicers) ? Settings.Slicers : account.Slicers).Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)?.FirstOrDefault();
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

        private static void PrintWorkItems(string input, List<WorkItem> workItems)
        {
            if (int.TryParse(input, out var index))
            {
                var workItem = workItems.SingleOrDefault(wi => wi.Id.Equals(index));
                PrintWorkItemDetails(workItem);
            }
            else
            {
                var split = input.Split(new[] { ' ', ':' }, StringSplitOptions.RemoveEmptyEntries);
                if (split.Length == 2 && split.Contains("open", StringComparer.OrdinalIgnoreCase))
                {
                    var workItem = workItems.SingleOrDefault(item => item.Id.ToString().Equals(split.SingleOrDefault(s => !s.Equals("open", StringComparison.OrdinalIgnoreCase))));
                    Process.Start(new ProcessStartInfo { FileName = workItem.Url.Replace("_apis/wit/workItems", "_workitems/edit"), UseShellExecute = true });
                }
                else
                {
                    var items = workItems.Where(item => item.Flatten().Any(x => x.Value.Contains(input, StringComparison.OrdinalIgnoreCase)))?.ToList();
                    foreach (var workItem in items)
                    {
                        PrintWorkItemDetails(workItem);
                        ColorConsole.WriteLine();
                    }
                }
            }
        }

        private static void PrintWorkItemDetails(WorkItem workItem)
        {
            if (workItem != null)
            {
                const int Padding = 17;
                ColorConsole.WriteLine(Tab, workItem.Fields.State.PadLeft(Padding).Color(StateColors[workItem.Fields.State]), ": ", (workItem.Id.ToString() + " - " + workItem.Fields.Title).Color(StateColors[workItem.Fields.State]));
                ColorConsole.WriteLine(Tab, string.Empty.PadLeft(Padding), "  ", $" P{workItem.Fields.Priority} / OE = {workItem.Fields.OriginalEstimate} / CW = {workItem.Fields.CompletedWork} / RW = {workItem.Fields.RemainingWork} ".Black().OnGray());
                ColorConsole.WriteLine(Tab, nameof(workItem.Fields.Tags).PadLeft(Padding).Blue(), ": ", workItem.Fields.Tags);
                ColorConsole.WriteLine(Tab, nameof(workItem.Fields.AssignedTo).PadLeft(Padding).Blue(), ": ", workItem.Fields.AssignedTo);
                ColorConsole.WriteLine(Tab, nameof(workItem.Fields.IterationPath).PadLeft(Padding).Blue(), ": ", workItem.Fields.IterationPath);
                ColorConsole.WriteLine(Tab, nameof(workItem.Fields.AreaPath).PadLeft(Padding).Blue(), ": ", workItem.Fields.AreaPath);
                ColorConsole.WriteLine(Tab, nameof(workItem.Fields.Description).PadLeft(Padding).Blue(), ": ", string.IsNullOrWhiteSpace(workItem.Fields.Description) ? string.Empty : HttpUtility.HtmlDecode(Regex.Replace(workItem.Fields.Description.Replace(Environment.NewLine, Environment.NewLine + Tab + Tab.PadLeft(Padding + 1)), "<.*?>", String.Empty)));
            }
        }
    }
}
