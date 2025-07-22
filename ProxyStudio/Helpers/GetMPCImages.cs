using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Metsys.Bson;

namespace ProxyStudio.Helpers;

public class GetMPCImages
{
    public async Task<byte[]> GetImageFromMPCFill(string id, HttpClient httpClient)
        {
            string url = "https://script.google.com/macros/s/AKfycbw8laScKBfxda2Wb0g63gkYDBdy8NWNxINoC4xDOwnCQ3JMFdruam1MdmNmN4wI5k4/exec";
            HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient), "HttpClient cannot be null.");


            //set up the URL with the query parameters
            var queryParams = new Dictionary<string, string>
            {
                { "id", id }
            };

            string fullUrl = QueryStringHelper.BuildUrlWithQueryStringUsingStringConcat(url, queryParams);

            DebugHelper.WriteDebug("Full URL: " + fullUrl);

            string tempImageString = await ReadStringFromUriAsync(fullUrl, _httpClient);
            DebugHelper.WriteDebug("Image data received: ");



            
            byte[] imageBytes = Convert.FromBase64String(tempImageString);
            DebugHelper.WriteDebug("Image bytes converted from base64 string.");
            return imageBytes;

        }

        public async Task<string> ReadStringFromUriAsync(string url, HttpClient httpClient)
        {
            HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient), "HttpClient cannot be null.");

            DebugHelper.WriteDebug($"Entering Task HTTPClientKludge({url}...");
            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException("You must supply a url to interrogate for this function to work.");

            Uri uri;
            DebugHelper.WriteDebug($"Attempting to create Uri from {url}...");
            // Attempt to create a Uri from the provided URL.
            try
            {
                string response = await _httpClient.GetStringAsync(url);
                return response;
            }
            catch (HttpRequestException e)
            {
                DebugHelper.WriteDebug($"HttpRequestException: {e.Message}");
                throw new InvalidOperationException("An error occurred while making the HTTP request.", e);
            }
            catch (System.Exception e)
            {
                DebugHelper.WriteDebug($"Exception: {e.Message}");
                throw new InvalidOperationException("An unexpected error occurred.", e);
            }

        }
}