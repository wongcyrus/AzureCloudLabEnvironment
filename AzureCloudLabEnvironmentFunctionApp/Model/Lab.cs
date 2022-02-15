using System.Runtime.Serialization;

namespace AzureCloudLabEnvironment.Model;

[DataContract]
public class Lab : JsonBase<Lab>
{
    [DataMember] public string Name { get; set; }
    [DataMember] public string GitHubRepo { get; set; }
    [DataMember] public string Branch { get; set; }

    [DataMember] public int? RepeatedTimes { get; set; }

    public override string ToString()
    {
        return $"{Name}({RepeatedTimes})->{GitHubRepo}({Branch})";
    }
}