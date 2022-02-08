using AzureCloudLabEnvironment.Helper;
using AzureCloudLabEnvironment.Model;
using Microsoft.Extensions.Logging;

namespace AzureCloudLabEnvironment.Dao;

internal class SubscriptionDao : Dao<Subscription>
{
    public SubscriptionDao(Config config, ILogger logger) : base(config, logger)
    {
    }
}