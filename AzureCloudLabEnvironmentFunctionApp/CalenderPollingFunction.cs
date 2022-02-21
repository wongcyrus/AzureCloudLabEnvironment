using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Queues;
using AzureCloudLabEnvironment.Dao;
using AzureCloudLabEnvironment.Helper;
using AzureCloudLabEnvironment.Model;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace AzureCloudLabEnvironment;

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
        var onGoingEvents = GetOnGoingEvents(calendar, logger);

        var onGoingEventDao = new OnGoingEventDao(config, logger);
        var completedEventDao = new CompletedEventDao(config, logger);

        var newEvents = onGoingEvents.Where(c => onGoingEventDao.IsNew(c)).ToList();
        var endedEvents = onGoingEventDao.GetEndedEvents();

        var startEventQueueClient = new QueueClient(config.GetConfig(Config.Key.AzureWebJobsStorage), "start-event");
        var endEventQueueClient = new QueueClient(config.GetConfig(Config.Key.AzureWebJobsStorage), "end-event");

        string Base64Encode(string plainText)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
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
                Location = newEvent.Location,
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
                Location = endedEvent.Location,
                RepeatTimes = completedEventDao.GetRepeatCount(endedEvent.PartitionKey),
                Type = "END"
            };
            await endEventQueueClient.SendMessageAsync(Base64Encode(ev.ToJson()));
            onGoingEventDao.Delete(endedEvent);

            var completedEvent = new CompletedEvent
            {
                Context = endedEvent.Context,
                Location = endedEvent.Location,
                EndTime = endedEvent.EndTime,
                PartitionKey = endedEvent.PartitionKey,
                RowKey = endedEvent.RowKey,
                StartTime = endedEvent.StartTime,
                Timestamp = endedEvent.Timestamp,
                ETag = ETag.All,
            };

            completedEventDao.Upsert(completedEvent);
        }

        logger.LogInformation("onGoingEvents:" + onGoingEvents.Count);
        logger.LogInformation($"CalenderPollingFunction Timer trigger function executed at: {DateTime.Now}");
    }


    private static List<OnGoingEvent> GetOnGoingEvents(IGetOccurrences calendar, ILogger logger)
    {
        const double threshold = 1;

        var onGoingEvents = new List<OnGoingEvent>();

        var now = DateTime.Now;
        var start = now.AddMinutes(-threshold);
        var end = now.AddMinutes(threshold);


        var todayOccurrences = calendar.GetOccurrences(start.AddHours(-12), start.AddHours(+12));

        var eventsInPeriod = todayOccurrences.Select(c =>
            new
            {
                Event = c,
                IsAfterStart = new CalDateTime(c.Period.StartTime) > new CalDateTime(start),
                IsBeforeEnd = new CalDateTime(c.Period.StartTime) < new CalDateTime(end)
            }).ToArray();

        logger.LogInformation("Events between " + start.AddHours(-12) + "and " + start.AddHours(+12));
        foreach (var e in eventsInPeriod)
            logger.LogInformation(e.Event.Period.StartTime.AsUtc + "(IsAfterStart:" + e.IsAfterStart + ",IsBeforeEnd:" + e.IsBeforeEnd + ")");

        var occurrences = eventsInPeriod.Where(c => c.IsAfterStart && c.IsBeforeEnd).Select(c => c.Event);

        string GetRowKey(string summary, DateTime startTime, DateTime endTime)
        {
            return
                $"{summary} - From: {startTime.ToLocalTime()} To: {endTime.ToLocalTime()} TimeZone: {TimeZoneInfo.Local.StandardName}";
        }

        foreach (var occurrence in occurrences)
        {
            var startTime = occurrence.Period.StartTime.AsUtc;
            var endTime = occurrence.Period.EndTime.AsUtc;

            string pk, rk, description,location;

            switch (occurrence.Source)
            {
                case IRecurringComponent rc:
                    pk = rc.Summary;
                    rk = GetRowKey(rc.Summary, startTime, endTime);
                    description = rc.Description;
                    location = rc.Properties.Get<string>("LOCATION");
                    break;
                case ICalendarComponent ev:
                    pk = ev.Properties["SUMMARY"].Value as string;
                    rk = GetRowKey(ev.Properties["SUMMARY"].Value as string, startTime, endTime);
                    description = ev.Properties["DESCRIPTION"].Value as string;
                    location = ev.Properties["LOCATION"].Value as string;
                    break;
                default:
                    continue;
            }

            logger.LogInformation(pk + description);
            Debug.Assert(pk != null, nameof(pk) + " != null");
            pk = Regex.Replace(pk, @"[^0-9a-zA-Z]+", ",");
            rk = Regex.Replace(rk, @"[^0-9a-zA-Z]+", ",");
            onGoingEvents.Add(new OnGoingEvent
            {
                PartitionKey = pk,
                RowKey = rk,
                StartTime = startTime,
                EndTime = endTime,
                Context = description,
                Location = location
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