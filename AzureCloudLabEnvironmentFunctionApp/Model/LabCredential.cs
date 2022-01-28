using Microsoft.WindowsAzure.Storage.Table;

namespace AzureCloudLabEnvironment.Model;

internal class LabCredential : TableEntity
{
    public string AppId { get; set; }
    public string DisplayName { get; set; }
    public string Password { get; set; }
    public string Tenant { get; set; }
}