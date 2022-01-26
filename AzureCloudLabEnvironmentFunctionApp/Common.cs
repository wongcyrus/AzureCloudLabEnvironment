using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;

namespace AzureCloudLabEnvironment
{
    class Common
    {
        public static IConfigurationRoot Config(ExecutionContext context)
        {
            return new ConfigurationBuilder()
                .SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
        }
    }
}
