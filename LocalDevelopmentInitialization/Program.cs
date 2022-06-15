// See https://aka.ms/new-console-template for more information
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Azure.Data.Tables;


Console.WriteLine("Create Local Azure Storage Account resources.");
var tableServiceClient = new TableServiceClient(
    new Uri("UseDevelopmentStorage=true"));

var tables = new[] { "CompletedEvent", "OnGoingEvent", "Subscription", "LabCredential", "Deployment", "ErrorLog" };
foreach (var tableName in tables)
{
    tableServiceClient.CreateTableIfNotExists(tableName);
}

var queueServiceClient = new QueueServiceClient(new Uri("UseDevelopmentStorage=true"));
var queues = new[] { "start-event", "end-event" };
foreach (var queueName in queues)
{
    queueServiceClient.CreateQueue(queueName);
}

var blobs = new[] { "lab-variables" };
var BblobServiceClient = new BlobServiceClient(new Uri("UseDevelopmentStorage=true"));
foreach (var containerName in blobs)
{
    BblobServiceClient.CreateBlobContainer(containerName);
}
Console.WriteLine("Created Local Azure Storage Account resources.");

