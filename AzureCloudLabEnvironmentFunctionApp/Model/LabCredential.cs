using System;
using Azure;
using Azure.Data.Tables;

namespace AzureCloudLabEnvironment.Model;

internal class LabCredential :  ITableEntity
{
    public string AppId { get; set; }
    public string DisplayName { get; set; }
    public string Password { get; set; }
    public string Tenant { get; set; }

    public string SubscriptionId { get; set; }
    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}