resource "azurerm_application_insights" "func_application_insights" {
  name                = "func-application-insights"
  location            = var.LOCATION
  resource_group_name = var.RESOURCE_GROUP.name
  application_type    = "other"
}

resource "azurerm_app_service_plan" "func_app_service_plan" {
  name                = "func-app-service-plan"
  location            = var.LOCATION
  resource_group_name = var.RESOURCE_GROUP.name
  kind                = "FunctionApp"
  reserved            = true
  sku {
    tier = "Dynamic"
    size = "Y1"
  }
}

resource "null_resource" "function_app_build_publish" {
  provisioner "local-exec" {
    working_dir = abspath("${path.module}/${var.FUNCTION_APP_FOLDER}")
    command     = "dotnet publish -p:PublishProfile=FolderProfile"
  }
  triggers = {
    dir_sha1 = sha1(join("", [for f in fileset(abspath("${path.module}/${var.FUNCTION_APP_FOLDER}"), "*.cs") : filemd5(abspath("${path.module}/${var.FUNCTION_APP_FOLDER}/${f}"))]))
  }
}

data "archive_file" "azure_function_deployment_package" {
  type        = "zip"
  source_dir  = abspath("${path.module}/${var.FUNCTION_APP_PUBLISH_FOLDER}")
  output_path = abspath("${path.module}/${var.FUNCTION_APP_PUBLISH_FOLDER}/../deployment.zip")
  depends_on = [
    null_resource.function_app_build_publish
  ]
}

resource "azurerm_function_app" "func_function_app" {
  name                = "${var.PREFIX}-${var.ENVIRONMENT}-function-app"
  location            = var.LOCATION
  resource_group_name = var.RESOURCE_GROUP.name
  app_service_plan_id = azurerm_app_service_plan.func_app_service_plan.id
  app_settings = {
    FUNCTIONS_WORKER_RUNTIME       = "dotnet",
    AzureWebJobsStorage            = var.STORAGE_CONNECTION_STRING,
    APPINSIGHTS_INSTRUMENTATIONKEY = azurerm_application_insights.func_application_insights.instrumentation_key,
    WEBSITE_RUN_FROM_PACKAGE       = "1"
    CalendarUrl                    = var.CALENDAR_URL
    CalendarTimeZone               = var.CALENDAR_TIME_ZONE
  }
  os_type                    = "linux"
  storage_account_name       = var.STORAGE_ACC_NAME
  storage_account_access_key = var.STORAGE_ACC_KEY
  version                    = "~4"
  identity {
    type = "SystemAssigned"
  }
}

data "azurerm_subscription" "primary" {}

resource "azurerm_role_definition" "run_azure_container_instance" {
  name  = "run_azure_container_instance"
  scope = data.azurerm_subscription.primary.id

  permissions {
    actions = [
      "Microsoft.ContainerRegistry/registries/push/write",
      "Microsoft.ContainerRegistry/registries/pull/read",
      "Microsoft.ContainerRegistry/registries/read",
      "Microsoft.ContainerRegistry/registries/importImage/action"
    ]
    not_actions = []
  }

  assignable_scopes = [
    data.azurerm_subscription.primary.id
  ]
}

resource "azurerm_role_assignment" "example" {
  scope              = data.azurerm_subscription.primary.id
  role_definition_id = azurerm_role_definition.run_azure_container_instance.role_definition_resource_id
  principal_id       = azurerm_function_app.func_function_app.identity.0.principal_id
}

locals {
  publish_code_command = "az functionapp deployment source config-zip --resource-group ${var.RESOURCE_GROUP.name} --name ${azurerm_function_app.func_function_app.name} --src ${data.archive_file.azure_function_deployment_package.output_path}"
}

resource "null_resource" "function_app_publish" {
  provisioner "local-exec" {
    command = local.publish_code_command
  }
  depends_on = [local.publish_code_command, data.archive_file.azure_function_deployment_package]
  triggers = {
    source_md5           = filemd5(data.archive_file.azure_function_deployment_package.output_path)
    publish_code_command = local.publish_code_command
  }
}

# data "azurerm_user_assigned_identity" "assigned_identity_acr_pull" {
#   provider            = azurerm.acr_sub
#   name                = "User_ACR_pull"
#   resource_group_name = var.RESOURCE_GROUP.name
# }

resource "azurerm_container_registry" "acr" {
  name                = "${var.PREFIX}TerraformContainerRegistry"
  resource_group_name = var.RESOURCE_GROUP.name
  location            = var.LOCATION
  sku                 = "Standard"
  admin_enabled       = true
}

resource "azurerm_container_registry_task" "build_terraform_image_task" {
  name                  = "build_terraform_image_task"
  container_registry_id = azurerm_container_registry.acr.id
  platform {
    os = "Linux"
  }
  docker_step {
    dockerfile_path      = "Dockerfile"
    context_path         = "https://github.com/wongcyrus/terraform-azure-cli#master"
    context_access_token = "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"
    image_names          = ["terraformazurecli:latest"]
    arguments ={
      AZURE_CLI_VERSION = "2.32.0"
      TERRAFORM_VERSION = "1.1.4"
    }
  }
}

data "http" "docker_file" {
  url = "https://raw.githubusercontent.com/wongcyrus/terraform-azure-cli/master/Dockerfile"
}

resource "null_resource" "run_arc_task" {
  provisioner "local-exec" {
    command = "az acr task run --registry ${azurerm_container_registry.acr.name} --name build_terraform_image_task"
  }
  depends_on = [azurerm_container_registry_task.build_terraform_image_task]
  triggers = {
    dockerfile = data.http.docker_file.body
  }
}

