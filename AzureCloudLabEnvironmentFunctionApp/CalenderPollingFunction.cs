using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Text.RegularExpressions;
using Azure;
using AzureCloudLabEnvironment.Model;
using Microsoft.Extensions.Configuration;

namespace AzureCloudLabEnvironment
{
    public class CalenderPollingFunction
    {
        [FunctionName(nameof(CalenderPollingFunction))]
        public async Task Run([TimerTrigger("0 */1 * * * *")] TimerInfo myTimer, ExecutionContext context, ILogger log)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var calendarSerializer = new CalendarSerializer();
            //calendarSerializer
            var url = new Uri("https://calendar.google.com/calendar/ical/ai28g3ticnn156spsve4k2t4fk%40group.calendar.google.com/private-a983a09afc269499a9bb6439563d03ce/basic.ics");
            var calendar = await CalenderPollingFunction.LoadFromUriAsync(url);
            var onGoingEvents = GetOnGoingEvents(log, calendar);

            var tableClient = GetTableClient(config);
            var newEvents = onGoingEvents.Where(c => IsNew(c, tableClient)).ToList();
            var endedEvents = GetEndedEvents(tableClient);

            foreach (var newClass in newEvents) SaveNewEvent(newClass, tableClient);
            foreach (var endedClass in endedEvents) DeleteEndedEvent(endedClass, tableClient);

            log.LogInformation("onGoingEvents:" + onGoingEvents.Count.ToString());
            log.LogInformation($"CalenderPollingFunction Timer trigger function executed at: {DateTime.Now}");
        }


        private static List<OnGoingEvent> GetOnGoingEvents(ILogger log, IGetOccurrences calendar)
        {
            const double threshold = 0.5;

            var onGoingEvents = new List<OnGoingEvent>();
            var start = TimeZoneInfo.ConvertTime(DateTime.Now.AddMinutes(-threshold),
                TimeZoneInfo.FindSystemTimeZoneById("China Standard Time"));
            var end = TimeZoneInfo.ConvertTime(DateTime.Now.AddMinutes(threshold),
                TimeZoneInfo.FindSystemTimeZoneById("China Standard Time"));
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

                log.LogInformation(pk + description);
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

        private static void SaveNewEvent(OnGoingEvent onGoingEvent, TableClient tableClient)
        {
            tableClient.AddEntity(onGoingEvent);
            Console.WriteLine("Saved " + onGoingEvent);
        }

        private static TableClient GetTableClient(IConfigurationRoot config)
        {
            var connectionString = config["AzureWebJobsStorage"];
            var tableClient = new TableClient(
                connectionString,
                nameof(OnGoingEvent));
            return tableClient;
        }

        private static void DeleteEndedEvent(OnGoingEvent onGoingEvent, TableClient tableClient)
        {
            tableClient.DeleteEntity(onGoingEvent.PartitionKey, onGoingEvent.RowKey);
            Console.WriteLine("Deleted " + onGoingEvent);
        }
    }
}
