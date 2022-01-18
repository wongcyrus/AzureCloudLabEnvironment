resource "azurerm_resource_group" "func-rg" {
  name     = var.RESOURCE_GROUP
  location = var.LOCATION
}

resource "random_string" "prefix" {
  length  = 4
  special = false
  lower   = true
  upper   = false
}

resource "random_string" "storage_name" {
  length  = 24
  upper   = false
  lower   = true
  number  = true
  special = false
}

resource "azurerm_storage_account" "storage" {
  name                     = random_string.storage_name.result
  resource_group_name      = azurerm_resource_group.func-rg.name
  location                 = var.LOCATION
  account_tier             = "Standard"
  account_replication_type = "LRS"
}

resource "azurerm_storage_container" "deployments" {
  name                  = "function-releases"
  storage_account_name  = azurerm_storage_account.storage.name
  container_access_type = "private"
}

data "archive_file" "azure_function_deployment_package" {
  type        = "zip"
  source_dir  = var.FUNCTION_APP_CODE_FOLDER
  output_path = "${var.FUNCTION_APP_CODE_FOLDER}/../deployment.zip"
}

resource "azurerm_storage_blob" "appcode" {
  name                   = "functionapp.zip"
  storage_account_name   = azurerm_storage_account.storage.name
  storage_container_name = azurerm_storage_container.deployments.name
  type                   = "Block"
  source                 = data.archive_file.azure_function_deployment_package.output_path
  depends_on = [
    data.archive_file.azure_function_deployment_package
  ]
}

data "azurerm_storage_account_sas" "sas" {
  connection_string = azurerm_storage_account.storage.primary_connection_string
  https_only        = true
  start             = "2022-01-01"
  expiry            = "2028-12-31"
  resource_types {
    object    = true
    container = false
    service   = false
  }
  services {
    blob  = true
    queue = false
    table = false
    file  = false
  }
  permissions {
    read    = true
    write   = false
    delete  = false
    list    = false
    add     = false
    create  = false
    update  = false
    process = false
  }
}

module "func" {
  source                    = "./modules/func"
  LOCATION                  = var.LOCATION
  RESOURCE_GROUP            = var.RESOURCE_GROUP
  ENVIRONMENT               = var.ENVIRONMENT
  PREFIX                    = random_string.prefix.result
  STORAGE_ACC_NAME          = azurerm_storage_account.storage.name
  STORAGE_ACC_KEY           = azurerm_storage_account.storage.primary_access_key
  STORAGE_CONNECTION_STRING = azurerm_storage_account.storage.primary_blob_connection_string
  DEPLOYMENTS_NAME          = azurerm_storage_container.deployments.name
  SAS                       = data.azurerm_storage_account_sas.sas.sas
  HASH                      = data.archive_file.azure_function_deployment_package.output_base64sha256
  TIME_ZONE                 = var.TIME_ZONE
  depends_on                = [azurerm_resource_group.func-rg]
}

resource "local_file" "output" {
  content = jsonencode({
    "app_functions" : {
      "name" : module.func.function_app_name,
      "id" : module.func.function_app_id,
      "hostname" : module.func.function_app_default_hostname,
      "storage_account" : replace(module.func.function_app_storage_connection, "/", "\\/"),
    }
  })
  filename = "../temp_infra/func.json"
}
