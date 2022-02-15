using System;
using System.Runtime.Serialization;

namespace AzureCloudLabEnvironment.Model;

public class Event : JsonBase<Event>
{
    [DataMember] public string Title { get; set; }
    [DataMember] public DateTime StartTime { get; set; }
    [DataMember] public DateTime EndTime { get; set; }
    [DataMember] public string Context { get; set; }
    [DataMember] public int RepeatTimes { get; set; }
    [DataMember] public string Type { get; set; }

    public override string ToString()
    {
        return $"{Title} [{RepeatTimes}] {Type} ({StartTime} - {EndTime}): {Context}";
    }
}