# ---------------------------------------------------------------------------
# Almacén de secretos y sitio donde viven las claves de sesión.
#
# El almacén es la implementación de producción del puerto ISecretStore, que en
# desarrollo guarda en un fichero de user secrets. Guarda dos cosas distintas:
# las credenciales de los brókeres, que da de alta la propia aplicación cuando
# el usuario las introduce, y el identificador y el secreto de Google, que da
# de alta el pipeline.
# ---------------------------------------------------------------------------

data "azurerm_client_config" "current" {}

resource "azurerm_key_vault" "main" {
  count = var.deploy_app ? 1 : 0

  name                = "kv-${var.project}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  tenant_id           = data.azurerm_client_config.current.tenant_id
  sku_name            = "standard"

  # Permisos por roles de Azure y no por políticas de acceso del propio
  # almacén: así el acceso se concede y se retira donde se concede todo lo
  # demás, y no hay dos sitios que consultar para saber quién puede leer qué.
  rbac_authorization_enabled = true

  # Un almacén borrado se puede recuperar durante una semana. Es el mínimo que
  # admite Azure y lo suficiente para deshacer un borrado por error.
  soft_delete_retention_days = 7

  # Sin purge protection: es una instalación personal y poder destruir el
  # entorno con terraform destroy pesa más que impedir un borrado definitivo.
  purge_protection_enabled = false

  tags = var.tags
}

# La identidad que aplica la infraestructura necesita dar de alta secretos (los
# de Google) y crear la clave de cifrado. No los hereda por ser dueña del
# almacén: con permisos por roles, crear el recurso y usar su contenido son dos
# cosas distintas.
resource "azurerm_role_assignment" "infra_secrets_officer" {
  count = var.deploy_app ? 1 : 0

  scope                = azurerm_key_vault.main[0].id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = data.azurerm_client_config.current.object_id
}

resource "azurerm_role_assignment" "infra_crypto_officer" {
  count = var.deploy_app ? 1 : 0

  scope                = azurerm_key_vault.main[0].id
  role_definition_name = "Key Vault Crypto Officer"
  principal_id         = data.azurerm_client_config.current.object_id
}

# Protege el anillo de claves de sesión que se guarda en el blob, para que quien
# pueda leer el blob no pueda además descifrar las cookies.
resource "azurerm_key_vault_key" "data_protection" {
  count = var.deploy_app ? 1 : 0

  name         = "data-protection"
  key_vault_id = azurerm_key_vault.main[0].id
  key_type     = "RSA"
  key_size     = 2048

  key_opts = ["unwrapKey", "wrapKey"]

  # Una asignación de rol tarda en propagarse, y sin esperarla la creación de la
  # clave falla por falta de permisos aunque el rol ya esté declarado. El
  # depends_on ordena las operaciones; si aun así fallara, basta con volver a
  # aplicar.
  depends_on = [azurerm_role_assignment.infra_crypto_officer]
}

# ---------------------------------------------------------------------------
# Claves que cifran la cookie de sesión.
#
# En local viven en un volumen. En Azure cada revisión es un contenedor nuevo:
# sin almacén externo, publicar cerraría la sesión de quien estuviera dentro.
# ---------------------------------------------------------------------------

resource "azurerm_storage_account" "main" {
  count = var.deploy_app ? 1 : 0

  name                     = "st${var.project}"
  location                 = azurerm_resource_group.main.location
  resource_group_name      = azurerm_resource_group.main.name
  account_tier             = "Standard"
  account_replication_type = "LRS"
  min_tls_version          = "TLS1_2"

  # Nadie se autentica con clave contra esta cuenta: la aplicación usa su
  # identidad y el pipeline la suya.
  shared_access_key_enabled = false

  tags = var.tags
}

resource "azurerm_storage_container" "data_protection" {
  count = var.deploy_app ? 1 : 0

  name                  = "data-protection"
  storage_account_id    = azurerm_storage_account.main[0].id
  container_access_type = "private"
}
