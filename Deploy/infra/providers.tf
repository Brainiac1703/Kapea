terraform {
  required_version = ">= 1.7.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 5.1"
    }
  }
}

provider "azurerm" {
  features {}

  subscription_id = var.subscription_id

  # La identidad del flujo sólo tiene permisos sobre el grupo de recursos, no sobre
  # la suscripción, que es corporativa. Registrar proveedores es una operación de
  # suscripción y fallaría por falta de permisos; los que hacen falta ya están
  # registrados, y así queda escrito en Deploy/README.md.
  resource_provider_registrations = "none"
}
