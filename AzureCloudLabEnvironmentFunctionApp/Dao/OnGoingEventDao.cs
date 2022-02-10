using System;
using System.Collections.Generic;
using System.Linq;
using AzureCloudLabEnvironment.Helper;
using AzureCloudLabEnvironment.Model;
using Microsoft.Extensions.Logging;

namespace AzureCloudLabEnvironment.Dao;

internal class OnGoingEventDao : Dao<OnGoingEvent>
{
    public OnGoingEventDao(Config config, ILogger logger) : base(config, logger)
    {
    }

    public List<OnGoingEvent> GetEndedEvents()
    {
        var oDataQueryEntities = TableClient.Query<OnGoingEvent>(c => c.EndTime < DateTime.UtcNow.AddMinutes(0.5));
        return oDataQueryEntities.ToList();
    }
}