using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Globalization;

namespace youtubevideosearch.videosearch
{
    public static class videosearch
    {
        [FunctionName("videosearch")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            string words = req.Query["words"];
            string code = req.Query["code"];

            if (String.IsNullOrEmpty(words) || (words.Length < 4))
                return new BadRequestObjectResult("Provide some words to search");

            try {
                RegionInfo info = new RegionInfo(code);
            }
            catch (ArgumentException ex) {
                return new BadRequestObjectResult(ex.Message);
            }


            
            return new OkObjectResult("ok");
        }
    }
}
