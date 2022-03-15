### INPUT VARs ###
variable "APP_NAME" {}
variable "PREFIX" {}
variable "ENVIRONMENT" {}
variable "LOCATION" {}
variable "RESOURCE_GROUP" {}
variable "STORAGE_ACC_NAME" {}
variable "STORAGE_ACC_KEY" {}
variable "STORAGE_CONNECTION_STRING" {}
variable "DEPLOYMENTS_NAME" {}
variable "CALENDAR_URL" {}
variable "EMAIL_SMTP" {}
variable "EMAIL_USERNAME" {}
variable "EMAIL_PASSWORD" {}
variable "EMAIL_FROM_ADDRESS" {}
variable "ADMIN_EMAIL" {}
variable "FUNCTION_APP_FOLDER" {
  default = "../../../AzureCloudLabEnvironmentFunctionApp"
}
variable "FUNCTION_APP_PUBLISH_FOLDER" {
  default = "../../../AzureCloudLabEnvironmentFunctionApp/bin/Release/net6.0/publish"
}