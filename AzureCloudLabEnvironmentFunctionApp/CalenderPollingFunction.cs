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
        public async Task Run([TimerTrigger("0 */15 * * * *")] TimerInfo timer, ExecutionContext context,
            ILogger logger)
        {
            var config = Common.Config(context);

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
                var ev = new Event
                {
                    Title = newClass.PartitionKey,
                    StartTime = newClass.StartTime,
                    EndTime = newClass.EndTime,
                    Context = newClass.Context,
                    RepeatTimes = GetRepeatCount(newClass, tableClient),
                    Type = "START"
                };
                await startEventQueueClient.SendMessageAsync(Base64Encode(ev.ToJson()));
                SaveNewEvent(newClass, tableClient, logger);
            }

            foreach (var endedClass in endedEvents)
            {
                var ev = new Event
                {
                    Title = endedClass.PartitionKey,
                    StartTime = endedClass.StartTime,
                    EndTime = endedClass.EndTime,
                    Context = endedClass.Context,
                    RepeatTimes = GetRepeatCount(endedClass, tableClient),
                    Type = "END"
                };
                await endEventQueueClient.SendMessageAsync(Base64Encode(ev.ToJson()));
                DeleteEndedEvent(endedClass, tableClient, logger);
            }

            logger.LogInformation("onGoingEvents:" + onGoingEvents.Count);
            logger.LogInformation($"CalenderPollingFunction Timer trigger function executed at: {DateTime.Now}");
        }


        private static List<OnGoingEvent> GetOnGoingEvents(IGetOccurrences calendar, string calenderTimeZone,
            ILogger logger)
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

            string GetRowKey(string summary, DateTime startTime, DateTime endTime) => $"{summary} - From: {startTime.ToLocalTime()} To: {endTime.ToLocalTime()} TimeZone: {TimeZoneInfo.Local.StandardName}";

            foreach (var occurrence in occurrences)
            {
                var startTime = occurrence.Period.StartTime.AsUtc;
                var endTime = occurrence.Period.EndTime.AsUtc;

                string pk, rk, description;

                switch (occurrence.Source)
                {
                    case IRecurringComponent rc:
                        pk = rc.Summary;
                        rk = GetRowKey(rc.Summary, startTime, endTime);
                        description = rc.Description;
                        break;
                    case ICalendarComponent ev:
                        pk = ev.Properties["SUMMARY"].Value as string;
                        rk = GetRowKey(ev.Properties["SUMMARY"].Value as string, startTime, endTime);
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
            Pageable<OnGoingEvent> oDataQueryEntities = tableClient.Query<OnGoingEvent>(
                filter: TableClient.CreateQueryFilter($"PartitionKey eq {onGoingEvent.PartitionKey} and RowKey eq {onGoingEvent.RowKey}"));
            return !oDataQueryEntities.Any();
        }

        private static int GetRepeatCount(OnGoingEvent onGoingEvent, TableClient tableClient)
        {
            Pageable<OnGoingEvent> oDataQueryEntities =
                tableClient.Query<OnGoingEvent>(filter: TableClient.CreateQueryFilter($"PartitionKey eq {onGoingEvent.PartitionKey}"));
            return oDataQueryEntities.Count();
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