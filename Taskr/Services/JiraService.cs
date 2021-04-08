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
        internal const string DefaultQuery = "project={0} AND issuetype=Subtask";

        private const string BrowseUrl = "https://{0}.atlassian.net/browse/{1}";
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

                // var defaultQuery = string.IsNullOrWhiteSpace(this.settings.Query) ? DefaultQuery : this.settings.Query; //  The default query could be related to AzDO
                var query = HttpUtility.UrlEncode(string.Format(CultureInfo.InvariantCulture, string.IsNullOrWhiteSpace(account.Query) ? DefaultQuery : account.Query, account.Project));
                var baseUrl = $"https://{account.Org}.atlassian.net/rest/api/3/search?jql={query}&fields=id,summary,description,status,priority,labels,assignee,issuetype,statuscategorychangedate,customfield_10020&maxResults={Extensions.MaxSize}";
                var result = await baseUrl
                    .WithHeader(AuthHeader, pat)
                    .WithHeader("Accept", MediaType)
                    .WithHeader("Content-Type", MediaType)
                    .GetJsonAsync<JiraResponse>(cancellationToken)
                    .ConfigureAwait(false);

                var items = result.issues.Select(x => new WorkItem
                {
                    Id = int.Parse(x.id),
                    Url = string.Format(BrowseUrl, account.Org, x.key),
                    Fields = new Fields
                    {
                        Title = $"[{x.key}] {x.fields.summary}",
                        DescriptionHtml = x.fields.description?.ToString() ?? string.Empty,
                        State = x.fields.status?.name,
                        Priority = short.Parse(x.fields.priority?.id ?? "0"),
                        IterationPath = x.fields.iterations?.FirstOrDefault()?.name,
                        Tags = string.Join("; ", x.fields.labels),
                        StateChangeDate = x.fields.statuscategorychangedate,
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
