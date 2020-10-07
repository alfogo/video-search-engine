using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
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

namespace videosearchengine
{
    public static class loadvideos
    {
        // Cosmos DB settings for storing videos
        private static readonly string EndpointUri = Environment.GetEnvironmentVariable("cosmosdbEndpoint");
        private static readonly string PrimaryKey = Environment.GetEnvironmentVariable("cosmosdbKey");
        
        // Azure Cognitive Service settings for language detection
        private static readonly AzureKeyCredential credentials = new AzureKeyCredential(Environment.GetEnvironmentVariable("cognitiveServicesKey"));
        private static readonly Uri endpoint = new Uri(Environment.GetEnvironmentVariable("cognitiveServicesEndpoint"));

        // API for getting the captions
        private static string captionsapi = Environment.GetEnvironmentVariable("azCaptionsApiEndpoint");

        [FunctionName("loadvideos")]
        public static async Task Run([TimerTrigger("0 0 1 * * *"
        #if DEBUG
            , RunOnStartup=true
        #endif
        )]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"Loading videos from YouTube to Cosmos SQL: {DateTime.Now}");
            
            // Get CosmosDB client
            CosmosClientOptions options = new CosmosClientOptions() { AllowBulkExecution = true };
            CosmosClient cosmosClient = new CosmosClient(EndpointUri, PrimaryKey, options);
            Container captionsContainer = cosmosClient.GetContainer("videosearchengine","captions");

            // Get TextAnalytics client
            var clientTextAnalytics = new TextAnalyticsClient(endpoint, credentials);

            // Get YouTube client
            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = "AIzaSyCDV7ALRl4mGrU1yg3x68CUL7darmEP1Rg",
                ApplicationName = "test"
            });

            //Madrid -> "40.4165,-3.70256";
            // Random location in Spain
            Random rnd = new Random();
            string latitude = rnd.Next(36,43).ToString();
            string longitude = rnd.Next(-6, 3).ToString();
            
            var searchListRequest = youtubeService.Search.List("snippet");

            searchListRequest.Location = string.Format("{0},{1}", latitude, longitude);
            searchListRequest.LocationRadius = "500km";
            searchListRequest.Type = "video";
            searchListRequest.Order = SearchResource.ListRequest.OrderEnum.Relevance;
            
            // Values used within the loop
            HttpClient newClient = new HttpClient();
            HttpRequestMessage newRequest;
            HttpResponseMessage response;
            DetectedLanguage detectedLanguage;

            // Page results from YouTube
            string nextpage = "";

            List<Task> concurrentTasksCaptions = new List<Task>();

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

                    response = await newClient.SendAsync(newRequest);
                    var captions = await response.Content.ReadAsStringAsync();

                    // If captions are not empty
                    if (!string.IsNullOrEmpty(captions))
                    {
                        // Adding video to videos container if video doesn't exit
                        try
                        {
                            // Read the item to see if it exists
                            ItemResponse<YoutubeVideoCaption> YoutubeVideoResponse = await captionsContainer.ReadItemAsync<YoutubeVideoCaption>(videoId, new PartitionKey(detectedLanguage.Iso6391Name));
                        }
                        catch(CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                        {
                            log.LogInformation($"Storing video: "+ videoId);

                            List<YoutubeVideoCaption> videocaptions = JsonConvert.DeserializeObject<List<YoutubeVideoCaption>>(captions);

                            log.LogInformation($"Entering captions for video: "+ videoId);
                            foreach (YoutubeVideoCaption caption in videocaptions)
                            {
                                caption.id = videoId;
                                caption.regionCode = regionCode;
                                concurrentTasksCaptions.Add(captionsContainer.CreateItemAsync<YoutubeVideoCaption>(caption));
                            }
                            log.LogInformation($"Leaving captions for video: "+ videoId);

                            await Task.WhenAll(concurrentTasksCaptions);
                        }
                    }
                }

                await Task.WhenAll(concurrentTasksCaptions);

                log.LogInformation($"Loading next page: "+ searchListResponse.NextPageToken);
                
                if(!string.IsNullOrEmpty(searchListResponse.NextPageToken))
                {
                    nextpage = searchListResponse.NextPageToken;
                    searchListRequest.PageToken = searchListResponse.NextPageToken;
                }

            } while (!string.IsNullOrEmpty(nextpage));
        }
    }

    public class YoutubeVideoCaption
    {
        public YoutubeVideoCaption(string id, string regioncode, float start, string text)
        {
            this.id = id;
            this.regionCode = regioncode;
            this.start = start;
            this.text = text;
        }
        public string id {get;set;}
        public string regionCode {get; set;}
        public float start {get; set;}
        public string text {get; set;}
    }     
}
