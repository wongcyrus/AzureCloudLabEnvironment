using System;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AzureCloudLabEnvironment.Model
{
    public class Lab
    {
        public string Name { get; set; }
        public string GitHubRepo { get; set; }
        public string Branch { get; set; }

        public int? RepeatedTimes { get; set; }
        public override string ToString()
        {
            return $"{Name}({RepeatedTimes})->{GitHubRepo}({Branch})";
        }
        public static Lab FromJson(string jsonString)
        {
            try
            {
                return JsonSerializer.Deserialize<Lab>(Regex.Replace(jsonString, "<.*?>", string.Empty));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return null;
            }
           
        }
    }
}
