resource "azurerm_resource_group" "main" {
  name     = "rg-${var.project}"
  location = var.location
  tags     = var.tags
}

resource "azurerm_cognitive_account" "openai" {
  name                  = "oai-${var.project}"
  location              = azurerm_resource_group.main.location
  resource_group_name   = azurerm_resource_group.main.name
  kind                  = "OpenAI"
  sku_name              = "S0"
  custom_subdomain_name = "oai-${var.project}"

  # La aplicación desplegada llama con su identidad, pero este servicio existe
  # también para desarrollar en local, y una máquina de desarrollo no tiene
  # identidad administrada con la que pedir un token. Se deja la clave
  # habilitada a propósito.
  local_auth_enabled = true

  tags = var.tags
}

resource "azurerm_cognitive_deployment" "model" {
  name                 = var.model.name
  cognitive_account_id = azurerm_cognitive_account.openai.id

  model {
    format  = "OpenAI"
    name    = var.model.name
    version = var.model.version
  }

  sku {
    name     = "Standard"
    capacity = var.model.capacity
  }
}
