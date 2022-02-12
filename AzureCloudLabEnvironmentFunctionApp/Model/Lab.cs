using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.Logging;

namespace AzureCloudLabEnvironment.Model
{
    [DataContract]
    public class Lab
    {
        [DataMember] public string Name { get; set; }
        [DataMember] public string GitHubRepo { get; set; }
        [DataMember] public string Branch { get; set; }

        [DataMember] public int? RepeatedTimes { get; set; }
        public override string ToString()
        {
            return $"{Name}({RepeatedTimes})->{GitHubRepo}({Branch})";
        }
        public static Lab FromJson(string content, ILogger logger)
        {
            try
            {
                StringWriter myWriter = new StringWriter();
                HttpUtility.HtmlDecode(content, myWriter);
                var json = myWriter.ToString();
                json = Regex.Replace(json, "<.*?>", string.Empty);
                var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
                var ser = new DataContractJsonSerializer(typeof(Lab));
                var lab  = ser.ReadObject(ms) as Lab;
                ms.Close();
                return lab;
            }
            catch (Exception e)
            {
                logger.LogError(e.ToString());
                return null;
            }

        }
    }
}
