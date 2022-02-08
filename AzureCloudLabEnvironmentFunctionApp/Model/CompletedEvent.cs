using System;
using Azure;
using Azure.Data.Tables;

namespace AzureCloudLabEnvironment.Model
{
    class CompletedEvent : ITableEntity
    {
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public string Context { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public override string ToString()
        {
            return PartitionKey;
        }
    }
}
