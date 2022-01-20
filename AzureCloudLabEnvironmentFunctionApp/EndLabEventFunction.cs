using AzureCloudLabEnvironment.Model;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace AzureCloudLabEnvironment
{
    public class EndLabEventFunction
    {
        [FunctionName(nameof(EndLabEventFunction))]
        public void Run([QueueTrigger("start-event", Connection = "AzureWebJobsStorage")] Lab lab, ILogger log)
        {
            log.LogInformation($"EndLabEventFunction Queue trigger function processed: {lab}");
        }
    }
}