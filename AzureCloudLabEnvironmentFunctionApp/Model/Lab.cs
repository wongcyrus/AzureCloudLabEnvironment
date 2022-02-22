using System.Runtime.Serialization;

namespace AzureCloudLabEnvironment.Model;

[DataContract]
public class Lab : JsonBase<Lab>
{
    [DataMember] public string Name { get; set; }
    [DataMember] public string GitHubRepo { get; set; }
    [DataMember] public string Branch { get; set; }
    [DataMember] public string Location { get; set; }
    [DataMember(IsRequired = false, EmitDefaultValue = false)] public string LifeCycleHookUrl { get; set; }
    [DataMember] public int? RepeatedTimes { get; set; }

    public override string ToString()
    {
        return $"{Name}({RepeatedTimes})->{GitHubRepo}({Branch})";
    }
}