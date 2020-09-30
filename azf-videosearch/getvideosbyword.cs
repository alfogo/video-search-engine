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

using System.Net.Http;

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
            string words = req.Query["words"];
            string language = req.Query["language"];

            if (words != null && words.Length > 3)
            {
                List<YoutubeVideo> videos = await QueryItemsAsync(language);
                
                List<string> urls = new List<string>();

                try
                {
                    HttpClient newClient = new HttpClient();

                    foreach (YoutubeVideo video in videos)
                    {
                        Console.WriteLine("Searching on video" + video.id);

                        HttpRequestMessage newRequest = new HttpRequestMessage(HttpMethod.Get, 
                            string.Format("https://youtubevideosearchpy.azurewebsites.net/api/captions?videoid={0}&code={1}", video.id, language));

                        //Read Server Response
                        HttpResponseMessage response = await newClient.SendAsync(newRequest);
                        var jsonString = await response.Content.ReadAsStringAsync();

                        if (!string.IsNullOrEmpty(jsonString))
                        {
                            List<VideoInfo> infovideo = JsonConvert.DeserializeObject<List<VideoInfo>>(jsonString);

                            List<VideoInfo> filteredlist = infovideo.FindAll(v => v.text.Contains(words));

                            foreach (VideoInfo vi in filteredlist)
                            {
                                urls.Add(string.Format("https://www.youtube.com/watch?v={0}&t={1}", video.id, Math.Round(vi.start)));
                            }
                        }
                        else
                        {
                            Console.WriteLine("Video " + video.id + " with no captions enabled");
                        }
                    }

                    return new OkObjectResult(urls);

                }
                catch (Exception ex)
                {
                    return new OkObjectResult(ex.Message);
                }
            }

            

            return new OkObjectResult("ok");
        }

        private static async Task<List<YoutubeVideo>> QueryItemsAsync(string language)
        {
            CosmosClient cosmosClient = new CosmosClient(EndpointUri, PrimaryKey);
            Container container = cosmosClient.GetContainer("videosearchengine","videos");

            var sqlQueryText = "SELECT * FROM c WHERE c.regioncode = '" + language + "'";

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

        public class VideoInfo
        {
            public string text {get; set;}
            public decimal start {get; set;}
        }   
    }
    
}
