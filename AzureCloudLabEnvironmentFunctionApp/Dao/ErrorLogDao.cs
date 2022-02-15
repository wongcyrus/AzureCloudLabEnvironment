using AzureCloudLabEnvironment.Helper;
using AzureCloudLabEnvironment.Model;
using Microsoft.Extensions.Logging;

namespace AzureCloudLabEnvironment.Dao;

internal class ErrorLogDao : Dao<ErrorLog>
{
    public ErrorLogDao(Config config, ILogger logger) : base(config, logger)
    {
    }
}