using AzureCloudLabEnvironment.Model;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace AzureCloudLabEnvironment
{
    public class EndLabEventFunction
    {
        [FunctionName(nameof(EndLabEventFunction))]
        public void Run([QueueTrigger("end-event", Connection = "AzureWebJobsStorage")] Event ev, ILogger log)
        {
            Lab lab = Lab.FromJson(ev.Context);
            log.LogInformation($"EndLabEventFunction Queue trigger function processed: {ev} => {lab}");
            if (lab == null) return;
            lab.RepeatTimes = ev.RepeatTimes;
            log.LogInformation($"Start the lab: {lab}");
        }
    }
}