using System;
using Azure;
using Azure.Data.Tables;

namespace AzureCloudLabEnvironment.Model;

internal class Subscription : ITableEntity
{
    public string Email { get; set; }
    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}