// See https://aka.ms/new-console-template for more information
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;


Console.WriteLine("Create Local Azure Storage Account resources.");

CloudStorageAccount storageAcc = CloudStorageAccount.Parse("UseDevelopmentStorage=true");

CloudTableClient cloudTableClient = storageAcc.CreateCloudTableClient();
var tables = new[] { "CompletedEvent", "OnGoingEvent", "Subscription", "LabCredential", "Deployment", "ErrorLog" };
foreach (var tableName in tables)
{
    var table = cloudTableClient.GetTableReference(tableName);
    await table.CreateIfNotExistsAsync();
}

var cloudQueueClient = storageAcc.CreateCloudQueueClient();
var queues = new[] { "start-event", "end-event" };
foreach (var queueName in queues)
{
    var queue = cloudQueueClient.GetQueueReference(queueName);
    await queue.CreateIfNotExistsAsync();
}

var cloudBlobClient = storageAcc.CreateCloudBlobClient();
var blobs = new[] { "lab-variables" };

foreach (var containerName in blobs)
{
    var container = cloudBlobClient.GetContainerReference(containerName);
    await container.CreateIfNotExistsAsync();
}
Console.WriteLine("Created Local Azure Storage Account resources.");

