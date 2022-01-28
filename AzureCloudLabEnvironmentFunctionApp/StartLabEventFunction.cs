using AzureCloudLabEnvironment.Model;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Management.ContainerInstance.Fluent;
using Microsoft.Azure.Management.ContainerInstance.Fluent.ContainerGroup.Definition;
using Microsoft.Azure.Management.ContainerInstance.Fluent.Models;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;


namespace AzureCloudLabEnvironment
{
    public class StartLabEventFunction
    {
        [FunctionName(nameof(StartLabEventFunction))]
        public void Run([QueueTrigger("start-event", Connection = "AzureWebJobsStorage")] Event ev, ILogger log)
        {
            Lab lab = Lab.FromJson(ev.Context);
            log.LogInformation($"StartLabEventFunction Queue trigger function processed: {ev} => {lab}");
            if (lab == null) return;
            lab.Name = ev.Title;
            lab.RepeatTimes = ev.RepeatTimes;
            log.LogInformation($"Start the lab: {lab}");
        }

        [FunctionName("HttpTriggerCSharp")]
        public static async Task<IActionResult> HttpTriggerCSharp(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]
            HttpRequest req, ILogger log,
            ExecutionContext context)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            var isCreate = !req.Query.ContainsKey("Delete");

            var azure = await Common.GetAzure();

            var gitRepositoryUrl = "https://github.com/wongcyrus/AzureCloudLabInfrastructure";
            var branch = "main";
            var lab = "lab";
            var terraformVariables = new Dictionary<string, string>()
            {
                {"NAME", "StudentRG"},
            };

            string[] studentIds = Enumerable.Range(1, 60).Select(x => $"{x:00000000}").ToArray();

            var subStudentGroup = studentIds.Chunk(10);
            terraformVariables.Add("LAB", lab);
            var config = Common.Config(context);
            var tasks = Enumerable.Range(0, subStudentGroup.Count())
                .Select(i => RunTerraformWithContainerGroupAsync(azure, config,
                    gitRepositoryUrl, branch, isCreate, "container-" + i + "-" + lab, lab, new Dictionary<string, string>(terraformVariables), subStudentGroup.ElementAt(i)));
            await Task.WhenAll(tasks);

            return new OkObjectResult($"Hello, {isCreate}");
        }



        private static async Task<string> RunTerraformWithContainerGroupAsync(IAzure azure,
            IConfigurationRoot config,
            string gitRepositoryUrl,
            string branch,
            bool isCreate,
            string containerGroupName,
            string lab,
            IDictionary<string, string> terraformVariables, string[] studentIds)
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

            IWithVolume containerGroupWithVolume =
                azure.ContainerGroups.Define(containerGroupName)
               .WithRegion(azureRegion)
               .WithExistingResourceGroup(resourceGroupName)
               .WithLinux()
               .WithPrivateImageRegistry(config["AcrUrl"], config["AcrUserName"], config["AcrPassword"])
               .DefineVolume("workspace")
               .WithExistingReadWriteAzureFileShare("containershare")
               .WithStorageAccountName(config["StorageAccountName"])
               .WithStorageAccountKey(config["StorageAccountKey"])
               .Attach();


            IWithNextContainerInstance withNextContainerInstance = null;
            for (var index = 0; index < studentIds.Length; index++)
            {
                var studentId = studentIds[index];
                withNextContainerInstance = AddContainerInstance(containerGroupWithVolume, withNextContainerInstance,
                    config, commands, lab, index, studentId, terraformVariables);
            }

            containerGroup = withNextContainerInstance
                .WithRestartPolicy(ContainerGroupRestartPolicy.Never)
                .WithDnsPrefix(containerGroupName)
                .Create();
            Console.WriteLine("Created");

            return containerGroup.Id;
        }


        private static IWithNextContainerInstance AddContainerInstance(IWithVolume containerGroupWithVolume, IWithNextContainerInstance withNextContainerInstance, IConfigurationRoot config, string commands, string lab,
int index, string studentId,
            IDictionary<string, string> terraformVariables)
        {
            terraformVariables.Remove("STUDENT_ID");
            terraformVariables.Add("STUDENT_ID", studentId);
            var prefixTerraformVariables = terraformVariables.Select(item => ("TF_VAR_" + item.Key, item.Value)).ToDictionary(p => p.Item1, p => p.Item2);

            IWithNextContainerInstance SetContainer(IContainerInstanceDefinitionBlank<IWithNextContainerInstance> container)
            {
                return container.WithImage(config["AcrUrl"] + "/terraformazurecli:latest")
                    .WithExternalTcpPort(80 + index)
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
                    .Attach();
            }
            //TODO: Remove Workaround for bug https://github.com/Azure/azure-libraries-for-net/issues/1275
            IContainerInstanceDefinitionBlank<IWithNextContainerInstance> container;
            if (withNextContainerInstance == null)
            {
                container = containerGroupWithVolume.DefineContainerInstance("terraformcli-" + studentId);
            }
            else
            {
                container = withNextContainerInstance.DefineContainerInstance("terraformcli-" + studentId);
            }

            return SetContainer(container);
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
