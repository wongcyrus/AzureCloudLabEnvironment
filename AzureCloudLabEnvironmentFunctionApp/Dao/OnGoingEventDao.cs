using System;
using System.Collections.Generic;
using System.Linq;
using Azure;
using Azure.Data.Tables;
using AzureCloudLabEnvironment.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AzureCloudLabEnvironment.Dao
{

    internal class OnGoingEventDao: Dao
    {

        public OnGoingEventDao(IConfigurationRoot config, ILogger logger) : base(config, logger)
        {
        }

        public bool IsNew(OnGoingEvent onGoingEvent)
        {
            Pageable<OnGoingEvent> oDataQueryEntities = TableClient.Query<OnGoingEvent>(
                filter: TableClient.CreateQueryFilter($"PartitionKey eq {onGoingEvent.PartitionKey} and RowKey eq {onGoingEvent.RowKey}"));
            return !oDataQueryEntities.Any();
        }

        public int GetRepeatCount(OnGoingEvent onGoingEvent)
        {
            Pageable<OnGoingEvent> oDataQueryEntities =
                TableClient.Query<OnGoingEvent>(filter: TableClient.CreateQueryFilter($"PartitionKey eq {onGoingEvent.PartitionKey}"));
            return oDataQueryEntities.Count();
        }

        public List<OnGoingEvent> GetEndedEvents()
        {
            Pageable<OnGoingEvent> oDataQueryEntities = TableClient.Query<OnGoingEvent>(c => c.EndTime < DateTime.UtcNow);
            return oDataQueryEntities.ToList();
        }

        public void SaveNewEvent(OnGoingEvent onGoingEvent)
        {
            TableClient.AddEntity(onGoingEvent);
            Logger.LogInformation("Saved " + onGoingEvent);
        }

        public void DeleteEndedEvent(OnGoingEvent onGoingEvent)
        {
            TableClient.DeleteEntity(onGoingEvent.PartitionKey, onGoingEvent.RowKey);
            Logger.LogInformation("Deleted " + onGoingEvent);
        }
    }
}
