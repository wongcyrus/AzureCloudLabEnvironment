using Microsoft.WindowsAzure.Storage.Table;

namespace AzureCloudLabEnvironment.Model
{
    internal class Subscription : TableEntity
    {
        public string Email { get; set; }
    }
}

  
