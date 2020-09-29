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
using System.Net;
using System.Globalization;

using Google.Apis.YouTube.v3;
using Google.Apis.Services;
using Microsoft.Azure.Cosmos;
using Azure.AI.TextAnalytics;
using Azure;

namespace videosearch.pullvideos
{
    public static class pullvideos
    {
        // Cosmos DB settings for storing videos
        private static readonly string EndpointUri = "https://videosearchengine.documents.azure.com:443/";
        private static readonly string PrimaryKey = "lx57DGO8isymXlvVIDJdEIN3qYfNvGhXRwBhTwySyJftV0fusW8aGrz0VAWG1qwdzedf8UeZuO2j196PrthTgQ==";

        // Azure Cognitive Service settings for language detection
        private static readonly AzureKeyCredential credentials = new AzureKeyCredential("f7c1ca82e3344750bed69fc0eebb900c");
        private static readonly Uri endpoint = new Uri("https://westeurope.api.cognitive.microsoft.com/");

        [FunctionName("pullvideos")]
        public static async Task Run([TimerTrigger("0 0 1 * * *"
        #if DEBUG
            , RunOnStartup=true
        #endif
        )]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"Pulling videos from YouTube to Cosmos DB: {DateTime.Now}");

            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = "AIzaSyAUlMhtMQDIziseW6MSVMipB2Ocy9XmIRg",
                ApplicationName = "videosearchengine"
            });

            var client = new TextAnalyticsClient(endpoint, credentials);

            CosmosClient cosmosClient = new CosmosClient(EndpointUri, PrimaryKey);
            Container container = cosmosClient.GetContainer("videosearchengine","videos");

            var searchListRequest = youtubeService.Search.List("snippet");
            searchListRequest.Location = "40.4168,3.7038"; //Madrid location
            searchListRequest.LocationRadius = "1000km";
            searchListRequest.Type = "video";
            searchListRequest.Order = SearchResource.ListRequest.OrderEnum.Relevance;

            // Call the search.list method to retrieve results matching the specified query term.
            var searchListResponse = await searchListRequest.ExecuteAsync();

            foreach (var searchResult in searchListResponse.Items)
            {      
                DetectedLanguage detectedLanguage = client.DetectLanguage(searchResult.Snippet.Title);

                YoutubeVideo video = new YoutubeVideo(searchResult.Id.VideoId, searchResult.Snippet.Title, detectedLanguage.Iso6391Name);

                try
                {
                    // Read the item to see if it exists
                    ItemResponse<YoutubeVideo> YoutubeVideoResponse = await container.ReadItemAsync<YoutubeVideo>(video.id, new PartitionKey(detectedLanguage.Iso6391Name));
                }
                catch(CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    await container.CreateItemAsync<YoutubeVideo>(video, new PartitionKey(detectedLanguage.Iso6391Name));
                }
            }

            while (!string.IsNullOrEmpty(searchListResponse.NextPageToken))
            {
                searchListRequest.PageToken = searchListResponse.NextPageToken;
                searchListResponse = await searchListRequest.ExecuteAsync();

                foreach (var searchResult in searchListResponse.Items)
                {      
                    DetectedLanguage detectedLanguage = client.DetectLanguage(searchResult.Snippet.Title);

                    YoutubeVideo video = new YoutubeVideo(searchResult.Id.VideoId, searchResult.Snippet.Title, detectedLanguage.Iso6391Name);
                    
                    try
                    {
                        // Read the item to see if it exists
                        ItemResponse<YoutubeVideo> YoutubeVideoResponse = await container.ReadItemAsync<YoutubeVideo>(video.id, new PartitionKey(detectedLanguage.Iso6391Name));
                    }
                    catch(CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                    {
                        await container.CreateItemAsync<YoutubeVideo>(video, new PartitionKey(detectedLanguage.Iso6391Name));
                    }
                }
            }
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
