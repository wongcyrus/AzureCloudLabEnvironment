using AzureCloudLabEnvironment.Model;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Management.ContainerInstance.Fluent;
using Microsoft.Azure.Management.ContainerInstance.Fluent.Models;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;


namespace AzureCloudLabEnvironment
{
    public class StartLabEventFunction
    {
        [FunctionName(nameof(StartLabEventFunction))]
        public void Run([QueueTrigger("start-event", Connection = "AzureWebJobsStorage")] Lab lab, ILogger log)
        {

            log.LogInformation($"StartLabEventFunction Queue trigger function processed: {lab}");
        }

        [FunctionName("HttpTriggerCSharp")]
        public static async Task<IActionResult> HttpTriggerCSharp(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log,
            ExecutionContext context)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            var defaultCredential = new DefaultAzureCredential();
            var defaultToken = defaultCredential.GetToken(new TokenRequestContext(new[] { "https://management.azure.com/.default" })).Token;
            var defaultTokenCredentials = new Microsoft.Rest.TokenCredentials(defaultToken);
            var azureCredentials = new AzureCredentials(defaultTokenCredentials, defaultTokenCredentials, null, AzureEnvironment.AzureGlobalCloud);

            var azure = Microsoft.Azure.Management.Fluent.Azure.Authenticate(azureCredentials).WithDefaultSubscription();

            var config = Common.Config(context);
            await CreateContainerGroupAsync(azure, config, "azure-cloud-lab-environment-terraform", "container", "demo");
            string name = req.Query["name"];

            string requestBody = String.Empty;
            using (StreamReader streamReader = new StreamReader(req.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            return name != null
                ? (ActionResult)new OkObjectResult($"Hello, {name}")
                : new BadRequestObjectResult("Please pass a name on the query string or in the request body");
        }

        /// <summary>
        /// Creates a container group with a single container.
        /// </summary>
        /// <param name="azure">An authenticated IAzure object.</param>
        /// <param name="configurationRoot"></param>
        /// <param name="resourceGroupName">The name of the resource group in which to create the container group.</param>
        /// <param name="containerGroupName">The name of the container group to create.</param>
        /// <param name="containerImage">The container image name and tag, for example 'microsoft\aci-helloworld:latest'.</param>
        /// <param name="instanceId"></param>
        private static async Task<string> CreateContainerGroupAsync(IAzure azure,
            IConfigurationRoot config,
            string resourceGroupName,
            string containerGroupName,
            string instanceId)
        {
            Console.WriteLine($"\nCreating container group '{containerGroupName}'...");

            // Get the resource group's region
            IResourceGroup resGroup = azure.ResourceGroups.GetByName(resourceGroupName);
            Region azureRegion = resGroup.Region;

            var containerGroup = azure.ContainerGroups.GetByResourceGroup(resourceGroupName, containerGroupName);
            if (containerGroup != null)
            {
                Console.WriteLine("Delete");
                await azure.ContainerGroups.DeleteByIdAsync(containerGroup.Id);
                Console.WriteLine("Deleted");
            }

            var commands = @"
rm -rf AzureCloudLabInfrastructure/ || \
git clone https://github.com/wongcyrus/AzureCloudLabInfrastructure || \
echo $(pwd) || \
cp terraform.tfvars AzureCloudLabInfrastructure/ || \
cd AzureCloudLabInfrastructure && \
echo $(pwd) || \
terraform init && \
terraform apply -auto-approve
";
            containerGroup = azure.ContainerGroups.Define(containerGroupName)
                .WithRegion(azureRegion)
                .WithExistingResourceGroup(resourceGroupName)
                .WithLinux()
                .WithPrivateImageRegistry(config["AcrUrl"], config["AcrUserName"], config["AcrPassword"])
                .DefineVolume("workspace")
                .WithExistingReadWriteAzureFileShare("containershare")
                .WithStorageAccountName(config["StorageAccountName"])
                .WithStorageAccountKey(config["StorageAccountKey"])
                .Attach()
                .DefineContainerInstance("terraformcli")
                .WithImage(config["AcrUrl"] + "/terraformazurecli:latest")
                .WithExternalTcpPort(80)
                .WithCpuCoreCount(1.0)
                .WithMemorySizeInGB(3)
                .WithEnvironmentVariableWithSecuredValue("ARM_CLIENT_ID", config["ARM_CLIENT_ID"])
                .WithEnvironmentVariableWithSecuredValue("ARM_CLIENT_SECRET", config["ARM_CLIENT_SECRET"])
                .WithEnvironmentVariableWithSecuredValue("ARM_SUBSCRIPTION_ID", config["ARM_SUBSCRIPTION_ID"])
                .WithEnvironmentVariableWithSecuredValue("ARM_TENANT_ID", config["ARM_TENANT_ID"])
                .WithStartingCommandLine("/bin/sh", "-c", commands)
                .WithVolumeMountSetting("workspace", "/workspace")
                .Attach()
                .WithRestartPolicy(ContainerGroupRestartPolicy.Never)
                .WithDnsPrefix(containerGroupName)
                .Create();
            Console.WriteLine("Created");
            // Create the container group


            Console.WriteLine($"Once DNS has propagated, container group '{containerGroup.Name}' will be reachable at http://{containerGroup.Fqdn}");
            return containerGroup.IPAddress;
        }

        private static void DeleteContainerGroup(IAzure azure, string resourceGroupName, string containerGroupName)
        {
            IContainerGroup containerGroup = null;

            while (containerGroup == null)
            {
                containerGroup = azure.ContainerGroups.GetByResourceGroup(resourceGroupName, containerGroupName);

                SdkContext.DelayProvider.Delay(1000);
            }

            Console.WriteLine($"Deleting container group '{containerGroupName}'...");

            azure.ContainerGroups.DeleteById(containerGroup.Id);
        }
    }
}
