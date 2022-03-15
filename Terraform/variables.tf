# SECRETS, PLEASE PROVIDE THESE VALUES IN A TFVARS FILE
variable "APP_NAME" {}
variable "SUBSCRIPTION_ID" {}
variable "TENANT_ID" {}
variable "CALENDAR_URL" {}
variable "EMAIL_SMTP" {}
variable "EMAIL_USERNAME" {}
variable "EMAIL_PASSWORD" {}
variable "EMAIL_FROM_ADDRESS" {}
variable "ADMIN_EMAIL" {}

# GLOBAL VARIABLES
variable "RESOURCE_GROUP" {
  default = "azure-cloud-lab-environment"
}
variable "ENVIRONMENT" {
  default = "dev"
}
variable "LOCATION" {
  default = "EastAsia"
}

