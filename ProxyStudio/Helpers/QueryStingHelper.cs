using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProxyStudio
{
    internal static class QueryStringHelper
    {
        public static string BuildUrlWithQueryStringUsingStringConcat(string baseUrl, Dictionary<string, string> queryParams)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new ArgumentException("Base URL cannot be null or empty.", nameof(baseUrl));

            if (queryParams == null || queryParams.Count == 0)
                return baseUrl;

            var queryString = new StringBuilder("?");
            foreach (var param in queryParams)
            {
                queryString.Append($"{Uri.EscapeDataString(param.Key)}={Uri.EscapeDataString(param.Value)}&");
            }

            // Remove the trailing '&'  
            queryString.Length--;

            return baseUrl + queryString.ToString();
        }
    }
}