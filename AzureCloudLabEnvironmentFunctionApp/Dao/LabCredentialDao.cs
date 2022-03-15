using System.Collections.Generic;
using System.Linq;
using AzureCloudLabEnvironment.Helper;
using AzureCloudLabEnvironment.Model;
using Microsoft.Extensions.Logging;

namespace AzureCloudLabEnvironment.Dao;

internal class LabCredentialDao : Dao<LabCredential>
{
    public LabCredentialDao(Config config, ILogger logger) : base(config, logger)
    {
    }

    public List<LabCredential> GetByLab(string lab)
    {
        var oDataQueryEntities =
            TableClient.Query<LabCredential>(c => c.PartitionKey == lab);
        return oDataQueryEntities.OrderBy(c => c.Email).ToList();
    }
}