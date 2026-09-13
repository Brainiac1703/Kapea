# ---------------------------------------------------------------------------
# Publicación de la aplicación.
#
# Todo lo de este fichero va dentro de var.deploy_app, para poder aprovisionar
# primero solo el servicio de modelos y desarrollar en local con él.
#
# Son dos procesos y dos aplicaciones: la API, que sirve además los estáticos
# del cliente WebAssembly, con entrada pública; y el trabajador de
# sincronización, sin entrada y con una sola réplica. La imagen es la misma
# construida dos veces, una por cada destino del Dockerfile.
# ---------------------------------------------------------------------------

resource "azurerm_log_analytics_workspace" "main" {
  count = var.deploy_app ? 1 : 0

  name                = "log-${var.project}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = var.tags
}

resource "azurerm_container_registry" "main" {
  count = var.deploy_app ? 1 : 0

  name                = "acr${var.project}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "Basic"
  admin_enabled       = false
  tags                = var.tags
}

resource "azurerm_container_app_environment" "main" {
  count = var.deploy_app ? 1 : 0

  name                = "cae-${var.project}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  tags                = var.tags

  # Los dos argumentos van juntos: enlazar el workspace sin declarar el destino
  # hace que el proveedor rechace el recurso. Omitir logs_destination no
  # significa "por defecto", significa "solo streaming, sin persistir".
  logs_destination           = "log-analytics"
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main[0].id
}

locals {
  # Cadena de conexión sin credenciales: Active Directory Default hace que el
  # cliente de SQL pida un token con la identidad del contenedor. Para que
  # funcione, esa identidad tiene que existir como usuario dentro de la base de
  # datos, y de eso se encarga el pipeline.
  #
  # El tiempo de espera es generoso a propósito: con la base en serverless y
  # pausada, el primer acceso tiene que esperar a que despierte.
  connection_string = var.deploy_app ? join("", [
    "Server=tcp:${azurerm_mssql_server.main[0].fully_qualified_domain_name},1433;",
    "Database=${azurerm_mssql_database.main[0].name};",
    "Authentication=Active Directory Default;",
    "Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;"
  ]) : ""

  # Configuración común a los dos procesos. Ninguna de estas variables lleva un
  # secreto: son direcciones, nombres e identificadores. Lo que sí es secreto
  # —las credenciales de los brókeres y las de Google— vive en el almacén y se
  # lee con la identidad.
  shared_environment = var.deploy_app ? {
    "ConnectionStrings__Kapea" = local.connection_string
    "KeyVault__Uri"            = azurerm_key_vault.main[0].vault_uri

    # Con una identidad asignada por el usuario hay que decir cuál es:
    # DefaultAzureCredential no la adivina, y sin esta variable pediría un token
    # para la identidad de sistema, que no existe. Afecta a SQL, al almacén, al
    # blob de claves y al servicio de modelos, porque todos usan la misma
    # cadena de credenciales.
    "AZURE_CLIENT_ID" = azurerm_user_assigned_identity.app[0].client_id

    # Sin ApiKey: al no configurarse, la llamada al servicio de modelos se
    # autentica con esa misma identidad.
    "MappingProposals__AzureOpenAi__Endpoint"   = azurerm_cognitive_account.openai.endpoint
    "MappingProposals__AzureOpenAi__Deployment" = azurerm_cognitive_deployment.model.name
  } : {}
}

