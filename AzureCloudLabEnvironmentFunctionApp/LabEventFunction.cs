using System;
using AzureCloudLabEnvironment.Model;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AzureCloudLabEnvironment.Dao;
using AzureCloudLabEnvironment.Helper;
using Microsoft.Azure.Management.ContainerInstance.Fluent.ContainerGroup.Definition;
using Microsoft.Azure.Management.ContainerInstance.Fluent.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.WebJobs;


namespace AzureCloudLabEnvironment
{
    // ReSharper disable once UnusedMember.Global
    public class LabEventFunction
    {
        [FunctionName(nameof(StartLabEventHandlerFunction))]
        // ReSharper disable once UnusedMember.Global
        public async Task StartLabEventHandlerFunction([QueueTrigger("start-event", Connection = nameof(Config.Key.AzureWebJobsStorage))] Event ev, ILogger log, ExecutionContext executionContext)
        {
            Lab lab = Lab.FromJson(ev.Context);
            log.LogInformation($"StartLabEventHandlerFunction Queue trigger function processed: {ev} => {lab}");
            if (lab == null) return;
            lab.Name = ev.Title;
            lab.RepeatTimes = ev.RepeatTimes;
            lab.Branch = lab.Branch.Replace("###RepeatTimes###", lab.RepeatTimes.ToString());
            log.LogInformation($"Start the lab: {lab}");
            await RunClassInfrastructure(log, executionContext, lab, true);
        }

        [FunctionName(nameof(EndLabEventHandlerFunction))]
        public async Task EndLabEventHandlerFunction([QueueTrigger("end-event", Connection = nameof(Config.Key.AzureWebJobsStorage))] Event ev, ILogger log, ExecutionContext executionContext)
        {
            Lab lab = Lab.FromJson(ev.Context);
            log.LogInformation($"EndLabEventHandlerFunction Queue trigger function processed: {ev} => {lab}");
            if (lab == null) return;
            lab.Name = ev.Title;
            lab.RepeatTimes = ev.RepeatTimes;
            lab.Branch = lab.Branch.Replace("###RepeatTimes###", lab.RepeatTimes.ToString());
            log.LogInformation($"End the lab: {lab}");
            await RunClassInfrastructure(log, executionContext, lab, false);
        }

        private static async Task RunClassInfrastructure(ILogger log, ExecutionContext context, Lab lab, bool isCreate)
        {

            var config = new Config(context);
            var labCredentialDao = new LabCredentialDao(config, log);

            var students = labCredentialDao.GetByLab(lab.Name);

            var terraformVariables = new Dictionary<string, string>()
            {
                {"LAB", lab.Name},
                {"BRANCH", lab.Branch},
                {"REPEAT_TIMES", lab.RepeatTimes.ToString()},
            };

            var subStudentGroup = students.Chunk(10).ToList();

            string GetContainerGroupName(int i, string labName)
            {
                return "container-" + i + "-" + Regex.Replace(labName, @"[^0-9a-zA-Z]+", "-").Trim();
            }

            var tasks = Enumerable.Range(0, subStudentGroup.Count)
                .Select(i => RunTerraformWithContainerGroupAsync(config, log, lab, isCreate, GetContainerGroupName(i, lab.Name),
                    new Dictionary<string, string>(terraformVariables), subStudentGroup.ElementAt(i)));
            await Task.WhenAll(tasks);
        }

