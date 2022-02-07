using System.Linq;
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AzureCloudLabEnvironment.Dao
{
    abstract class Dao<T> where T : class, ITableEntity, new()
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

        public bool IsNew(T entity)
        {
            Pageable<T> oDataQueryEntities = TableClient.Query<T>(
                filter: TableClient.CreateQueryFilter($"PartitionKey eq {entity.PartitionKey} and RowKey eq {entity.RowKey}"));
            return !oDataQueryEntities.Any();
        }

        public bool Save(T entity)
        {
            var response = TableClient.AddEntity(entity);
            Logger.LogInformation("Saved " + entity);
            return !response.IsError;
        }

        public void Delete(T entity)
        {
            TableClient.DeleteEntity(entity.PartitionKey, entity.RowKey);
            Logger.LogInformation("Deleted " + entity);
        }
    }
}
