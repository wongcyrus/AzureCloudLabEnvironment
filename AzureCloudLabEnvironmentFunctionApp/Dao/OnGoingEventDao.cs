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

    internal class OnGoingEventDao : Dao<OnGoingEvent>
    {

        public OnGoingEventDao(IConfigurationRoot config, ILogger logger) : base(config, logger)
        {
        }
        
        public List<OnGoingEvent> GetEndedEvents()
        {
            Pageable<OnGoingEvent> oDataQueryEntities = TableClient.Query<OnGoingEvent>(c => c.EndTime < DateTime.UtcNow);
            return oDataQueryEntities.ToList();
        }
    }
}
