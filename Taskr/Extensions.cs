namespace Taskr
{
    using System.Collections.Generic;
    using System.Linq;

    using Newtonsoft.Json.Linq;

    public static class Extensions
    {
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
    }
}
