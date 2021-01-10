namespace Taskr
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;

    using ColoredConsole;

    using Flurl.Http;

    using Microsoft.Extensions.Options;

    public class JiraService : IBacklogService
    {
        internal const string DefaultQuery = "project={0}";

        private const string MediaType = "application/json";
        private const string AuthHeader = "Authorization";

        private AccountSettings settings;

        public JiraService(IOptionsMonitor<AccountSettings> settingsMonitor)
        {
            this.settings = settingsMonitor.CurrentValue;
            settingsMonitor.OnChange(changedSettings => { this.settings = changedSettings; });
        }

        public AccountType AccountType => AccountType.Jira;

        // Auth: https://developer.atlassian.com/cloud/jira/platform/security-for-other-integrations/
        public async Task<List<WorkItem>> GetWorkItems(Account account, CancellationToken cancellationToken)
        {
            var workItemsList = new List<WorkItem>();
            try
            {
                var pat = this.GetBase64Token(account.Token);
                var defaultQuery = string.IsNullOrWhiteSpace(this.settings.Query) ? DefaultQuery : this.settings.Query;
                var query = HttpUtility.UrlEncode(string.Format(CultureInfo.InvariantCulture, string.IsNullOrWhiteSpace(account.Query) ? defaultQuery : account.Query, account.Project));
                var baseUrl = $"https://{account.Org}.atlassian.net/rest/api/3/search?jql={query}";
                var result = await baseUrl
                    .WithHeader(AuthHeader, pat)
                    .WithHeader("Accept", MediaType)
                    .WithHeader("Content-Type", MediaType)
                    .GetJsonAsync<JiraResponse>(cancellationToken)
                    .ConfigureAwait(false);

                var items = result.issues.Select(x => new WorkItem
                {
                    Id = int.Parse(x.id),
                    Fields = new Fields
                    {
                        Title = x.fields.summary,
                        Description = x.fields.description?.ToString() ?? string.Empty,
                        State = x.fields.status?.name,
                        Tags = string.Join(",", x.fields.labels),
                        AssignedToObj = new AssignedTo { DisplayName = x.fields.assignee?.displayName, UniqueName = x.fields.assignee?.emailAddress },
                    },
                });
                workItemsList.AddRange(items);
            }
            catch (Exception ex)
            {
                this.LogError(ex, ex.Message);
            }

            return workItemsList;
        }

        private void LogError(Exception ex, string message)
        {
            var fex = ex as FlurlHttpException;
            if (fex != null)
            {
                var vex = fex.GetResponseJsonAsync<AzDOException>()?.GetAwaiter().GetResult();
                message = vex?.Message ?? ex.Message;
            }

            ColorConsole.WriteLine(message?.Red() ?? string.Empty);
        }

        private string GetBase64Token(string userNamePassword)
        {
            var byteCredentials = UTF8Encoding.UTF8.GetBytes(userNamePassword);
            return "Basic " + Convert.ToBase64String(byteCredentials);
        }
    }
}
