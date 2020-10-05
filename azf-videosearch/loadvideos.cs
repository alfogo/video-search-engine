using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using Google.Apis.YouTube.v3;
using Google.Apis.Services;
using Azure.AI.TextAnalytics;
using Azure;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace videosearchengine
{
    public static class loadvideos
    {
        // Cosmos DB settings for storing videos
        private static readonly string EndpointUri = Environment.GetEnvironmentVariable("cosmosdb_endpointuri");
        private static readonly string PrimaryKey = Environment.GetEnvironmentVariable("cosmosdb_key");
        
        // Azure Cognitive Service settings for language detection
        private static readonly AzureKeyCredential credentials = new AzureKeyCredential(Environment.GetEnvironmentVariable("cs_key"));
        private static readonly Uri endpoint = new Uri(Environment.GetEnvironmentVariable("cs_endpoint"));

        // API for getting the captions
        private static string captionsapi = Environment.GetEnvironmentVariable("azcaptionsapiendpoint");

        [FunctionName("loadvideos")]
        public static async Task Run([TimerTrigger("0 0 1 * * *"
        /*#if DEBUG
            , RunOnStartup=true
        #endif*/
        )]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"Loading videos from YouTube to Cosmos SQL: {DateTime.Now}");
            
            // Get CosmosDB client
            CosmosClient cosmosClient = new CosmosClient(EndpointUri, PrimaryKey);
            Container videosContainer = cosmosClient.GetContainer("videosearchengine","videos");
            Container captionsContainer = cosmosClient.GetContainer("videosearchengine","captions");

            // Get TextAnalytics client
            var clientTextAnalytics = new TextAnalyticsClient(endpoint, credentials);

            // Get YouTube client
            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = "AIzaSyAUlMhtMQDIziseW6MSVMipB2Ocy9XmIRg",
                ApplicationName = "videosearchengine"
            });

            var searchListRequest = youtubeService.Search.List("snippet");
            searchListRequest.Location = "40.4168,3.7038"; //Madrid location
            searchListRequest.LocationRadius = "100km";
            searchListRequest.Type = "video";
            searchListRequest.Order = SearchResource.ListRequest.OrderEnum.Relevance;
            
            // Values used within the loop
            HttpClient newClient = new HttpClient();
            HttpRequestMessage newRequest;
            HttpResponseMessage response;
            DetectedLanguage detectedLanguage;

            // Page results from YouTube
            string nextpage = "";

            do
            {
                var searchListResponse = await searchListRequest.ExecuteAsync();

                foreach (var searchResult in searchListResponse.Items)
                {      
                    detectedLanguage = clientTextAnalytics.DetectLanguage(searchResult.Snippet.Title);
                    
                    string videoId = searchResult.Id.VideoId;
                    string regionCode = detectedLanguage.Iso6391Name;

                    // Get captions for video
                    newRequest = new HttpRequestMessage(HttpMethod.Get, 
                                string.Format("{0}?videoid={1}&code={2}",
                                captionsapi, 
                                videoId, 
                                regionCode));

                    // Read Server Response
                    response = await newClient.SendAsync(newRequest);
                    var captions = await response.Content.ReadAsStringAsync();

                    // If we have retrieved captions
                    if (!string.IsNullOrEmpty(captions))
                    {
                        // Adding video to videos container if video doesn't exit
                        try
                        {
                            // Read the item to see if it exists
                            ItemResponse<YoutubeVideo> YoutubeVideoResponse = await videosContainer.ReadItemAsync<YoutubeVideo>(videoId, new PartitionKey(detectedLanguage.Iso6391Name));
                        }
                        catch(CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                        {
                            YoutubeVideo video = new YoutubeVideo(videoId, detectedLanguage.Iso6391Name, searchResult.Snippet.Title);
                            await videosContainer.CreateItemAsync<YoutubeVideo>(video);

                            //Adding captions to captions container
                            List<YoutubeVideoCaption> videocaptions = JsonConvert.DeserializeObject<List<YoutubeVideoCaption>>(captions);
                            foreach (YoutubeVideoCaption caption in videocaptions)
                            {
                                caption.id = videoId;
                                await captionsContainer.CreateItemAsync<YoutubeVideoCaption>(caption);
                            }
                        }
                    }
                }

                if(!string.IsNullOrEmpty(searchListResponse.NextPageToken))
                {
                    nextpage = searchListResponse.NextPageToken;
                    searchListRequest.PageToken = searchListResponse.NextPageToken;
                }

            } while (!string.IsNullOrEmpty(nextpage));
        }
    }

    public class YoutubeVideo
    {
        public YoutubeVideo(string videoid, string regioncode, string title)
        {
            this.id = videoid;
            this.regionCode = regioncode;
            this.title = title;
        }
        public string id {get; set;}
        public string regionCode {get; set;}
        public string title {get; set;}
    }

    public class YoutubeVideoCaption
    {
        public YoutubeVideoCaption(string id, float start, string text)
        {
            this.id = id;
            this.start = start;
            this.text = text;
        }
        public string id {get;set;}
        public float start {get; set;}
        public string text {get; set;}
    }     
}
