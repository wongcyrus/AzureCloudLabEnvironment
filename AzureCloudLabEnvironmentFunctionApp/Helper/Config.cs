using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;

namespace AzureCloudLabEnvironment.Helper
{
    public class Config
    {
        private readonly ExecutionContext _context;

        public Config(ExecutionContext context)
        {
            this._context = context;
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
            StorageAccountKey
        };

        public string GetConfig( Key key)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(_context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var name = Enum.GetName(typeof(Key), key);
            return config[name];
        }
    }
}
