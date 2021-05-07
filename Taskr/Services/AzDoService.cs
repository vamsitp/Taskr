namespace Taskr
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;

    using ColoredConsole;

    using Flurl.Http;
    using Flurl.Http.Content;

    using Microsoft.Extensions.Options;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class AzDoService : IBacklogService
    {
        internal const string DefaultQuery = "SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{0}' AND [System.WorkItemType] = 'Task' ORDER BY [System.Id] ASC";

        private const string MediaType = "application/json";
        private const string AuthHeader = "Authorization";
        private const string BasicAuthHeaderPrefix = "Basic ";
        private const string BearerAuthHeaderPrefix = "Bearer ";
        private const string WorkItemsTokenPath = ".workItems";
        private const string IdTokenPath = ".id";
        private const char WorkItemsDelimiter = ',';
        private const char Colon = ':';

        private const string WiqlUrl = "wiql?api-version=6.0";
        private const string WorkItemsUrl = "workitems?ids={0}&fields=System.Id,System.WorkItemType,System.Title,System.Description,System.Tags,System.State,System.Reason,System.AssignedTo,System.IterationPath,System.AreaPath,Microsoft.VSTS.Common.Priority,Microsoft.VSTS.Scheduling.OriginalEstimate,Microsoft.VSTS.Scheduling.CompletedWork,Microsoft.VSTS.Scheduling.RemainingWork,Microsoft.VSTS.Common.StateChangeDate&api-version=6.0";
        private const string DescriptionField = "/fields/System.Description";
        private const string AddOperation = "add";
        private const string JsonPatchMediaType = "application/json-patch+json";

        private AccountSettings settings;

        public AzDoService(IOptionsMonitor<AccountSettings> settingsMonitor)
        {
            this.settings = settingsMonitor.CurrentValue;
            settingsMonitor.OnChange(changedSettings => { this.settings = changedSettings; });
        }

        public AccountType AccountType => AccountType.AzDo;

        public async Task<List<WorkItem>> GetWorkItems(Account account, CancellationToken cancellationToken)
        {
            var pat = account.IsPat ? this.GetBase64Token(account.Token) : (BearerAuthHeaderPrefix + account.Token);
            var workItemsList = new List<WorkItem>();
            try
            {
                var defaultQuery = string.IsNullOrWhiteSpace(this.settings.Query) ? DefaultQuery : this.settings.Query;
                var wiql = new
                {
                    query = string.Format(CultureInfo.InvariantCulture, string.IsNullOrWhiteSpace(account.Query) ? defaultQuery : account.Query, account.Project),
                };
                var postValue = new StringContent(JsonConvert.SerializeObject(wiql), Encoding.UTF8, MediaType);
                var baseUrl = $"https://dev.azure.com/{account.Org}/{account.Project}/_apis/wit";
                var result = await $"{baseUrl}/{WiqlUrl}" // .WithHeader("X-TFS-FedAuthRedirect", "Suppress")
                    .WithHeader(AuthHeader, pat)
                    .PostAsync(postValue, cancellationToken)
                    .ReceiveJson<JObject>()
                    .ConfigureAwait(false);

                var response = result?.SelectTokens(WorkItemsTokenPath)?.Values<JObject>()?.ToList();
                if (response != null)
                {
                    var ids = response.Select(x => x.SelectToken(IdTokenPath).Value<int>());
                    var chunkedIds = ids.ChunkBy();
                    foreach (var cids in chunkedIds)
                    {
                        var joinedIds = string.Join(WorkItemsDelimiter, cids);
                        if (!string.IsNullOrWhiteSpace(joinedIds))
                        {
                            var workItems = await string.Format(CultureInfo.InvariantCulture, $"{baseUrl}/{WorkItemsUrl}", joinedIds.Trim(WorkItemsDelimiter))
                               .WithHeader(AuthHeader, pat)
                               .GetJsonAsync<WorkItems>(cancellationToken)
                               .ConfigureAwait(false);

                            if (workItems?.Items?.Length > 0)
                            {
                                workItemsList.AddRange(workItems.Items);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await this.LogError(ex, ex.Message);
            }

            return workItemsList;
        }

        public async Task UpdateWorkItems(Account account, CancellationToken cancellationToken)
        {
            var workItems = await this.GetWorkItems(account, cancellationToken);
            foreach (var workItem in workItems)
            {
                try
                {
                    var html = HttpUtility.HtmlDecode(workItem.Fields.DescriptionHtml);
                    if (!string.IsNullOrWhiteSpace(html))
                    {
                        var updatesFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Taskr_Updates.csv");
                        if (File.Exists(updatesFile))
                        {
                            var updates = CsvService.GetRecords<dynamic>(File.OpenRead(updatesFile));
                            foreach (dynamic update in updates)
                            {
                                var old = update.Old;
                                var updated = update.New;
                                if (!string.IsNullOrWhiteSpace(old) && !string.IsNullOrWhiteSpace(updated))
                                {
                                    // Debug.WriteLine($"{workItem.Id} - Old: {html}");
                                    html = html.Replace(old, updated).Replace(HttpUtility.UrlDecode(old), updated);

                                    // Debug.WriteLine($"{workItem.Id} - New: {html}");
                                }
                            }
                        }

                        var content = new[] { new Op { op = AddOperation, path = DescriptionField, value = html } }.ToJson();
                        using (var stringContent = new CapturedStringContent(content, JsonPatchMediaType))
                        {
                            var pat = account.IsPat ? this.GetBase64Token(account.Token) : (BearerAuthHeaderPrefix + account.Token);
                            await $"https://dev.azure.com/{account.Org}/{account.Project}/_apis/wit/workitems/{workItem.Id}?api-version=6.0"
                                .WithHeader(AuthHeader, pat)
                                .PatchAsync(stringContent);
                            ColorConsole.Write(workItem.Id.ToString(), ".".Blue());
                        }
                    }
                }
                catch (Exception ex)
                {
                    await this.LogError(ex, workItem.Id.ToString());
                }
            }

            ColorConsole.WriteLine();
        }

        private async Task LogError(Exception ex, string message)
        {
            var fex = ex as FlurlHttpException;
            if (fex != null)
            {
                var vex = await fex.GetResponseJsonAsync<AzDOException>();
                message = $"{message ?? string.Empty}: {vex?.Message ?? ex.Message}";
            }

            ColorConsole.WriteLine(message?.Red() ?? string.Empty);
        }

        private string GetBase64Token(string accessToken)
        {
            return BasicAuthHeaderPrefix + Convert.ToBase64String(Encoding.ASCII.GetBytes(Colon + accessToken.TrimStart(Colon)));
        }
    }
}
