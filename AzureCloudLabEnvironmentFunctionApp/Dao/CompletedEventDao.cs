using System.Linq;
using AzureCloudLabEnvironment.Helper;
using AzureCloudLabEnvironment.Model;
using Microsoft.Extensions.Logging;

namespace AzureCloudLabEnvironment.Dao;

internal class CompletedEventDao : Dao<CompletedEvent>
{
    public CompletedEventDao(Config config, ILogger logger) : base(config, logger)
    {
    }

    public int GetRepeatCount(string partitionKey)
    {
        var oDataQueryEntities =
            TableClient.Query<CompletedEvent>(c => c.PartitionKey == partitionKey);
        return oDataQueryEntities.Count();
    }
}