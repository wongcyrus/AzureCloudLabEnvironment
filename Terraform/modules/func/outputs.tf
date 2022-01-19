output "function_app_name" {
  value       = azurerm_function_app.func_function_app.name
  description = "Deployed function app name"
}

output "function_app_id" {
  value       = azurerm_function_app.func_function_app.id
  description = "Deployed function app ID"
}

output "function_app_default_hostname" {
  value       = azurerm_function_app.func_function_app.default_hostname
  description = "Deployed function app hostname"
}

output "function_app_storage_connection" {
  value       = azurerm_function_app.func_function_app.storage_connection_string
  description = "Conenction String for the storage account that needs to be set as AzureWebJobsStorage"
}
