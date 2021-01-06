namespace Taskr
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Threading.Tasks;

    using ColoredConsole;

    using Microsoft.IdentityModel.Clients.ActiveDirectory;

    // https://docs.microsoft.com/en-us/azure/devops/integrate/get-started/authentication/authentication-guidance?view=azure-devops
    // https://github.com/microsoft/azure-devops-auth-samples/blob/master/ManagedClientConsoleAppSample/Program.cs
    public static class AuthHelper
    {
        internal const string AzureDevOpsResourceId = "499b84ac-1321-427f-aa17-267ca6975798"; // Constant value to target Azure DevOps. Do not change

        internal const string ClientId = "872cd9fa-d31f-45e0-9eab-6e460a02d1f1"; // Change to your app registration's Application ID, unless you are an MSA backed account

        internal const string ReplyUri = "urn:ietf:wg:oauth:2.0:oob"; // Change to your app registration's reply URI, unless you are an MSA backed account

        internal static readonly ConcurrentDictionary<string, Lazy<Task<string>>> AuthTokens = new ConcurrentDictionary<string, Lazy<Task<string>>>();

        public static async Task<string> GetAuthTokenAsync(string tenantId)
        {
            var accessToken = await AuthTokens.GetOrAdd(tenantId ?? string.Empty, k =>
            {
                return new Lazy<Task<string>>(async () =>
                {
                    var ctx = GetAuthenticationContext(tenantId); // null
                    AuthenticationResult result = null;
                    var promptBehavior = new PlatformParameters(PromptBehavior.SelectAccount, new CustomWebUi());
                    ColorConsole.WriteLine("Authenticating...");
                    try
                    {
                        result = await ctx.AcquireTokenAsync(AzureDevOpsResourceId, ClientId, new Uri(ReplyUri), promptBehavior);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // If the token has expired, prompt the user with a login prompt
                        result = await ctx.AcquireTokenAsync(AzureDevOpsResourceId, ClientId, new Uri(ReplyUri), promptBehavior);
                    }

                    return result?.AccessToken;
                });
            }).Value;

            return accessToken;
        }

        private static AuthenticationContext GetAuthenticationContext(string tenant)
        {
            AuthenticationContext ctx = null;
            if (tenant != null)
            {
                ctx = new AuthenticationContext("https://login.microsoftonline.com/" + tenant);
            }
            else
            {
                ctx = new AuthenticationContext("https://login.windows.net/common");
                if (ctx.TokenCache.Count > 0)
                {
                    var homeTenant = ctx.TokenCache.ReadItems().First().TenantId;
                    ctx = new AuthenticationContext("https://login.microsoftonline.com/" + homeTenant);
                }
            }

            return ctx;
        }
    }
}
