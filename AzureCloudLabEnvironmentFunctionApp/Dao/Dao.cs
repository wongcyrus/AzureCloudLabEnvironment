using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AzureCloudLabEnvironment.Dao
{
    abstract class Dao
    {
        protected readonly TableClient TableClient;
        protected readonly ILogger Logger;

        protected Dao(IConfigurationRoot config, ILogger logger)
        {
            this.TableClient = GetTableClient(config);
            this.Logger = logger;
        }

        private TableClient GetTableClient(IConfigurationRoot config)
        {
            var connectionString = config["AzureWebJobsStorage"];
            var tableClient = new TableClient(
                connectionString,
                this.GetType().Name.Replace("Dao", ""));
            return tableClient;
        }
    }
}
