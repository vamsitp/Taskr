namespace Taskr
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal class AutoCompletionHandler : IAutoCompleteHandler
    {
        private readonly Worker worker;
        private readonly IEnumerable<string> props;

        internal AutoCompletionHandler(Worker worker)
        {
            this.worker = worker;
            this.props = typeof(Fields).GetProperties().Select(p => p.Name).Where(x => !x.Equals("AssignedToObj") && !x.Equals("DescriptionHtml"));
        }

        // characters to start completion from
        public char[] Separators { get; set; } = new char[] { ' ', ':', '=' };

        // text - The current text entered in the console
        // index - The index of the terminal cursor within {text}
        public string[] GetSuggestions(string text, int index)
        {
            if (this.worker.FlowStep == FlowStep.Accounts)
            {
                return this.worker.AccountsData.Items.Select((x, i) => (i + 1).ToString()).ToArray();
            }
            else if (this.worker.FlowStep == FlowStep.Slicers || this.worker.FlowStep == FlowStep.Details)
            {
                var id = -1;
                if (string.IsNullOrWhiteSpace(text))
                {
                    var values = this.props.Select(p => p.ToLowerInvariant() + ":" + this.worker.AccountsData.Items.ElementAt(this.worker.Index - 1).WorkItems?.FirstOrDefault().Fields?.GetPropertyValue(p)?.ToString())?.Distinct()?.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
                    return values; // new string[] { "state removed", "tags security" };
                }
                else if (text.Trim(this.Separators).EqualsIgnoreCase("id") || text.Trim(this.Separators).EqualsIgnoreCase("open") || int.TryParse(text.Trim(this.Separators), out id))
                {
                    var ids = this.worker.AccountsData.Items.ElementAt(this.worker.Index - 1).WorkItems?.Select(x => this.Separators.Any(text.EndsWith) ? x.Id.ToString() : (text.Trim(this.Separators) + ":" + x.Id.ToString()));
                    var values = id <= 0 ? ids?.ToArray() : ids.Where(i => i.StartsWith(id.ToString())).ToArray();
                    return values;
                }
                else
                {
                    var split = text.Split(this.Separators, 2)?.Select(x => x.Trim())?.ToArray();
                    var prop = split?.FirstOrDefault();
                    var value = split?.Length < 2 ? null : split?.LastOrDefault();
                    var values = this.props.Where(p => p.ContainsIgnoreCase(prop)).SelectMany(p => this.worker.AccountsData.Items.ElementAt(this.worker.Index - 1).WorkItems?.Select(x => this.Separators.Any(text.Contains) ? x.Fields?.GetPropertyValue(p)?.ToString() : p.ToLowerInvariant() + ":" + x.Fields?.GetPropertyValue(p)?.ToString()))?.Distinct()?.Where(x => !string.IsNullOrWhiteSpace(x) && (value == null || x.ContainsIgnoreCase(value))).ToArray();
                    return values;
                }
            }

            return Array.Empty<string>();
        }
    }
}
