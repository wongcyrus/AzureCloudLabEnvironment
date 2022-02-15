using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.Logging;

namespace AzureCloudLabEnvironment.Model;

[DataContract]
public abstract class JsonBase<T> where T : class
{
    public string ToJson()
    {
        var serializer = new DataContractJsonSerializer(GetType());
        using var ms = new MemoryStream();
        serializer.WriteObject(ms, this);
        return Encoding.Default.GetString(ms.ToArray());
    }

    public static T FromJson(string content, ILogger logger)
    {
        try
        {
            var myWriter = new StringWriter();
            HttpUtility.HtmlDecode(content, myWriter);
            var json = myWriter.ToString();
            json = Regex.Replace(json, "<.*?>", string.Empty);
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var ser = new DataContractJsonSerializer(typeof(T));
            var obj = ser.ReadObject(ms) as T;
            ms.Close();
            return obj;
        }
        catch (Exception e)
        {
            logger.LogError(e.ToString());
            return null;
        }
    }
}