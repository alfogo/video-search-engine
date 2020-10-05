using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Cosmos;
using System.Collections.Generic;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;

using System.Net.Http;

namespace videosearchengine
{
    public static class getvideosbyword
    {
        [FunctionName("getvideosbyword")]
        public static async Task<List<string>> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            string words = req.Query["words"];

            List<string> urls = new List<string>();

            if (words != null && words.Length > 3)
            {
                SearchServiceClient serviceClient = CreateSearchServiceClient();

                string indexName = Environment.GetEnvironmentVariable("SearchIndexName");

                ISearchIndexClient indexClient = serviceClient.Indexes.GetClient(indexName);
                SearchParameters parameters;
                DocumentSearchResult<YoutubeVideoCaption> results;

                parameters = new SearchParameters();
                results = indexClient.Documents.Search<YoutubeVideoCaption>(words, parameters);
                    
                foreach (SearchResult<YoutubeVideoCaption> caption in results.Results)
                {
                    urls.Add(string.Format("https://www.youtube.com/watch?v={0}&t={1}", caption.Document.id, Math.Round(caption.Document.start)));
                }
            }

        
            return urls;;
        }

        // Create the search service client
        private static SearchServiceClient CreateSearchServiceClient()
        {
            string searchServiceName = Environment.GetEnvironmentVariable("SearchServiceName");
            string adminApiKey = Environment.GetEnvironmentVariable("SearchServiceAdminApiKey");

            SearchServiceClient serviceClient = new SearchServiceClient(searchServiceName, new SearchCredentials(adminApiKey));
            
            return serviceClient;
        }

        public class YoutubeVideoCaption
        {
            [IsSearchable]
            [JsonProperty("id")]
            public string id {get;set;}

            [IsSearchable]
            [JsonProperty("start")]
            public float start {get; set;}

            [IsSearchable]
            [JsonProperty("text")]
            public string text {get; set;}
        }
  
    }
    
}
