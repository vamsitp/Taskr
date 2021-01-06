namespace Taskr
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

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
                    await Execute(index);
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
            var settingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Taskr.json");
            if (!File.Exists(settingsFile))
            {
                var settings = JsonConvert.SerializeObject(new AccountSettings { Query = DefaultQuery, Slicers = "Tags,Priority", Accounts = new[] { new Account { Name = "Account-1", Org = "Org-1", Project = "Project-1", Token = "PAT Token for Org-1/Project-1", Enabled = true }, new Account { Name = "Account-2", Org = "Org-2", Project = "Project-2", Token = "PAT Token for Org-2/Project-2", Enabled = true } } }, Formatting.Indented);
                File.WriteAllText(settingsFile, settings);
                ColorConsole.WriteLine("Update settings here: ".Red(), settingsFile);
            }

            return File.ReadAllText(settingsFile);
        }

        private static async IAsyncEnumerable<List<WorkItem>> GetTasks(int index)
        {
            var defaultWiql = Settings.Query ?? DefaultQuery;
            if (index > 0 && index <= Settings.Accounts.Count())
            {
                var account = Settings.Accounts[index - 1];
                ColorConsole.WriteLine($" {account.Org} / {account.Project} ".Black().OnCyan());
                yield return await AzDOService.GetWorkItems(account, defaultWiql).ConfigureAwait(false);
            }
            else
            {
                foreach (var account in Settings.Accounts.Where(a => a.Enabled))
                {
                    ColorConsole.WriteLine($" {account.Org} / {account.Project} ".Black().OnCyan());
                    yield return await AzDOService.GetWorkItems(account, defaultWiql).ConfigureAwait(false);
                }
            }
        }

        private static async Task Execute(int index)
        {
            var results = GetTasks(index);
            await foreach (var items in results)
            {
                ColorConsole.WriteLine();
                foreach (var slicer in Settings.Slicers.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    PrintSlicer(items, slicer);
                    ColorConsole.WriteLine();
                }

                Debug.WriteLine("input-1");
                ColorConsole.Write("> ".Blue());
                var input = Console.ReadLine();

                // Proceed with all Work-item details if the input is blank
                if (string.IsNullOrWhiteSpace(input))
                {
                    PrintAllWorkItems(items);
                    ColorConsole.WriteLine();
                }
                else if (int.TryParse(input, out index)) // Fetch details for the Account based on the numeric index provided
                {
                    await Execute(index);
                    break;
                }

                do
                {
                    Debug.WriteLine("input-2");
                    ColorConsole.Write("> ".Blue());
                    input = Console.ReadLine();

                    // Fetch details for the Work-item based on the text provided
                    if (!string.IsNullOrWhiteSpace(input))
                    {
                        PrintWorkItems(input, items);
                    }
                }
                while (!string.IsNullOrWhiteSpace(input));
            }
        }

        private static void PrintSlicer(List<WorkItem> items, string slicer)
        {
            var headers = items.GroupBy(x => x.Fields.GetPropertyValue(slicer)).OrderBy(x => x.Key);
            var max = headers.Max(x => x.Count());
            var padding = headers.Max(h => h.Key.ToString().Length) + Tab.Length;
            ColorConsole.WriteLine(Tab.PadLeft(padding + 1), $" {slicer.ToUpperInvariant()} ".Black().OnWhite());
            foreach (var workItems in headers)
            {
                ColorConsole.Write($" {workItems.Key} ".PadLeft(padding).Color(ConsoleColor.Blue), HorizontalChar, VerticalChar);
                var states = workItems.Select(x => x.Fields.State).OrderBy(s => s);
                foreach (var state in states.GroupBy(x => x))
                {
                    foreach (var item in state)
                    {
                        ColorConsole.Write(" ".On(StateColors[state.Key]));
                    }
                }

                var statusTexts = new List<ColorToken>();
                ColorConsole.Write($" {workItems.Count()} ");
                foreach (var state in states.GroupBy(x => x))
                {
                    statusTexts.Add($"{state.Key}: {state.Count()} ({state.Count() * 100 / states.Count()}%) ".Color(StateColors[state.Key]));
                }

                ColorConsole.WriteLine(statusTexts.ToArray());
            }

            ColorConsole.WriteLine($" {BorderChar}".PadLeft(padding + 2), string.Join(HorizontalChar, Enumerable.Range(0, max + 1).Select(x => string.Empty)), $" {max}".Blue());
        }

        private static void PrintAllWorkItems(List<WorkItem> items)
        {
            foreach (var workItems in items.GroupBy(x => x.Fields.Tags))
            {
                ColorConsole.WriteLine();
                ColorConsole.WriteLine(Tab, $" {workItems.Key} ".White().OnDarkBlue());
                foreach (var workItem in workItems.OrderBy(x => x.Fields.State))
                {
                    ColorConsole.WriteLine(Tab, $"[{workItem.Fields.State.FirstOrDefault()}] ".Color(StateColors[workItem.Fields.State]), $"{workItem.Id} - {workItem.Fields.Title}");
                }
            }
        }

        private static void PrintWorkItems(string input, List<WorkItem> items)
        {
            if (int.TryParse(input, out var index))
            {
                var workItem = items.SingleOrDefault(wi => wi.Id.Equals(index));
                PrintWorkItemDetails(workItem, items);
            }
            else
            {
                var workItems = items.Where(item => item.Flatten().Any(x => x.Value.Contains(input, StringComparison.OrdinalIgnoreCase)))?.ToList();
                foreach (var workItem in workItems)
                {
                    PrintWorkItemDetails(workItem, items);
                    ColorConsole.WriteLine();
                }
            }
        }

        private static void PrintWorkItemDetails(WorkItem workItem, List<WorkItem> items)
        {
            if (workItem != null)
            {
                const int Padding = 17;
                ColorConsole.WriteLine(Tab, workItem.Fields.State.PadLeft(Padding).Color(StateColors[workItem.Fields.State]), ": ", (workItem.Id.ToString() + " - " + workItem.Fields.Title).Color(StateColors[workItem.Fields.State]));
                ColorConsole.WriteLine(Tab, "Pillar".PadLeft(Padding).Blue(), ": ", workItem.Fields.Tags);
                ColorConsole.WriteLine(Tab, nameof(workItem.Fields.AssignedTo).PadLeft(Padding).Blue(), ": ", workItem.Fields.AssignedTo?.DisplayName ?? (workItem.Fields.AssignedTo?.UniqueName ?? string.Empty));
                ColorConsole.WriteLine(Tab, nameof(workItem.Fields.OriginalEstimate).PadLeft(Padding).Blue(), ": ", workItem.Fields.OriginalEstimate.ToString());
                ColorConsole.WriteLine(Tab, nameof(workItem.Fields.CompletedWork).PadLeft(Padding).Blue(), ": ", workItem.Fields.CompletedWork.ToString());
                ColorConsole.WriteLine(Tab, nameof(workItem.Fields.RemainingWork).PadLeft(Padding).Blue(), ": ", workItem.Fields.RemainingWork.ToString());
                ColorConsole.WriteLine(Tab, nameof(workItem.Fields.IterationPath).PadLeft(Padding).Blue(), ": ", workItem.Fields.IterationPath);
                ColorConsole.WriteLine(Tab, nameof(workItem.Fields.AreaPath).PadLeft(Padding).Blue(), ": ", workItem.Fields.AreaPath);
                ColorConsole.WriteLine(Tab, nameof(workItem.Fields.Description).PadLeft(Padding).Blue(), ": ", Regex.Replace(workItem.Fields.Description.Replace(Environment.NewLine, Environment.NewLine + Tab + Tab.PadLeft(Padding + 1)), "<.*?>", String.Empty));
            }
        }
    }
}
