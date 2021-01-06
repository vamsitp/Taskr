namespace Taskr
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;

    using Newtonsoft.Json.Linq;

    public static class Extensions
    {
        private static HttpClient Client = new HttpClient(new HttpClientHandler() { AllowAutoRedirect = false });

        public static Dictionary<string, string> Flatten(this object item)
        {
            var jsonObject = JObject.FromObject(item);
            var tokens = jsonObject.Descendants().Where(p => p.Count() == 0);
            var results = tokens.Aggregate(new Dictionary<string, string>(), (props, token) =>
            {
                props.Add(token.Path, token.ToString());
                return props;
            });

            return results;
        }

        public static object GetPropertyValue(this object obj, string propertyName)
        {
            return obj.GetType().GetProperty(propertyName.Trim()).GetValue(obj, null);
        }

        public static bool ContainsIgnoreCase(this string item, string subString)
        {
            return item?.IndexOf(subString, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static async Task<string> GetTenantId(this string azDoOrg)
        {
            var url = $"https://dev.azure.com/{azDoOrg}";
            var httpResponseMessage = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            var redirectedUrl = httpResponseMessage.Headers.WwwAuthenticate.ToList().FirstOrDefault(i => i.Parameter.StartsWith("authorization_uri")).Parameter.Split('=', 2).LastOrDefault().Split('/').LastOrDefault();
            return redirectedUrl;
        }
    }
}
