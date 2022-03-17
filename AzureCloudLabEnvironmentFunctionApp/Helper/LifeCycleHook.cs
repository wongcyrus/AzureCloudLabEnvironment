using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using AzureCloudLabEnvironment.Model;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AzureCloudLabEnvironment.Helper
{
    internal class LifeCycleHook
    {
        public static async Task<Dictionary<string, string>> SendCallBack(Deployment deployment, ILogger log)
        {
            if (!string.IsNullOrEmpty(deployment.LifeCycleHookUrl))
            {
                using var client = new HttpClient();
                var values = new Dictionary<string, string>
                {
                    {"Lab", deployment.Name},
                    {"RepeatedTimes", deployment.RepeatedTimes.ToString()},
                    {"Email", deployment.Email},
                    {"Status", deployment.Status},  
                    {"Variables", deployment.Variables},
                    {"Output", deployment.Output}
                };
                try
                {
                    log.LogInformation(deployment.ToString());
                    var httpResponseMessage = await client.PostAsync(deployment.LifeCycleHookUrl, new FormUrlEncodedContent(values));                    
                    var json = httpResponseMessage.Content.ReadAsStringAsync().Result;
                    log.LogInformation(
                        $"Sent {deployment.Status} Callback to {deployment.LifeCycleHookUrl}");                    
                    log.LogInformation(json);
                    return JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                }
                catch (Exception ex)
                {
                    log.LogError("SendCallBack Error.");
                    log.LogError(ex.Message);
                }
            }
            return new Dictionary<string, string>();
        }
    }
}
