import { Construct } from "constructs";
import { App, TerraformOutput, TerraformStack } from "cdktf";
import { AzurermProvider, ResourceGroup, StorageAccount, StorageQueue, StorageTable, StorageContainer, StorageShare, RoleDefinition, RoleAssignment} from "cdktf-azure-providers/.gen/providers/azurerm";
import { StringResource } from 'cdktf-azure-providers/.gen/providers/random'
import { AzureFunctionLinuxConstruct, PublishMode } from "azure-common-construct/patterns/AzureFunctionLinuxConstruct";
import { AzureStaticConstainerConstruct } from "azure-common-construct/patterns/AzureStaticConstainerConstruct";

import * as path from "path";
import * as dotenv from 'dotenv';
dotenv.config({ path: __dirname + '/.env' });

class AzureCloudLabEnvironmentStack extends TerraformStack {
  constructor(scope: Construct, name: string) {
    super(scope, name);

    new AzurermProvider(this, "AzureRm", {
      features: {}
    })

    const prefix = "AzureCloudLab"
    const environment = "dev"

    const resourceGroup = new ResourceGroup(this, "ResourceGroup", {
      location: "EastAsia",
      name: prefix + "ResourceGroup"
    })
    const terraformResourceGroup = new ResourceGroup(this, "TerraformResourceGroupName", {
      location: "EastAsia",
      name: prefix + "TerraformResourceGroup"
    })

    const azureStaticConstainerConstruct = new AzureStaticConstainerConstruct(this, "AzureStaticConstainerConstruct", {
      environment,
      prefix,
      resourceGroup,
      gitHubUserName: "wongcyrus",
      gitHubRepo: "terraform-azure-cli",
      gitAccessToken: "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",
      branch: "master",
      dockerBuildArguments: {
        "AZURE_CLI_VERSION": "2.37.0",
        "TERRAFORM_VERSION": "1.2.2"
      }
    })

    const suffix = new StringResource(this, "Random", {
      length: 5,
      special: false,
      lower: true,
      upper: false,
    })
    const storageAccount = new StorageAccount(this, "StorageAccount", {
      name: prefix.toLocaleLowerCase() + environment.toLocaleLowerCase() + suffix.result,
      location: resourceGroup.location,
      resourceGroupName: resourceGroup.name,
      accountTier: "Standard",
      accountReplicationType: "LRS"
    })

    const tables = ["OnGoingEvent", "CompletedEvent", "LabCredential", "Deployment", "ErrorLog", "Subscription"];
    tables.map(t => new StorageTable(this, t + "StorageTable", {
      name: t,
      storageAccountName: storageAccount.name
    }))

    const queues = ["start-event", "end-event"];
    queues.map(q =>
      new StorageQueue(this, q + "StorageQueue", {
        name: q,
        storageAccountName: storageAccount.name
      })
    )

    new StorageContainer(this, "LabVariables", {
      name: "lab-variables",
      storageAccountName: storageAccount.name
    })

    new StorageShare(this, "containershare", {
      name: "containershare",
      storageAccountName: storageAccount.name,
      quota: 500,
      accessTier: "Hot"
    })

    

    const appSettings = {
      "TerraformResourceGroupName": terraformResourceGroup.name,
      "AcrUserName": azureStaticConstainerConstruct.containerRegistry.adminUsername,
      "AcrPassword": azureStaticConstainerConstruct.containerRegistry.adminPassword,
      "AcrUrl": azureStaticConstainerConstruct.containerRegistry.loginServer,
      "CalendarUrl": process.env.CALENDAR_URL!,      
      "EmailSmtp": process.env.EMAIL_SMTP!,
      "CommunicationServiceConnectionString":process.env.COMMUNICATION_SERVICE_CONNECTION_STRING!,
      "EmailUserName": process.env.EMAIL_USERNAME!,
      "EmailPassword": process.env.EMAIL_PASSWORD!,
      "EmailFromAddress": process.env.EMAIL_FROM_ADDRESS!,
      "AdminEmail": process.env.ADMIN_EMAIL!,
      "Salt": prefix,
      "StorageAccountName": storageAccount.name,
      "StorageAccountKey": storageAccount.primaryAccessKey,
      "StorageAccountConnectionString": storageAccount.primaryConnectionString
    }

    const azureFunctionConstruct = new AzureFunctionLinuxConstruct(this, "AzureFunctionConstruct", {
      functionAppName: `ive-virtual-cloud-lab-${environment}-function-app`,
      environment,
      prefix,
      resourceGroup,
      appSettings,
      vsProjectPath: path.join(__dirname, "..", "AzureCloudLabEnvironmentFunctionApp/"),
      publishMode: PublishMode.AfterCodeChange
    })

    const runAciRoleDefinition = new RoleDefinition(this, "RunAciRoleDefinition", {
      name: prefix + environment + "run_azure_container_instance",
      scope: terraformResourceGroup.id,
      permissions: [{
        actions: ["Microsoft.Resources/subscriptions/resourcegroups/read",
          "Microsoft.ContainerInstance/containerGroups/read",
          "Microsoft.ContainerInstance/containerGroups/write",
          "Microsoft.ContainerInstance/containerGroups/delete"], notActions: []
      }],
      assignableScopes: [terraformResourceGroup.id]
    })

    new RoleAssignment(this, "RoleAssignment", {
      scope: terraformResourceGroup.id,
      roleDefinitionId: runAciRoleDefinition.roleDefinitionResourceId,
      principalId: azureFunctionConstruct.functionApp.identity.principalId
    })

    new TerraformOutput(this, "FunctionAppHostname", {
      value: azureFunctionConstruct.functionApp.name
    });


    new TerraformOutput(this, "AzureFunctionBaseUrl", {
      value: `https://${azureFunctionConstruct.functionApp.name}.azurewebsites.net`
    });

  }
}

const app = new App({ skipValidation: true });
new AzureCloudLabEnvironmentStack(app, "infrastructure");
app.synth();
