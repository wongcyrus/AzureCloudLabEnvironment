using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Text.RegularExpressions;
using Azure;
using Azure.Storage.Queues;
using AzureCloudLabEnvironment.Dao;
using AzureCloudLabEnvironment.Helper;
using AzureCloudLabEnvironment.Model;

namespace AzureCloudLabEnvironment
{
    public class CalenderPollingFunction
    {
        [FunctionName(nameof(CalenderPollingFunction))]
        // ReSharper disable once UnusedMember.Global
        public async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo timer, ExecutionContext context,
            ILogger logger)
        {
            if (timer.IsPastDue)
            {
                logger.LogInformation("Skip for past due.");
                return;
            }

            var config = new Config(context);
            
            var calendar = await LoadFromUriAsync(new Uri(config.GetConfig(Config.Key.CalendarUrl)));
            var onGoingEvents = GetOnGoingEvents(calendar, config.GetConfig(Config.Key.CalendarTimeZone), logger);

            var onGoingEventDao = new OnGoingEventDao(config, logger);
            var completedEventDao = new CompletedEventDao(config, logger);

            var newEvents = onGoingEvents.Where(c => onGoingEventDao.IsNew(c)).ToList();
            var endedEvents = onGoingEventDao.GetEndedEvents();

            var startEventQueueClient = new QueueClient(config.GetConfig(Config.Key.AzureWebJobsStorage), "start-event");
            var endEventQueueClient = new QueueClient(config.GetConfig(Config.Key.AzureWebJobsStorage), "end-event");

            string Base64Encode(string plainText)
            {
                var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
                return Convert.ToBase64String(plainTextBytes);
            }

            foreach (var newEvent in newEvents)
            {
                logger.LogInformation("newClass:" + newEvent);
                var ev = new Event
                {
                    Title = newEvent.PartitionKey,
                    StartTime = newEvent.StartTime,
                    EndTime = newEvent.EndTime,
                    Context = newEvent.Context,
                    RepeatTimes = completedEventDao.GetRepeatCount(newEvent.PartitionKey),
                    Type = "START"
                };
                await startEventQueueClient.SendMessageAsync(Base64Encode(ev.ToJson()));
                onGoingEventDao.Upsert(newEvent);
            }

            foreach (var endedEvent in endedEvents)
            {
                logger.LogInformation("endedClass:" + endedEvent);
                var ev = new Event
                {
                    Title = endedEvent.PartitionKey,
                    StartTime = endedEvent.StartTime,
                    EndTime = endedEvent.EndTime,
                    Context = endedEvent.Context,
                    RepeatTimes = completedEventDao.GetRepeatCount(endedEvent.PartitionKey),
                    Type = "END"
                };
                await endEventQueueClient.SendMessageAsync(Base64Encode(ev.ToJson()));
                onGoingEventDao.Delete(endedEvent);

                var completedEvent = new CompletedEvent()
                {
                    Context = endedEvent.Context,
                    ETag = ETag.All,
                    EndTime = endedEvent.EndTime,
                    PartitionKey = endedEvent.PartitionKey,
                    RowKey = endedEvent.RowKey,
                    StartTime = endedEvent.StartTime,
                    Timestamp = endedEvent.Timestamp
                };

                completedEventDao.Upsert(completedEvent);
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
            var occurrences = calendar.GetOccurrences(startUtc, endUtc);

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
                Debug.Assert(pk != null, nameof(pk) + " != null");
                pk = Regex.Replace(pk, @"[^0-9a-zA-Z]+", ",");
                rk = Regex.Replace(rk, @"[^0-9a-zA-Z]+", ",");
                onGoingEvents.Add(new OnGoingEvent()
                {
                    PartitionKey = pk,
                    RowKey = rk,
                    StartTime = startTime,
                    EndTime = endTime,
                    Context = description,
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
    }
}