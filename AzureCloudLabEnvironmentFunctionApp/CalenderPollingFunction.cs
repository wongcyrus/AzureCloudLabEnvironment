using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Text.RegularExpressions;
using Azure.Storage.Queues;
using AzureCloudLabEnvironment.Dao;
using AzureCloudLabEnvironment.Model;

namespace AzureCloudLabEnvironment
{
    public class CalenderPollingFunction
    {
        [FunctionName(nameof(CalenderPollingFunction))]
        public async Task Run([TimerTrigger("0 */1 * * * *")] TimerInfo timer, ExecutionContext context,
            ILogger logger)
        {
            var config = Common.Config(context);

            var calendar = await CalenderPollingFunction.LoadFromUriAsync(new Uri(config["CalendarUrl"]));
            var onGoingEvents = GetOnGoingEvents(calendar, config["CalendarTimeZone"], logger);

            var onGoingEventDao = new OnGoingEventDao(config, logger);

            var newEvents = onGoingEvents.Where(c => onGoingEventDao.IsNew(c)).ToList();
            var endedEvents = onGoingEventDao.GetEndedEvents();

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
                    RepeatTimes = onGoingEventDao.GetRepeatCount(newClass),
                    Type = "START"
                };
                await startEventQueueClient.SendMessageAsync(Base64Encode(ev.ToJson()));
                onGoingEventDao.Add(newClass);
            }

            foreach (var endedClass in endedEvents)
            {
                var ev = new Event
                {
                    Title = endedClass.PartitionKey,
                    StartTime = endedClass.StartTime,
                    EndTime = endedClass.EndTime,
                    Context = endedClass.Context,
                    RepeatTimes = onGoingEventDao.GetRepeatCount(endedClass),
                    Type = "END"
                };
                await endEventQueueClient.SendMessageAsync(Base64Encode(ev.ToJson()));
                onGoingEventDao.Delete(endedClass);
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
    }
}