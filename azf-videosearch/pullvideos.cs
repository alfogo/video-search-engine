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
using System.Net.Http;

using Google.Apis.YouTube.v3;
using Google.Apis.Services;

namespace videosearch.pullvideos
{
    public static class pullvideos
    {
        [FunctionName("pullvideos")]
        public static void Run([TimerTrigger("0 0 1 * * *")]TimerInfo myTimer, out object taskDocument, ILogger log)
        {
            log.LogInformation($"Pulling videos from YouTube to Cosmos DB: {DateTime.Now}");

            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = "AIzaSyAUlMhtMQDIziseW6MSVMipB2Ocy9XmIRg",
                ApplicationName = "videosearchengine"
            });

            var searchListRequest = youtubeService.Search.List("snippet");
            //searchListRequest.Q = "Madrid";
            //searchListRequest.MaxResults = 100;
            searchListRequest.Location = "40.4168,3.7038"; //Madrid location
            searchListRequest.LocationRadius = "1000km";
            searchListRequest.Type = "video";
            searchListRequest.Order = SearchResource.ListRequest.OrderEnum.Relevance;

            // Call the search.list method to retrieve results matching the specified query term.
            var searchListResponse = await searchListRequest.ExecuteAsync();

            HttpClient client = new HttpClient();

            foreach (var searchResult in searchListResponse.Items)
            {
                taskDocument = new
                {
                    searchResult.Id.VideoId,
                    searchResult.Snippet.Title
                };
            }

            return OkResult();
        }

        public class YoutubeVideo
        {
            public string Id {get; set;}
            public string Title {get; set;}
        }   
    }
}
