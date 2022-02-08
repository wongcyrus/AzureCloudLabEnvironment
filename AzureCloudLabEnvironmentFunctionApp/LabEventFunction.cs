using AzureCloudLabEnvironment.Model;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AzureCloudLabEnvironment.Dao;
using Microsoft.Azure.Management.ContainerInstance.Fluent;
using Microsoft.Azure.Management.ContainerInstance.Fluent.ContainerGroup.Definition;
using Microsoft.Azure.Management.ContainerInstance.Fluent.Models;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;


namespace AzureCloudLabEnvironment
{
    public class LabEventFunction
    {
        [FunctionName(nameof(StartLabEventHandlerFunction))]
        public async Task StartLabEventHandlerFunction([QueueTrigger("start-event", Connection = "AzureWebJobsStorage")] Event ev, ILogger log, ExecutionContext executionContext)
        {
            Lab lab = Lab.FromJson(ev.Context);
            log.LogInformation($"StartLabEventHandlerFunction Queue trigger function processed: {ev} => {lab}");
            if (lab == null) return;
            lab.Name = ev.Title;
            lab.RepeatTimes = ev.RepeatTimes;
            log.LogInformation($"Start the lab: {lab}");
            await RunClassInfrastructure(log, executionContext, lab, true);
        }

        [FunctionName(nameof(EndLabEventHandlerFunction))]
        public async Task EndLabEventHandlerFunction([QueueTrigger("end-event", Connection = "AzureWebJobsStorage")] Event ev, ILogger log, ExecutionContext executionContext)
        {
            Lab lab = Lab.FromJson(ev.Context);
            log.LogInformation($"EndLabEventHandlerFunction Queue trigger function processed: {ev} => {lab}");
            if (lab == null) return;
            lab.Name = ev.Title;
            lab.RepeatTimes = ev.RepeatTimes;
            log.LogInformation($"End the lab: {lab}");
            await RunClassInfrastructure(log, executionContext, lab, false);
        }

        private static async Task RunClassInfrastructure(ILogger log, ExecutionContext context, Lab lab, bool isCreate)
        {
            var config = Common.Config(context);
            var labCredentialDao = new LabCredentialDao(config, log);

            var students = labCredentialDao.GetByLab(lab.Name);

            var terraformVariables = new Dictionary<string, string>()
            {
                {"LAB", lab.Name},
                {"REPEAT_TIMES", lab.RepeatTimes.ToString()},
            };

            var subStudentGroup = students.Chunk(10).ToList();

            string GetContainerGroupName(int i, string labName)
            {
                return "container-" + i + "-" + Regex.Replace(labName, @"[^0-9a-zA-Z]+", "-").Trim();
            };

            var tasks = Enumerable.Range(0, subStudentGroup.Count())
                .Select(i => RunTerraformWithContainerGroupAsync(config,
                    lab.TerraformRepo, lab.Branch, isCreate, GetContainerGroupName(i, lab.Name),
                    new Dictionary<string, string>(terraformVariables), subStudentGroup.ElementAt(i)));
            await Task.WhenAll(tasks);
        }

        private static async Task<string> RunTerraformWithContainerGroupAsync(
            IConfigurationRoot config,
            string gitRepositoryUrl,
            string branch,
            bool isCreate,
            string containerGroupName,
            IDictionary<string, string> terraformVariables, LabCredential[] labCredentials)
        {
            Console.WriteLine($"\nCreating container group '{containerGroupName}'...");

            const string resourceGroupName = "azure-cloud-lab-environment-terraform";

            var azure = await Common.GetAzure();
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
            for (var index = 0; index < labCredentials.Length; index++)
            {
                var labCredential = labCredentials[index];
                withNextContainerInstance = AddContainerInstance(containerGroupWithVolume, withNextContainerInstance, config, commands, index, labCredential, new Dictionary<string, string>(terraformVariables));
            }

            containerGroup = withNextContainerInstance
                .WithRestartPolicy(ContainerGroupRestartPolicy.Never)
                .WithDnsPrefix(containerGroupName)
                .Create();
            Console.WriteLine("Created");

            return containerGroup.Id;
        }


        private static IWithNextContainerInstance AddContainerInstance(IWithVolume containerGroupWithVolume, IWithNextContainerInstance withNextContainerInstance, IConfigurationRoot config, string commands,
int index, LabCredential labCredential,
            IDictionary<string, string> terraformVariables)
        {
            terraformVariables.Remove("EMAIL");
            terraformVariables.Add("EMAIL", labCredential.Email);

            var prefixTerraformVariables = terraformVariables.Select(item => ("TF_VAR_" + item.Key, item.Value)).ToDictionary(p => p.Item1, p => p.Item2);

            IWithNextContainerInstance SetContainer(IContainerInstanceDefinitionBlank<IWithNextContainerInstance> container)
            {
                return container.WithImage(config["AcrUrl"] + "/terraformazurecli:latest")
                    .WithExternalTcpPort(80 + index)
                    .WithMemorySizeInGB(0.5)
                    .WithEnvironmentVariableWithSecuredValue("ARM_CLIENT_ID", labCredential.AppId)
                    .WithEnvironmentVariableWithSecuredValue("ARM_CLIENT_SECRET", labCredential.Password)
                    .WithEnvironmentVariableWithSecuredValue("ARM_SUBSCRIPTION_ID", labCredential.SubscriptionId)
                    .WithEnvironmentVariableWithSecuredValue("ARM_TENANT_ID", labCredential.Tenant)
                    .WithEnvironmentVariables(terraformVariables)
                    .WithEnvironmentVariables(prefixTerraformVariables)
                    .WithStartingCommandLine("/bin/bash", "-c", commands)
                    .WithVolumeMountSetting("workspace", "/workspace")
                    .Attach();
            }
            //TODO: Remove Workaround for bug https://github.com/Azure/azure-libraries-for-net/issues/1275
            IContainerInstanceDefinitionBlank<IWithNextContainerInstance> container;
            var suffix = Regex.Replace(labCredential.Email, @"[^0-9a-zA-Z]+", "-");
            container = withNextContainerInstance == null ?
                containerGroupWithVolume.DefineContainerInstance("terraformcli-" + suffix) :
                withNextContainerInstance.DefineContainerInstance("terraformcli-" + suffix);

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
