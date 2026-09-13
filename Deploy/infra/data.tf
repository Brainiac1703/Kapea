# ---------------------------------------------------------------------------
# Base de datos.
#
# Va dentro de var.deploy_app porque en desarrollo se usa el SQL Server que
# levanta docker compose: no hace falta pagar una base de datos en Azure para
# trabajar en local.
# ---------------------------------------------------------------------------

resource "azurerm_mssql_server" "main" {
  count = var.deploy_app ? 1 : 0

  name                = "sql-${var.project}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  version             = "12.0"
  minimum_tls_version = "1.2"

  azuread_administrator {
    login_username = var.sql_admin.login
    object_id      = var.sql_admin.object_id

    # Deshabilita por completo la autenticación por usuario y contraseña. Sin
    # esto el servidor aceptaría credenciales SQL, que es justo el secreto que
    # el proyecto evita en todas las demás capas: sin contraseña no hay
    # contraseña que rotar ni que filtrar.
    azuread_authentication_only = true
  }

  tags = var.tags

  lifecycle {
    precondition {
      condition     = var.sql_admin != null
      error_message = <<-EOT
        Con deploy_app activado hay que indicar sql_admin: la identidad de Entra
        que administra la base de datos, que es la que el pipeline usa para
        aplicar migraciones y para dar de alta la identidad de la aplicación.
        Los valores están en Deploy/README.md.
      EOT
    }
  }
}

resource "azurerm_mssql_database" "main" {
  count = var.deploy_app ? 1 : 0

  name      = "Kapea"
  server_id = azurerm_mssql_server.main[0].id

  sku_name                    = var.database.sku_name
  min_capacity                = var.database.min_capacity
  auto_pause_delay_in_minutes = var.database.auto_pause_minutes
  max_size_gb                 = var.database.max_size_gb

  collation = "SQL_Latin1_General_CP1_CI_AS"

  # Sin redundancia de zona y con copias locales: es lo barato, y los datos de
  # esta base se pueden reconstruir volviendo a importar de las plataformas.
  zone_redundant       = false
  storage_account_type = "Local"

  tags = var.tags
}

# Las Container Apps salen a internet con IP variable, así que no se puede fijar
# un rango. Esta regla especial —origen y destino 0.0.0.0— es la que Azure
# interpreta como "permitir servicios de Azure", no como "permitir todo".
resource "azurerm_mssql_firewall_rule" "azure_services" {
  count = var.deploy_app ? 1 : 0

  name             = "permitir-servicios-de-azure"
  server_id        = azurerm_mssql_server.main[0].id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}
