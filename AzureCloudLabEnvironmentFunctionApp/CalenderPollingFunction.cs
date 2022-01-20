using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Text.RegularExpressions;
using Azure;
using Azure.Storage.Queues;
using AzureCloudLabEnvironment.Model;
using Microsoft.Extensions.Configuration;

namespace AzureCloudLabEnvironment
{
    public class CalenderPollingFunction
    {
        [FunctionName(nameof(CalenderPollingFunction))]
        public async Task Run([TimerTrigger("0 */15 * * * *")] TimerInfo timer, ExecutionContext context, ILogger logger)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var calendar = await CalenderPollingFunction.LoadFromUriAsync(new Uri(config["CalendarUrl"]));
            var onGoingEvents = GetOnGoingEvents(calendar, config["CalendarTimeZone"], logger);

            var tableClient = GetTableClient(config);
            var newEvents = onGoingEvents.Where(c => IsNew(c, tableClient)).ToList();
            var endedEvents = GetEndedEvents(tableClient);

            var startEventQueueClient = new QueueClient(config["AzureWebJobsStorage"], "start-event");
            var endEventQueueClient = new QueueClient(config["AzureWebJobsStorage"], "end-event");

            string Base64Encode(string plainText)
            {
                var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
                return System.Convert.ToBase64String(plainTextBytes);
            }

            foreach (var newClass in newEvents)
            {
                await startEventQueueClient.SendMessageAsync(Base64Encode(newClass.Context.Trim()));
                SaveNewEvent(newClass, tableClient, logger);
            }

            foreach (var endedClass in endedEvents)
            {
                await endEventQueueClient.SendMessageAsync(Base64Encode(endedClass.Context.Trim()));
                DeleteEndedEvent(endedClass, tableClient, logger);
            }

            logger.LogInformation("onGoingEvents:" + onGoingEvents.Count);
            logger.LogInformation($"CalenderPollingFunction Timer trigger function executed at: {DateTime.Now}");
        }


        private static List<OnGoingEvent> GetOnGoingEvents(IGetOccurrences calendar, string calenderTimeZone, ILogger logger)
        {
            const double threshold = 0.5;

            var onGoingEvents = new List<OnGoingEvent>();
            var start = TimeZoneInfo.ConvertTime(DateTime.Now.AddMinutes(-threshold),
                TimeZoneInfo.FindSystemTimeZoneById(calenderTimeZone));
            var end = TimeZoneInfo.ConvertTime(DateTime.Now.AddMinutes(threshold),
                TimeZoneInfo.FindSystemTimeZoneById(calenderTimeZone));
            var startUtc = DateTime.UtcNow.AddMinutes(-threshold);
            var endUtc = DateTime.UtcNow.AddMinutes(threshold);
            var occurrencesRepeatedEvents = calendar.GetOccurrences(startUtc, endUtc);
            var occurrencesSingleEvents = calendar.GetOccurrences(start, end);

            var occurrences = new List<Occurrence>();
            occurrences.AddRange(occurrencesRepeatedEvents);
            occurrences.AddRange(occurrencesSingleEvents);

            string GetPk(string summary, DateTime startTime, DateTime endTime)
            {
                return summary + " - From: " + startTime + " To: " + endTime;
            }
            string GetRk(string summary, DateTime startTime, DateTime endTime)
            {
                return $"{summary} - From: {startTime.ToLocalTime() } To: {endTime.ToLocalTime()} TimeZone: {TimeZoneInfo.Local.StandardName}";
            }

            foreach (var occurrence in occurrences)
            {
                var startTime = occurrence.Period.StartTime.AsUtc;
                var endTime = occurrence.Period.EndTime.AsUtc;

                string pk, rk, description;

                switch (occurrence.Source)
                {
                    case IRecurringComponent rc:
                        pk = GetPk(rc.Summary, startTime, endTime);
                        rk = GetRk(rc.Summary, startTime, endTime);
                        description = rc.Description;
                        break;
                    case ICalendarComponent ev:
                        pk = GetPk(ev.Properties["SUMMARY"].Value as string, startTime, endTime);
                        rk = GetRk(ev.Properties["SUMMARY"].Value as string, startTime, endTime);
                        description = ev.Properties["DESCRIPTION"].Value as string;
                        break;
                    default:
                        continue;
                }

                logger.LogInformation(pk + description);
                pk = Regex.Replace(pk, @"[^0-9a-zA-Z]+", ",");
                rk = Regex.Replace(rk, @"[^0-9a-zA-Z]+", ",");
                onGoingEvents.Add(new OnGoingEvent()
                {
                    PartitionKey = pk,
                    RowKey = rk,
                    StartTime = startTime,
                    EndTime = endTime,
                    Context = description
                });
            }

            return onGoingEvents;
        }

        private static async Task<Calendar> LoadFromUriAsync(Uri uri)
        {
            using var client = new HttpClient();
            using var response = await client.GetAsync(uri);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsStringAsync();
            return Calendar.Load(result);
        }

        private static bool IsNew(OnGoingEvent onGoingEvent, TableClient tableClient)
        {
            Pageable<OnGoingEvent> oDataQueryEntities = tableClient.Query<OnGoingEvent>(filter: TableClient.CreateQueryFilter($"PartitionKey eq {onGoingEvent.PartitionKey}"));
            return !oDataQueryEntities.Any();
        }

        private static List<OnGoingEvent> GetEndedEvents(TableClient tableClient)
        {
            Pageable<OnGoingEvent> oDataQueryEntities = tableClient.Query<OnGoingEvent>(c => c.EndTime < DateTime.UtcNow);
            return oDataQueryEntities.ToList();
        }

        private static void SaveNewEvent(OnGoingEvent onGoingEvent, TableClient tableClient, ILogger logger)
        {
            tableClient.AddEntity(onGoingEvent);
            logger.LogInformation("Saved " + onGoingEvent);
        }

        private static TableClient GetTableClient(IConfigurationRoot config)
        {
            var connectionString = config["AzureWebJobsStorage"];
            var tableClient = new TableClient(
                connectionString,
                nameof(OnGoingEvent));
            return tableClient;
        }

        private static void DeleteEndedEvent(OnGoingEvent onGoingEvent, TableClient tableClient, ILogger logger)
        {
            tableClient.DeleteEntity(onGoingEvent.PartitionKey, onGoingEvent.RowKey);
            logger.LogInformation("Deleted " + onGoingEvent);
        }
    }
}
