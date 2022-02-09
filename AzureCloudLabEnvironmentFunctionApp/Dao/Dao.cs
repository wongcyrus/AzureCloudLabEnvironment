using System;
using System.Linq;
using Azure;
using Azure.Data.Tables;
using AzureCloudLabEnvironment.Helper;
using Microsoft.Extensions.Logging;

namespace AzureCloudLabEnvironment.Dao;

internal abstract class Dao<T> where T : class, ITableEntity, new()
{
    protected readonly ILogger Logger;
    protected readonly TableClient TableClient;

    protected Dao(Config config, ILogger logger)
    {
        TableClient = GetTableClient(config);
        Logger = logger;
    }

    private TableClient GetTableClient(Config config)
    {
        var connectionString = config.GetConfig(Config.Key.AzureWebJobsStorage);
        var tableClient = new TableClient(
            connectionString,
            GetType().Name.Replace("Dao", ""));
        return tableClient;
    }

    public bool IsNew(T entity)
    {
        var oDataQueryEntities = TableClient.Query<T>(
            TableClient.CreateQueryFilter($"PartitionKey eq {entity.PartitionKey} and RowKey eq {entity.RowKey}"));
        return !oDataQueryEntities.Any();
    }

    public bool Add(T entity)
    {
        var response = TableClient.AddEntity(entity);
        Logger.LogInformation("Saved " + entity);
        return !response.IsError;
    }

    public bool Update(T entity)
    {
        var response = TableClient.UpdateEntity(entity, ETag.All, TableUpdateMode.Replace);
        Logger.LogInformation("Updated " + entity);
        return !response.IsError;
    }

    public bool Upsert(T entity)
    {
        return IsNew(entity) ? Add(entity) : Update(entity);
    }

    public void Delete(T entity)
    {
        TableClient.DeleteEntity(entity.PartitionKey, entity.RowKey);
        Logger.LogInformation("Deleted " + entity);
    }

    public T Get(string partitionKey)
    {
        try
        {
            var response = TableClient.GetEntity<T>(partitionKey, partitionKey);
            return response.Value;
        }
        catch (Exception)
        {
            return null;
        }
    }
}