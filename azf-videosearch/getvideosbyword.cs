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

namespace videosearchengine
{
    public static class getvideosbyword
    {
        // Cosmos DB settings for storing videos
        private static readonly string EndpointUri = "https://videosearchengine.documents.azure.com:443/";
        private static readonly string PrimaryKey = "lx57DGO8isymXlvVIDJdEIN3qYfNvGhXRwBhTwySyJftV0fusW8aGrz0VAWG1qwdzedf8UeZuO2j196PrthTgQ==";

        [FunctionName("getvideosbyword")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            List<YoutubeVideo> videos = await QueryItemsAsync();

            return new OkObjectResult("ok");
        }

        private static async Task<List<YoutubeVideo>> QueryItemsAsync()
        {
            CosmosClient cosmosClient = new CosmosClient(EndpointUri, PrimaryKey);
            Container container = cosmosClient.GetContainer("videosearchengine","videos");

            var sqlQueryText = "SELECT * FROM c WHERE c.regioncode = 'en'";

            Console.WriteLine("Running query: {0}\n", sqlQueryText);

            QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);
            FeedIterator<YoutubeVideo> queryResultSetIterator = container.GetItemQueryIterator<YoutubeVideo>(queryDefinition);

            List<YoutubeVideo> videos = new List<YoutubeVideo>();

            while (queryResultSetIterator.HasMoreResults)
            {
                FeedResponse<YoutubeVideo> currentResultSet = await queryResultSetIterator.ReadNextAsync();
                foreach (YoutubeVideo video in currentResultSet)
                {
                    videos.Add(video);
                }
            }

            return videos;
        }

        public class YoutubeVideo
        {
            public YoutubeVideo(string id, string title, string regioncode)
            {
                this.id = id;
                this.title = title;
                this.regioncode = regioncode;
            }
            public string id {get; set;}
            public string title {get; set;}
            public string regioncode {get; set;}
        }   
    }
    
}