        private static async Task<string> RunTerraformWithContainerGroupAsync(
            Config config,
            ILogger log,
            Lab lab,
            bool isCreate,
            string containerGroupName,
            IDictionary<string, string> terraformVariables, LabCredential[] labCredentials)
        {
            string resourceGroupName = config.GetConfig(Config.Key.TerraformResourceGroupName);
            var azure = await Helper.Azure.Get();
            // Get the resource group's region
            IResourceGroup resGroup = await azure.ResourceGroups.GetByNameAsync(resourceGroupName);
            Region azureRegion = resGroup.Region;

            var containerGroup = await azure.ContainerGroups.GetByResourceGroupAsync(resourceGroupName, containerGroupName);
            if (containerGroup != null)
            {
                log.LogInformation($"Delete existing container group'{containerGroupName}'");
                await azure.ContainerGroups.DeleteByIdAsync(containerGroup.Id);
            }
            log.LogInformation($"Create New container group'{containerGroupName}'");

            var scriptUrl = lab.TerraformRepo.Replace("github.com", "raw.githubusercontent.com") + "/" + lab.Branch + "/" + (isCreate
                 ? "deploy.sh"
                 : "undeploy.sh");

            var commands = $"curl -s {scriptUrl} | bash";

            var containerGroupWithVolume =
                azure.ContainerGroups.Define(containerGroupName)
               .WithRegion(azureRegion)
               .WithExistingResourceGroup(resourceGroupName)
               .WithLinux()
               .WithPrivateImageRegistry(config.GetConfig(Config.Key.AcrUrl), config.GetConfig(Config.Key.AcrUserName), config.GetConfig(Config.Key.AcrPassword))
               .DefineVolume("workspace")
               .WithExistingReadWriteAzureFileShare("containershare")
               .WithStorageAccountName(config.GetConfig(Config.Key.StorageAccountName))
               .WithStorageAccountKey(config.GetConfig(Config.Key.StorageAccountKey))
               .Attach();

            var deploymentDao = new DeploymentDao(config, log);
            IWithNextContainerInstance withNextContainerInstance = null;
            for (var index = 0; index < labCredentials.Length; index++)
            {
                var labCredential = labCredentials[index];
                var deployment = new Deployment()
                {
                    Name = lab.Name,
                    Branch = lab.Branch,
                    Email = labCredential.Email,
                    RepeatTimes = lab.RepeatTimes ?? 0,
                    TerraformRepo = lab.TerraformRepo,
                    Status = "CREATING"
                };
                var token = deployment.GetToken(config.GetConfig(Config.Key.Salt));

                var individualTerraformVariables = new Dictionary<string, string>(terraformVariables) { { "EMAIL", labCredential.Email } };

                var appName = Environment.ExpandEnvironmentVariables("%WEBSITE_SITE_NAME%");
                var callbackUrl = $"https://{appName}.azurewebsites.net/api/CallBackFunction?token={token}";
                individualTerraformVariables.Add("CALLBACK_URL", callbackUrl);

                var previousDeployment = deploymentDao.Get(token);
                if (isCreate && previousDeployment == null)
                {
                    deployment.PartitionKey = token;
                    deployment.RowKey = token;
                    deploymentDao.Add(deployment);
                    withNextContainerInstance = AddContainerInstance(containerGroupWithVolume, withNextContainerInstance, config, commands, index, labCredential, individualTerraformVariables);
                }
                else
                {
                    previousDeployment.Status = "DELETING";
                    deploymentDao.Update(previousDeployment);
                    withNextContainerInstance = AddContainerInstance(containerGroupWithVolume, withNextContainerInstance, config, commands, index, labCredential, individualTerraformVariables);
                }
            }

            if (withNextContainerInstance == null) return "";
            containerGroup = withNextContainerInstance
                .WithRestartPolicy(ContainerGroupRestartPolicy.Never)
                .WithDnsPrefix(containerGroupName)
                .Create();
            log.LogInformation($"Created container group'{containerGroupName}'");

            return containerGroup.Id;

        }


        private static IWithNextContainerInstance AddContainerInstance(IWithVolume containerGroupWithVolume, IWithNextContainerInstance withNextContainerInstance, Config config, string commands,
int index, LabCredential labCredential,
            IDictionary<string, string> terraformVariables)
        {
            var prefixTerraformVariables = terraformVariables.Select(item => ("TF_VAR_" + item.Key, item.Value)).ToDictionary(p => p.Item1, p => p.Item2);

            IWithNextContainerInstance SetContainer(IContainerInstanceDefinitionBlank<IWithNextContainerInstance> container)
            {
                return container.WithImage(config.GetConfig(Config.Key.AcrUrl) + "/terraformazurecli:latest")
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
            var suffix = Regex.Replace(labCredential.Email, @"[^0-9a-zA-Z]+", "-");
            var container = withNextContainerInstance == null ?
                containerGroupWithVolume.DefineContainerInstance("terraformcli-" + suffix) :
                withNextContainerInstance.DefineContainerInstance("terraformcli-" + suffix);

            return SetContainer(container);
        }

    }
}
