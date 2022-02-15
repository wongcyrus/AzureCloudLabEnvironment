using AzureCloudLabEnvironment.Helper;
using AzureCloudLabEnvironment.Model;
using Microsoft.Extensions.Logging;

namespace AzureCloudLabEnvironment.Dao;

internal class DeploymentDao : Dao<Deployment>
{
    public DeploymentDao(Config config, ILogger logger) : base(config, logger)
    {
    }
}