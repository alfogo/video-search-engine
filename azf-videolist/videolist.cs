using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

using Google.Apis.YouTube.v3;
using Google.Apis.Services;

namespace youtubevideosearch.videolist
{
    public static class videolist
    {
        [FunctionName("videolist")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = "AIzaSyAUlMhtMQDIziseW6MSVMipB2Ocy9XmIRg",
                ApplicationName = "youtubevideosearchcore"
            });

            var searchListRequest = youtubeService.Search.List("snippet");
            searchListRequest.Q = "Madrid";
            searchListRequest.MaxResults = 100;
            searchListRequest.Location = "40.4168,3.7038";
            searchListRequest.LocationRadius = "30km";
            searchListRequest.Type = "video";
            //searchListRequest.Order = SearchResource.ListRequest.OrderEnum.Relevance;

            // Call the search.list method to retrieve results matching the specified query term.
            var searchListResponse = await searchListRequest.ExecuteAsync();

            List<YoutubeVideo> videos = new List<YoutubeVideo>();
            // Add each result to the appropriate list, and then display the lists of
            // matching videos, channels, and playlists.
            foreach (var searchResult in searchListResponse.Items)
            {
                YoutubeVideo video = new YoutubeVideo
                {
                    Id = searchResult.Id.VideoId, 
                    Title = searchResult.Snippet.Title
                };
                videos.Add(video);
            }

            return new OkObjectResult(videos);
        }

        public class YoutubeVideo
        {
            public string Id {get; set;}
            public string Title {get; set;}
        }    
    }
}