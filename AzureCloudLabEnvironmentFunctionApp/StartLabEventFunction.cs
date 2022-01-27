using AzureCloudLabEnvironment.Model;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]
            HttpRequest req, ILogger log,
            ExecutionContext context)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            var isCreate = !req.Query.ContainsKey("Delete");

            var defaultCredential = new DefaultAzureCredential();
            var defaultToken = defaultCredential
                .GetToken(new TokenRequestContext(new[] { "https://management.azure.com/.default" })).Token;
            var defaultTokenCredentials = new Microsoft.Rest.TokenCredentials(defaultToken);
            var azureCredentials = new AzureCredentials(defaultTokenCredentials, defaultTokenCredentials, null,
                AzureEnvironment.AzureGlobalCloud);

            var azure = Microsoft.Azure.Management.Fluent.Azure.Authenticate(azureCredentials)
                .WithDefaultSubscription();

            var config = Common.Config(context);
            await RunTerraformWithContainerGroupAsync(azure, config,
                "https://github.com/wongcyrus/AzureCloudLabInfrastructure", "main", isCreate, "container", "lab",
                "123456789", new Dictionary<string, string>(){
                    {"NAME", "Student1"},
                });


            return new OkObjectResult($"Hello, {isCreate}");
        }


        private static async Task<string> RunTerraformWithContainerGroupAsync(IAzure azure,
            IConfigurationRoot config,
            string gitRepositoryUrl,
            string branch,
            bool isCreate,
            string containerGroupName,
            string lab,
            string studentId, IDictionary<string, string> terraformVariables)
        {
            Console.WriteLine($"\nCreating container group '{containerGroupName}'...");

            const string resourceGroupName = "azure-cloud-lab-environment-terraform";

            // Get the resource group's region
            IResourceGroup resGroup = await azure.ResourceGroups.GetByNameAsync(resourceGroupName);
            Region azureRegion = resGroup.Region;

            var containerGroup = await azure.ContainerGroups.GetByResourceGroupAsync(resourceGroupName, containerGroupName);
            if (containerGroup != null)
            {
                Console.WriteLine("Delete");
                await azure.ContainerGroups.DeleteByIdAsync(containerGroup.Id);
                Console.WriteLine("Deleted");
            }
            Console.WriteLine("Create");

            var scriptUrl = gitRepositoryUrl.Replace("github.com", "raw.githubusercontent.com") + "/" + branch + "/" + (isCreate
                ? "deploy.sh"
                : "undeploy.sh");

            var commands = $"curl -s {scriptUrl} | bash";

            terraformVariables.Add("LAB", lab);
            terraformVariables.Add("STUDENT_ID", studentId);
            var prefixTerraformVariables = terraformVariables.Select(item => ("TF_VAR_" + item.Key, item.Value)).ToDictionary(p => p.Item1, p => p.Item2);
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
                .DefineContainerInstance("terraformcli-" + studentId)
                .WithImage(config["AcrUrl"] + "/terraformazurecli:latest")
                .WithExternalTcpPort(80)
                .WithCpuCoreCount(0.25)
                .WithMemorySizeInGB(0.5)
                .WithEnvironmentVariableWithSecuredValue("ARM_CLIENT_ID", config["ARM_CLIENT_ID"])
                .WithEnvironmentVariableWithSecuredValue("ARM_CLIENT_SECRET", config["ARM_CLIENT_SECRET"])
                .WithEnvironmentVariableWithSecuredValue("ARM_SUBSCRIPTION_ID", config["ARM_SUBSCRIPTION_ID"])
                .WithEnvironmentVariableWithSecuredValue("ARM_TENANT_ID", config["ARM_TENANT_ID"])
                .WithEnvironmentVariable("LAB", lab)
                .WithEnvironmentVariable("STUDENT_ID", studentId)
                .WithEnvironmentVariables(prefixTerraformVariables)
                .WithStartingCommandLine("/bin/bash", "-c", commands)
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
