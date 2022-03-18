using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AzureCloudLabEnvironment.Dao;
using AzureCloudLabEnvironment.Helper;
using AzureCloudLabEnvironment.Model;
using Microsoft.Azure.Management.ContainerInstance.Fluent.ContainerGroup.Definition;
using Microsoft.Azure.Management.ContainerInstance.Fluent.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AzureCloudLabEnvironment;

// ReSharper disable once UnusedMember.Global
public class LabEventFunction
{
    [Timeout("00:10:00")]
    [FunctionName(nameof(StartLabEventHandlerFunction))]
    // ReSharper disable once UnusedMember.Global
    public async Task StartLabEventHandlerFunction(
        [QueueTrigger("start-event", Connection = nameof(Config.Key.AzureWebJobsStorage))] Event ev, ILogger log,
        ExecutionContext executionContext)
    {
        var lab = Lab.FromJson(ev.Context, log);
        log.LogInformation($"StartLabEventHandlerFunction Queue trigger function processed: {ev} => {lab}");
        if (lab == null) return;
        await RunClassInfrastructure(log, executionContext, ev, lab, true);
    }

    [Timeout("00:10:00")]
    [FunctionName(nameof(EndLabEventHandlerFunction))]
    public async Task EndLabEventHandlerFunction(
        [QueueTrigger("end-event", Connection = nameof(Config.Key.AzureWebJobsStorage))] Event ev, ILogger log,
        ExecutionContext executionContext)
    {
        var lab = Lab.FromJson(ev.Context, log);
        log.LogInformation($"EndLabEventHandlerFunction Queue trigger function processed: {ev} => {lab}");
        if (lab == null) return;
        await RunClassInfrastructure(log, executionContext, ev, lab, false);
    }

    private static async Task RunClassInfrastructure(ILogger log, ExecutionContext context, Event ev, Lab lab,
        bool isCreate)
    {
        lab.Name = ev.Title;
        lab.Location = ev.Location;
        lab.RepeatedTimes = ev.RepeatTimes;
        lab.Branch = lab.Branch.Replace("###RepeatedTimes###", lab.RepeatedTimes.ToString());
        var action = isCreate ? "Create" : "Delete";
        log.LogInformation($"{action} the lab: {lab}");

        var config = new Config(context);
        var labCredentialDao = new LabCredentialDao(config, log);

        var labVariablesDao = new LabVariablesDao(config, log);
        var foundStudentVariables = await labVariablesDao.LoadVariables(lab);
        log.LogInformation("foundStudentVariables:" + foundStudentVariables);

        var students = labCredentialDao.GetByLab(lab.Name);

        students = students.Select(c =>
        {
            c.Variables = labVariablesDao.GetVariables(c.Email);
            return c;
        }).ToList();

        var terraformVariables = new Dictionary<string, string>
        {
            {"LAB", lab.Name},
            {"LOCATION", lab.Location},
            {"BRANCH", lab.Branch},
            {"REPEAT_TIMES", lab.RepeatedTimes.ToString()}
        };

        var subStudentGroup = students.Chunk(10).ToArray();

        string GetContainerGroupName(int i, string labName)
        {
            return "container-" + Regex.Replace(labName, @"[^0-9a-zA-Z]+", "-").Trim() + "-" + action + "-" + i;
        }

        var tasks = Enumerable.Range(0, subStudentGroup.Length)
            .Select(i => RunTerraformWithContainerGroupAsync(config, log, lab, isCreate,
                GetContainerGroupName(i, lab.Name),
                new Dictionary<string, string>(terraformVariables), subStudentGroup[i]));
        await Task.WhenAll(tasks);
    }

