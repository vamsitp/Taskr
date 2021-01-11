﻿namespace Taskr
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;

    using Newtonsoft.Json.Linq;

    public static class Extensions
    {
        internal const int MaxSize = 200;
        private static readonly HttpClient Client = new HttpClient(new HttpClientHandler() { AllowAutoRedirect = false });

        public static Dictionary<string, string> Flatten(this object item)
        {
            var jsonObject = JObject.FromObject(item);
            var tokens = jsonObject.Descendants().Where(p => !p.Any());
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

        public static bool ContainsIgnoreCase(this IEnumerable<string> items, string subString)
        {
            return items?.Contains(subString, StringComparer.OrdinalIgnoreCase) == true;
        }

        public static bool EqualsIgnoreCase(this string item1, string item2)
        {
            return item1?.Equals(item2, StringComparison.OrdinalIgnoreCase) == true;
        }

        public static List<List<T>> ChunkBy<T>(this IEnumerable<T> source, int chunkSize = MaxSize)
        {
            return source
            .Select((x, i) => new { Index = i, Value = x })
            .GroupBy(x => x.Index / chunkSize)
            .Select(x => x.Select(v => v.Value).ToList())
            .ToList();
        }

        public static async Task<string> GetTenantId(this string azDoOrg)
        {
            var url = $"https://dev.azure.com/{azDoOrg}";
            var httpResponseMessage = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            var redirectedUrl = httpResponseMessage.Headers.WwwAuthenticate.AsEnumerable().FirstOrDefault(i => i.Parameter.StartsWith("authorization_uri")).Parameter.Split('=', 2).LastOrDefault().Split('/').LastOrDefault();
            return redirectedUrl;
        }
    }
}
