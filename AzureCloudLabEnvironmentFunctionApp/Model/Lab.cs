using System;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AzureCloudLabEnvironment.Model
{
    public class Lab
    {
        public string Name { get; set; }
        public string TerraformRepo { get; set; }
        public string Branch { get; set; }

        public int? RepeatTimes { get; set; }
        public override string ToString()
        {
            return $"{Name}({RepeatTimes})->{TerraformRepo}({Branch})";
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
