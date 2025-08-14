/*
ProxyStudio - A cross-platform proxy management application.
Copyright (C) 2025 James Ponter

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program. If not, see <https://www.gnu.org/licenses/>.
*/

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