    private static async Task<string> RunTerraformWithContainerGroupAsync(
        Config config,
        ILogger log,
        Lab lab,
        bool isCreate,
        string containerGroupName,
        IDictionary<string, string> terraformVariables, IReadOnlyList<LabCredential> labCredentials)
    {
        var resourceGroupName = config.GetConfig(Config.Key.TerraformResourceGroupName);
        var azure = await Helper.Azure.Get();
        // Get the resource group's region
        var resGroup = await azure.ResourceGroups.GetByNameAsync(resourceGroupName);
        var azureRegion = resGroup.Region;

        var containerGroup = await azure.ContainerGroups.GetByResourceGroupAsync(resourceGroupName, containerGroupName);
        if (containerGroup != null)
        {
            log.LogInformation($"Delete existing container group'{containerGroupName}'");
            await azure.ContainerGroups.DeleteByIdAsync(containerGroup.Id);
        }

        log.LogInformation($"Create New container group'{containerGroupName}'");

        var scriptUrl = lab.GitHubRepo.Replace("github.com", "raw.githubusercontent.com") + "/" + lab.Branch + "/" +
                        (isCreate
                            ? "deploy.sh"
                            : "undeploy.sh");

        var commands = $"curl -s {scriptUrl} | bash";

        var containerGroupWithVolume =
            azure.ContainerGroups.Define(containerGroupName)
                .WithRegion(azureRegion)
                .WithExistingResourceGroup(resourceGroupName)
                .WithLinux()
                .WithPrivateImageRegistry(config.GetConfig(Config.Key.AcrUrl), config.GetConfig(Config.Key.AcrUserName),
                    config.GetConfig(Config.Key.AcrPassword))
                .DefineVolume("workspace")
                .WithExistingReadWriteAzureFileShare("containershare")
                .WithStorageAccountName(config.GetConfig(Config.Key.StorageAccountName))
                .WithStorageAccountKey(config.GetConfig(Config.Key.StorageAccountKey))
                .Attach();

        var deploymentDao = new DeploymentDao(config, log);
        IWithNextContainerInstance withNextContainerInstance = null;
        for (var index = 0; index < labCredentials.Count; index++)
        {
            var labCredential = labCredentials[index];
            var deployment = new Deployment
            {
                Name = lab.Name,
                Location = lab.Location,
                Branch = lab.Branch,
                Email = labCredential.Email,
                LifeCycleHookUrl = lab.LifeCycleHookUrl,
                RepeatedTimes = lab.RepeatedTimes ?? 0,
                GitHubRepo = lab.GitHubRepo,
                Variables = JsonConvert.SerializeObject(labCredential.Variables),
                Output = "{}",
                Status = "CREATING"
            };
            var token = deployment.GetToken(config.GetConfig(Config.Key.Salt));

            var individualTerraformVariables = new Dictionary<string, string>(terraformVariables)
                {{"EMAIL", labCredential.Email}};
            foreach (var labCredentialVariable in labCredential.Variables)
            {
                if (individualTerraformVariables.ContainsKey(labCredentialVariable.Key))
                    individualTerraformVariables[labCredentialVariable.Key] = labCredentialVariable.Value;
                else
                    individualTerraformVariables.Add(labCredentialVariable.Key, labCredentialVariable.Value);
            }
            var appName = Environment.ExpandEnvironmentVariables("%WEBSITE_SITE_NAME%");
            var callbackUrl = $"https://{appName}.azurewebsites.net/api/CallBackFunction?token={token}";
            individualTerraformVariables.Add("CALLBACK_URL", callbackUrl);

            var previousDeployment = deploymentDao.Get(token);
            if (isCreate)
            {
                if (previousDeployment != null)
                {
                    log.LogInformation($"Skip to create repeated deployment {token} '{previousDeployment}' <=> {deployment}'");
                    continue;
                };
                deployment.PartitionKey = token;
                deployment.RowKey = token;

                var externalVariables = await LifeCycleHook.SendCallBack(deployment, log);
                foreach (var key in externalVariables.Keys.Where(key => !individualTerraformVariables.ContainsKey(key)))
                {
                    individualTerraformVariables.Add(key, externalVariables[key]);
                }
                deployment.ExternalVariables = JsonConvert.SerializeObject(externalVariables);
                deploymentDao.Add(deployment);

                withNextContainerInstance = AddContainerInstance(containerGroupWithVolume, withNextContainerInstance,
                    config, commands, index, labCredential, individualTerraformVariables);

            }
            else
            {
                if (previousDeployment != null)
                {
                    previousDeployment.Status = "DELETING";
                    deploymentDao.Update(previousDeployment);
                    await LifeCycleHook.SendCallBack(previousDeployment, log);
                    var externalVariables = JsonConvert.DeserializeObject<Dictionary<string, string>>(previousDeployment.ExternalVariables);
                    log.LogInformation(previousDeployment.ExternalVariables);
                    foreach (var key in externalVariables.Keys.Where(key => !individualTerraformVariables.ContainsKey(key)))
                    {
                        log.LogInformation("add" + key + "->" + externalVariables[key]);
                        individualTerraformVariables.Add(key, externalVariables[key]);
                    }
                }
                withNextContainerInstance = AddContainerInstance(containerGroupWithVolume, withNextContainerInstance,
                    config, commands, index, labCredential, individualTerraformVariables);
            }
        }

        if (withNextContainerInstance == null)
        {
            log.LogInformation($"No ContainerInstance and skip to create '{containerGroupName}'");
            return "";
        }
        containerGroup = withNextContainerInstance
            .WithRestartPolicy(ContainerGroupRestartPolicy.Never)
            .WithDnsPrefix(containerGroupName)
            .Create();
        log.LogInformation($"Created container group'{containerGroupName}'");

        return containerGroup.Id;
    }


    private static IWithNextContainerInstance AddContainerInstance(IWithVolume containerGroupWithVolume,
        IWithNextContainerInstance withNextContainerInstance, Config config, string commands,
        int index, LabCredential labCredential,
        IDictionary<string, string> terraformVariables)
    {
        var prefixTerraformVariables = terraformVariables.Select(item => ("TF_VAR_" + item.Key, item.Value))
            .ToDictionary(p => p.Item1, p => p.Item2);

        IWithNextContainerInstance SetContainer(IContainerInstanceDefinitionBlank<IWithNextContainerInstance> container)
        {
            return container.WithImage(config.GetConfig(Config.Key.AcrUrl) + "/terraformazurecli:latest")
                .WithExternalTcpPort(80 + index)
                .WithMemorySizeInGB(0.5)
                .WithCpuCoreCount(0.25)
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
        var container = withNextContainerInstance == null
            ? containerGroupWithVolume.DefineContainerInstance("terraformcli-" + suffix)
            : withNextContainerInstance.DefineContainerInstance("terraformcli-" + suffix);

        return SetContainer(container);
    }
}