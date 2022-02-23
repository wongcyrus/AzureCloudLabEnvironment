using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using AzureCloudLabEnvironment.Model;
using Microsoft.Extensions.Logging;

namespace AzureCloudLabEnvironment.Helper
{
    internal class LifeCycleHook
    {
        public static async Task SendCallBack(Deployment deployment, ILogger log)
        {
            if (!string.IsNullOrEmpty(deployment.LifeCycleHookUrl))
            {
                using var client = new HttpClient();
                var values = new Dictionary<string, string>
                {
                    {"Status", deployment.Status},
                    {"Output", deployment.Output}
                };
                try
                {
                    await client.PostAsync(deployment.LifeCycleHookUrl, new FormUrlEncodedContent(values));
                    log.LogInformation(
                        $"Sent {deployment.Status} Callback to {deployment.LifeCycleHookUrl}");
                }
                catch (Exception ex)
                {
                    log.LogError("SendCallBack Error.");
                    log.LogError(ex.Message);
                }
            }
        }
    }
}
