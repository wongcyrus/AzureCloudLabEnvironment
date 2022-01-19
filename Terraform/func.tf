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
