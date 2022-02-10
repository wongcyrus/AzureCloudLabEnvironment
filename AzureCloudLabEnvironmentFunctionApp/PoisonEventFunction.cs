using System;
using System.Globalization;
using AzureCloudLabEnvironment.Dao;
using AzureCloudLabEnvironment.Helper;
using AzureCloudLabEnvironment.Model;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace AzureCloudLabEnvironment
{
    public class PoisonEventFunction
    {
        [FunctionName(nameof(EndEventPoisonEventFunction))]
        public void EndEventPoisonEventFunction([QueueTrigger("end-event-poison", Connection = nameof(Config.Key.AzureWebJobsStorage))] string message, ILogger log, ExecutionContext context)
        {
            var source = "end-event-poison";
            ProcessPoisonEvent(message, log, context, source);
        }

        [FunctionName(nameof(StartEventPoisonEventFunction))]
        public void StartEventPoisonEventFunction([QueueTrigger("start-event-poison", Connection = nameof(Config.Key.AzureWebJobsStorage))] string message, ILogger log, ExecutionContext context)
        {
            var source = "start-event-poison";
            ProcessPoisonEvent(message, log, context, source);
        }
        private static void ProcessPoisonEvent(string message, ILogger log, ExecutionContext context, string source)
        {
            var config = new Config(context);
            var emailMessage = new EmailMessage
            {
                To = config.GetConfig(Config.Key.AdminEmail),
                Subject = $"Alert for {source}",
                Body = message
            };
            var emailClient = new Email(config, log);
            emailClient.Send(emailMessage, null);
            log.LogInformation($"{source} Queue trigger function processed: {message}");

            var errorLogDao = new ErrorLogDao(config, log);


            errorLogDao.Add(new ErrorLog()
            {
                PartitionKey = source,
                RowKey = DateTime.Now.ToString(CultureInfo.InvariantCulture),
                Message = message,
                Source = source
            });
        }
    }
}
