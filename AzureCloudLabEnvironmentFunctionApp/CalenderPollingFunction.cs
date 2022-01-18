using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace AzureCloudLabEnvironment
{
    public class CalenderPollingFunction
    {
        [FunctionName(nameof(CalenderPollingFunction))]
        public async Task Run([TimerTrigger("0 */1 * * * *")] TimerInfo myTimer, ILogger log)
        {
            var calendarSerializer = new CalendarSerializer();
            //calendarSerializer
            var url = new Uri("https://calendar.google.com/calendar/ical/ai28g3ticnn156spsve4k2t4fk%40group.calendar.google.com/private-a983a09afc269499a9bb6439563d03ce/basic.ics");
    
            var calendar = await CalenderPollingFunction.LoadFromUriAsync(url);

         
            var start = TimeZoneInfo.ConvertTime(DateTime.Now.AddMinutes(-5), TimeZoneInfo.FindSystemTimeZoneById("China Standard Time")); 
            var end  = TimeZoneInfo.ConvertTime(DateTime.Now.AddMinutes(5), TimeZoneInfo.FindSystemTimeZoneById("China Standard Time"));
            var occurrences = calendar.GetOccurrences(start, end);
            foreach (var occurrence in occurrences)
            {
                DateTime occurrenceTime = occurrence.Period.StartTime.AsSystemLocal;
                if (occurrence.Source is IRecurringComponent rc)
                {
                    Console.WriteLine(rc.Summary + ": " +
                                      occurrenceTime.ToShortTimeString());
                }
                Console.WriteLine(occurrence);
            }

            


            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
        }

        public static async Task<Calendar> LoadFromUriAsync(Uri uri)
        {
            using var client = new HttpClient();
            using var response = await client.GetAsync(uri);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsStringAsync();
            return Calendar.Load(result);
        }
    }
}