resource "azurerm_container_app" "api" {
  count = var.deploy_app ? 1 : 0

  name                         = "ca-${var.project}-api"
  container_app_environment_id = azurerm_container_app_environment.main[0].id
  resource_group_name          = azurerm_resource_group.main.name
  revision_mode                = "Single"
  tags                         = var.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.app[0].id]
  }

  registry {
    server   = azurerm_container_registry.main[0].login_server
    identity = azurerm_user_assigned_identity.app[0].id
  }

  template {
    # Escala a cero cuando nadie entra: es una cartera personal y no tiene
    # sentido pagar una réplica encendida toda la noche. El precio es la espera
    # del primer acceso, que es la misma que ya impone la base de datos al
    # despertar.
    min_replicas = 0
    max_replicas = 2

    container {
      name   = "api"
      image  = var.container_image
      cpu    = 0.5
      memory = "1Gi"

      dynamic "env" {
        for_each = merge(local.shared_environment, {
          "ASPNETCORE_ENVIRONMENT" = "Production"
          "ASPNETCORE_HTTP_PORTS"  = "8080"

          # Las claves que cifran la cookie viven en el blob y se protegen con la
          # clave del almacén. Sin esto, cada revisión nueva echaría de la sesión
          # a quien estuviera dentro.
          "DataProtection__BlobUri" = "${azurerm_storage_account.main[0].primary_blob_endpoint}${azurerm_storage_container.data_protection[0].name}/keys.xml"
          "DataProtection__KeyUri"  = azurerm_key_vault_key.data_protection[0].versionless_id
        })

        content {
          name  = env.key
          value = env.value
        }
      }

      # Sin sondas declaradas: el servicio comprueba el puerto por su cuenta, y
      # la imagen de arranque con la que nace el recurso la primera vez no sirve
      # /health, así que una sonda HTTP tumbaría el primer aprovisionamiento.
      # Quien comprueba /health es el pipeline, después de publicar la imagen de
      # verdad, que es donde el fallo se ve y detiene el despliegue.
    }
  }

  ingress {
    external_enabled = true
    target_port      = 8080
    transport        = "auto"

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  # Los roles se asignan a la identidad, no a la aplicación, así que Terraform no
  # ve ninguna dependencia entre ellos y crearía las tres cosas en paralelo. Sin
  # esto el ciclo del registro vuelve convertido en carrera: a veces estaría
  # autorizado a tiempo y a veces no.
  depends_on = [
    azurerm_role_assignment.acr_pull,
    azurerm_role_assignment.openai_user,
    azurerm_role_assignment.secrets_officer,
    azurerm_role_assignment.crypto_user,
    azurerm_role_assignment.keys_blob,
  ]

  lifecycle {
    # La imagen la sustituye el pipeline en cada despliegue y no se le devuelve a
    # Terraform. Sin ignorarla, el siguiente apply de infraestructura volvería a
    # la imagen de arranque pública y tumbaría lo desplegado.
    ignore_changes = [template[0].container[0].image]
  }
}

resource "azurerm_container_app" "sync" {
  count = var.deploy_app ? 1 : 0

  name                         = "ca-${var.project}-sync"
  container_app_environment_id = azurerm_container_app_environment.main[0].id
  resource_group_name          = azurerm_resource_group.main.name
  revision_mode                = "Single"
  tags                         = var.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.app[0].id]
  }

  registry {
    server   = azurerm_container_registry.main[0].login_server
    identity = azurerm_user_assigned_identity.app[0].id
  }

  template {
    # Exactamente una réplica. Dos sincronizarían la misma cuenta a la vez, y
    # aunque existe un cerrojo por cuenta que lo impediría a medias, depender de
    # él para algo evitable es buscarse un problema difícil de diagnosticar.
    # Cero tampoco vale: sin réplica no hay quien despierte al temporizador.
    min_replicas = 1
    max_replicas = 1

    container {
      name   = "sync"
      image  = var.container_image
      cpu    = 0.25
      memory = "0.5Gi"

      dynamic "env" {
        for_each = merge(local.shared_environment, {
          "DOTNET_ENVIRONMENT" = "Production"
        })

        content {
          name  = env.key
          value = env.value
        }
      }
    }
  }

  # Sin bloque ingress: el trabajador no publica nada y no hay forma de
  # alcanzarlo desde fuera del entorno.

  depends_on = [
    azurerm_role_assignment.acr_pull,
    azurerm_role_assignment.openai_user,
    azurerm_role_assignment.secrets_officer,
  ]

  lifecycle {
    ignore_changes = [template[0].container[0].image]
  }
}
