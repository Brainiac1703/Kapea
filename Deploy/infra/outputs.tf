output "openai_endpoint" {
  description = "Punto de acceso de Azure OpenAI. Va en AzureOpenAi__Endpoint."
  value       = azurerm_cognitive_account.openai.endpoint
}

output "openai_deployment" {
  description = "Nombre del despliegue del modelo. Va en AzureOpenAi__Deployment."
  value       = azurerm_cognitive_deployment.model.name
}

output "openai_key" {
  description = <<-EOT
    Clave del servicio, para configurarlo en una máquina de desarrollo. La
    aplicación desplegada no la usa: llama con su identidad.
  EOT
  value       = azurerm_cognitive_account.openai.primary_access_key
  sensitive   = true
}

output "resource_group" {
  value = azurerm_resource_group.main.name
}

output "api_url" {
  description = "URL pública de la API, vacía si aún no se ha desplegado."
  value       = var.deploy_app ? "https://${azurerm_container_app.api[0].ingress[0].fqdn}" : ""
}

output "container_registry" {
  description = "Servidor del registro de contenedores, para el pipeline."
  value       = var.deploy_app ? azurerm_container_registry.main[0].login_server : ""
}

output "api_app_name" {
  description = "Nombre de la aplicación de la API, para actualizar su revisión."
  value       = var.deploy_app ? azurerm_container_app.api[0].name : ""
}

output "sync_app_name" {
  description = "Nombre de la aplicación del trabajador, para actualizar su revisión."
  value       = var.deploy_app ? azurerm_container_app.sync[0].name : ""
}

output "sql_server_fqdn" {
  description = "Nombre del servidor SQL, para aplicar migraciones desde el pipeline."
  value       = var.deploy_app ? azurerm_mssql_server.main[0].fully_qualified_domain_name : ""
}

output "sql_database_name" {
  value = var.deploy_app ? azurerm_mssql_database.main[0].name : ""
}

output "app_identity_name" {
  description = <<-EOT
    Nombre de la identidad de la aplicación, que es el del usuario dentro de la
    base de datos. Al ser una identidad asignada por el usuario no coincide con
    el nombre de ninguna de las dos aplicaciones, así que el pipeline tiene que
    leerlo de aquí y no darlo por sabido.
  EOT
  value       = var.deploy_app ? azurerm_user_assigned_identity.app[0].name : ""
}

output "app_identity_client_id" {
  description = <<-EOT
    client_id de la identidad de la aplicación. El pipeline lo necesita para
    crear el usuario de base de datos por SID: el SID de una identidad
    administrada es su client_id. Así el servidor SQL no tiene que resolver el
    nombre contra Entra, que exigiría concederle el rol Directory Readers.
  EOT
  value       = var.deploy_app ? azurerm_user_assigned_identity.app[0].client_id : ""
}

output "key_vault_uri" {
  description = "Dirección del almacén de secretos. Va en KeyVault__Uri."
  value       = var.deploy_app ? azurerm_key_vault.main[0].vault_uri : ""
}
