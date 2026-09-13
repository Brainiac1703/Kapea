variable "subscription_id" {
  description = "Suscripción de Azure donde se despliega."
  type        = string
}

variable "project" {
  description = "Prefijo corto para nombrar los recursos."
  type        = string
  default     = "kapea"
}

variable "location" {
  description = <<-EOT
    Región de despliegue. La elección se hace por disponibilidad de cuota de
    modelos y no por cercanía: France Central tiene cuota de gpt-4.1-mini con
    SKU Standard y es, entre las que la tienen, la de menor latencia desde
    España.
  EOT
  type        = string
  default     = "francecentral"
}

variable "model" {
  description = <<-EOT
    Modelo de lenguaje. Los tres trabajos que Kapea le encarga —deducir el
    mapeo de un fichero, traducir un método a reglas y extraer ideas de un
    texto— son de una sola pasada y con salida estructurada, así que el modelo
    económico llega. Uno más capaz multiplicaría el coste sin mejorar una
    respuesta que además se valida a mano antes de guardarse.
  EOT
  type = object({
    name     = string
    version  = string
    capacity = number
  })
  default = {
    name     = "gpt-4.1-mini"
    version  = "2025-04-14"
    capacity = 50
  }
}

variable "deploy_app" {
  description = <<-EOT
    Permite aprovisionar por etapas. Con false solo se crean el grupo de
    recursos y Azure OpenAI, que es lo que hace falta para desarrollar en
    local. Con true se añaden la base de datos, el almacén de secretos, el
    registro de contenedores y las dos aplicaciones.
  EOT
  type        = bool
  default     = false
}

variable "sql_admin" {
  description = <<-EOT
    Identidad de Entra que administra el servidor SQL. Es la misma que aplica la
    infraestructura, porque es la que después tiene que aplicar las migraciones
    y dar de alta la identidad de la aplicación dentro de la base de datos.

    No hay usuario y contraseña: el servidor se crea con autenticación
    exclusivamente por Entra.
  EOT
  type = object({
    login     = string
    object_id = string
  })
  default = null
}

variable "database" {
  description = <<-EOT
    Dimensionado de la base de datos. Los valores por omisión son los más
    baratos que sirven: serverless con autopausa, porque una cartera personal
    se consulta a ráfagas y pagar por segundo de cómputo cuesta una fracción
    del escalón más bajo de capacidad reservada. El precio es la latencia del
    primer acceso tras una pausa, que la aplicación absorbe con reintentos.
  EOT
  type = object({
    sku_name           = string
    min_capacity       = number
    auto_pause_minutes = number
    max_size_gb        = number
  })
  default = {
    sku_name     = "GP_S_Gen5_1"
    min_capacity = 0.5
    # El mínimo que admite Azure.
    auto_pause_minutes = 60
    max_size_gb        = 32
  }
}

variable "container_image" {
  description = <<-EOT
    Imagen de arranque de las dos aplicaciones. El pipeline la sustituye por la
    recién construida en cada despliegue, así que aquí solo hace falta algo que
    exista para que el recurso se pueda crear la primera vez.
  EOT
  type        = string
  default     = "mcr.microsoft.com/k8se/quickstart:latest"
}

variable "tags" {
  description = "Etiquetas aplicadas a todo. Identifican el proyecto y su dueño."
  type        = map(string)
  default = {
    project    = "kapea"
    purpose    = "gestion-personal-de-inversiones"
    managed-by = "terraform"
  }
}
