using AzureCloudLabEnvironment.Model;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace AzureCloudLabEnvironment
{
    public class StartLabEventFunction
    {
        [FunctionName(nameof(StartLabEventFunction))]
        public void Run([QueueTrigger("start-event", Connection = "AzureWebJobsStorage")] Lab lab, ILogger log)
        {
            log.LogInformation($"StartLabEventFunction Queue trigger function processed: {lab}");
        }
    }
}
