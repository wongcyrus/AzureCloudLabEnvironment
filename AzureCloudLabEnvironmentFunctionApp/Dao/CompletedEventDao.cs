using System.Linq;
using Azure;
using AzureCloudLabEnvironment.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AzureCloudLabEnvironment.Dao
{

    internal class CompletedEventDao : Dao<CompletedEvent>
    {
        public CompletedEventDao(IConfigurationRoot config, ILogger logger) : base(config, logger)
        {
        }

        public int GetRepeatCount(string partitionKey)
        {
            Pageable<CompletedEvent> oDataQueryEntities =
                TableClient.Query<CompletedEvent>(c => c.PartitionKey == partitionKey);
            return oDataQueryEntities.Count();
        }
    }
}
