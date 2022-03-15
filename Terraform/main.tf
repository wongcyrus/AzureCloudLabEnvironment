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

resource "azurerm_storage_container" "lab-variables" {
  name                  = "lab-variables"
  storage_account_name  = azurerm_storage_account.storage.name
  container_access_type = "private"
}

resource "azurerm_storage_table" "on_going_events" {
  name                 = "OnGoingEvent"
  storage_account_name = azurerm_storage_account.storage.name
}

resource "azurerm_storage_table" "completed_events" {
  name                 = "CompletedEvent"
  storage_account_name = azurerm_storage_account.storage.name
}

resource "azurerm_storage_table" "lab_credential" {
  name                 = "LabCredential"
  storage_account_name = azurerm_storage_account.storage.name
}

resource "azurerm_storage_table" "deployment" {
  name                 = "Deployment"
  storage_account_name = azurerm_storage_account.storage.name
}

resource "azurerm_storage_table" "error_log" {
  name                 = "ErrorLog"
  storage_account_name = azurerm_storage_account.storage.name
}

resource "azurerm_storage_table" "subscriptions" {
  name                 = "Subscription"
  storage_account_name = azurerm_storage_account.storage.name
}

resource "azurerm_storage_queue" "start_event" {
  name                 = "start-event"
  storage_account_name = azurerm_storage_account.storage.name
}

resource "azurerm_storage_queue" "end_event" {
  name                 = "end-event"
  storage_account_name = azurerm_storage_account.storage.name
}

resource "azurerm_storage_share" "container_share" {
  name                 = "containershare"
  storage_account_name = azurerm_storage_account.storage.name
  quota                = 500
}

module "func" {
  source                    = "./modules/func"
  APP_NAME                  = var.APP_NAME
  LOCATION                  = var.LOCATION
  RESOURCE_GROUP            = azurerm_resource_group.func-rg
  ENVIRONMENT               = var.ENVIRONMENT
  PREFIX                    = random_string.prefix.result
  STORAGE_ACC_NAME          = azurerm_storage_account.storage.name
  STORAGE_ACC_KEY           = azurerm_storage_account.storage.primary_access_key
  STORAGE_CONNECTION_STRING = azurerm_storage_account.storage.primary_blob_connection_string
  DEPLOYMENTS_NAME          = azurerm_storage_container.deployments.name
  CALENDAR_URL              = var.CALENDAR_URL
  EMAIL_SMTP                = var.EMAIL_SMTP
  EMAIL_USERNAME            = var.EMAIL_USERNAME
  EMAIL_PASSWORD            = var.EMAIL_PASSWORD
  EMAIL_FROM_ADDRESS        = var.EMAIL_FROM_ADDRESS
  ADMIN_EMAIL               = var.ADMIN_EMAIL
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
