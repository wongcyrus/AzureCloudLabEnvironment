using System;
using Azure;
using Azure.Data.Tables;

namespace AzureCloudLabEnvironment.Model;

internal class ErrorLog : ITableEntity
{
    public string Source { get; set; }
    public string Message { get; set; }
    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}