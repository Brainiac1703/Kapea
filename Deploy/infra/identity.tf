# ---------------------------------------------------------------------------
# Identidad de la aplicación.
#
# Asignada por el usuario, no por el sistema, y creada antes que las
# aplicaciones. La diferencia no es estética: una identidad de sistema nace con
# la aplicación, así que su principal_id no existe hasta que el recurso está
# creado y los roles solo se pueden asignar después. Pero Azure no termina de
# aprovisionar una Container App hasta poder autenticarse contra el registro,
# que necesita AcrPull. El ciclo se cierra y el aprovisionamiento se queda
# esperando indefinidamente.
#
# Creándola por separado, los permisos existen antes que la aplicación y el
# ciclo desaparece. Es además un solo sujeto al que conceder y del que retirar:
# la misma identidad lee el registro, el almacén de secretos, las claves de
# sesión, la base de datos y el servicio de modelos.
# ---------------------------------------------------------------------------

resource "azurerm_user_assigned_identity" "app" {
  count = var.deploy_app ? 1 : 0

  name                = "id-${var.project}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  tags                = var.tags
}

# Las dos aplicaciones leen el registro con esa identidad, sin credenciales de
# administrador.
resource "azurerm_role_assignment" "acr_pull" {
  count = var.deploy_app ? 1 : 0

  scope                = azurerm_container_registry.main[0].id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_user_assigned_identity.app[0].principal_id
}

# Y llaman al servicio de modelos con ella, sin clave en configuración.
resource "azurerm_role_assignment" "openai_user" {
  count = var.deploy_app ? 1 : 0

  scope                = azurerm_cognitive_account.openai.id
  role_definition_name = "Cognitive Services OpenAI User"
  principal_id         = azurerm_user_assigned_identity.app[0].principal_id
}

# Officer y no User: la aplicación no solo lee credenciales de brókeres, también
# las da de alta cuando el usuario las introduce.
resource "azurerm_role_assignment" "secrets_officer" {
  count = var.deploy_app ? 1 : 0

  scope                = azurerm_key_vault.main[0].id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = azurerm_user_assigned_identity.app[0].principal_id
}

# Descifra y vuelve a cifrar con la clave del almacén las claves que protegen la
# cookie de sesión. Crypto User permite usar la clave, no gestionarla.
resource "azurerm_role_assignment" "crypto_user" {
  count = var.deploy_app ? 1 : 0

  scope                = azurerm_key_vault_key.data_protection[0].resource_versionless_id
  role_definition_name = "Key Vault Crypto User"
  principal_id         = azurerm_user_assigned_identity.app[0].principal_id
}

# Escribe el anillo de claves de sesión en el contenedor de blobs. Contributor y
# no Reader: la primera vez tiene que crear el blob.
resource "azurerm_role_assignment" "keys_blob" {
  count = var.deploy_app ? 1 : 0

  scope                = azurerm_storage_container.data_protection[0].id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_user_assigned_identity.app[0].principal_id
}
