using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AzureCloudLabEnvironment.Model
{
    public class Event
    {
        public string Title { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Context { get; set; }
        public int RepeatTimes { get; set; }
        public string Type { get; set; }

        public string ToJson()
        {
            return JsonSerializer.Serialize<Event>(this);
        }

        public static Event FromJson(string jsonString)
        {
            return JsonSerializer.Deserialize<Event>(jsonString);
        }

        public override string ToString()
        {
            return $"{Title}({RepeatTimes}) {Type}: {Context}";
        }
    }
}
