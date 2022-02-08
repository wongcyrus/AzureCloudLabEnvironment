using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using AzureCloudLabEnvironment.Model;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;

namespace AzureCloudLabEnvironment
{
    internal static class Common
    {
        public static IConfigurationRoot Config(ExecutionContext context)
        {
            return new ConfigurationBuilder()
                .SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
        }
        public static async Task<IAzure> GetAzure()
        {
            var defaultCredential = new DefaultAzureCredential();
            var defaultToken = (await defaultCredential
                .GetTokenAsync(new TokenRequestContext(new[] { "https://management.azure.com/.default" }))).Token;
            var defaultTokenCredentials = new Microsoft.Rest.TokenCredentials(defaultToken);
            var azureCredentials = new AzureCredentials(defaultTokenCredentials, defaultTokenCredentials, null,
                AzureEnvironment.AzureGlobalCloud);

            var azure = await Microsoft.Azure.Management.Fluent.Azure.Authenticate(azureCredentials)
                .WithDefaultSubscriptionAsync();
            return azure;
        }

        public static async Task<bool> IsValidSubscriptionContributorRole(LabCredential labCredential,string subscriptionId)
        {
            var credentials = SdkContext.AzureCredentialsFactory.FromServicePrincipal(labCredential.AppId, labCredential.Password, labCredential.Tenant, AzureEnvironment.AzureGlobalCloud);
            var authenticated = Microsoft.Azure.Management.Fluent.Azure.Authenticate(credentials);
            try
            {
                await authenticated.RoleDefinitions
                      .GetByScopeAndRoleNameAsync("subscriptions/" + subscriptionId, "Contributor");
                return true;
            }
            catch (System.Exception)
            {
                return false;
            }

        }
    }
}
