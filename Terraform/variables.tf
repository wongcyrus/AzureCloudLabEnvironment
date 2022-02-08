# SECRETS, PLEASE PROVIDE THESE VALUES IN A TFVARS FILE
variable "SUBSCRIPTION_ID" {}
variable "TENANT_ID" {}
variable "CALENDAR_URL" {}
variable "SMTP" {}
variable "EMAIL_USERNAME" {}
variable "EMAIL_PASSWORD" {}
variable "EMAIL_FROM_ADDRESS" {}

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
variable "CALENDAR_TIME_ZONE" {
  default = "China Standard Time"
}

