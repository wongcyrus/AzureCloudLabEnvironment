using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;

namespace AzureCloudLabEnvironment.Helper
{
    public class Config
    {
        private readonly IConfigurationRoot _config;

        public Config(ExecutionContext context)
        {
            _config = new ConfigurationBuilder()
                .SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
        }

        public enum Key
        {
            AzureWebJobsStorage,
            CalendarUrl,
            CalendarTimeZone,
            AcrUrl,
            AcrUserName,
            AcrPassword,
            TerraformResourceGroupName,
            EmailSmtp,
            EmailUserName,
            EmailPassword,
            EmailFromAddress,
            Environment,
            StorageAccountName,
            StorageAccountKey,
            Salt
        };

        public string GetConfig( Key key)
        {
            var name = Enum.GetName(typeof(Key), key);
            return _config[name];
        }
    }
}
