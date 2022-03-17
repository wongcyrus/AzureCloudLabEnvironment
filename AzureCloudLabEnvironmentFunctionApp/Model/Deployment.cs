using System;
using System.Security.Cryptography;
using System.Text;
using Azure;
using Azure.Data.Tables;

namespace AzureCloudLabEnvironment.Model;

internal class Deployment : ITableEntity
{
    public string Email { get; set; }
    public string Name { get; set; }
    public string Location { get; set; }
    public string LifeCycleHookUrl { get; set; }
    public string GitHubRepo { get; set; }
    public string Branch { get; set; }
    public int RepeatedTimes { get; set; }
    public string Variables { get; set; }

    public string ExternalVariables { get; set; }
    public string Output { get; set; }
    public string Status { get; set; }

    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public override string ToString()
    {
        return $"{Name}[{RepeatedTimes}] {Branch} -> {Email} {Status}";
    }

    public string GetToken(string salt)
    {
        var tmpSource = salt + Name + Email + Branch + RepeatedTimes;

        string ComputeSha256Hash(string rawData)
        {
            // Create a SHA256   
            using var sha256Hash = SHA256.Create();
            // ComputeHash - returns byte array  
            var bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            // Convert byte array to a string   
            var builder = new StringBuilder();
            foreach (var b in bytes) builder.Append(b.ToString("x2"));
            return builder.ToString();
        }

        return ComputeSha256Hash(tmpSource);
    }
}