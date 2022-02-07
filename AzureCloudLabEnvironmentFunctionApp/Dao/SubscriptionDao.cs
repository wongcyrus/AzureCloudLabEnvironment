using AzureCloudLabEnvironment.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AzureCloudLabEnvironment.Dao
{
    internal class SubscriptionDao : Dao<Subscription>
    {
        public SubscriptionDao(IConfigurationRoot config, ILogger logger) : base(config, logger)
        {
        }

    }
}
