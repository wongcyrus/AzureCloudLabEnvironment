# SECRETS, PLEASE PROVIDE THESE VALUES IN A TFVARS FILE
variable "SUBSCRIPTION_ID" {}
variable "TENANT_ID" {}

# GLOBAL VARIABLES
variable "RESOURCE_GROUP" {
  default = "func-rg"
}
variable "ENVIRONMENT" {
  default = "dev"
}
variable "LOCATION" {
  default = "EastAsia"
}
variable "TIME_ZONE" {
  default = "China Standard Time"
}
