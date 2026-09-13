# ---------------------------------------------------------------------------
# Estado remoto.
#
# La cuenta de almacenamiento y el contenedor son los mismos que usa el otro
# proyecto de este repositorio de trabajo; lo único propio de Kapea es la ruta
# del blob. Esa ruta tiene que ser distinta: coincidir significaría aplicar
# sobre el estado del otro proyecto y empezar a destruir sus recursos.
#
# Configuración parcial a propósito: los datos de la cuenta no se fijan aquí
# porque el fichero es público y porque el método de autenticación cambia según
# quién aplique. Se pasan al inicializar:
#
#   terraform init -backend-config=backend.hcl
#
# y en el pipeline con -backend-config=key=value.
#
# En una máquina de desarrollo, sobre una cuenta en la que quizá no se tengan
# permisos para asignarse roles, lo práctico es la clave de acceso en
# backend.hcl, que está excluido del repositorio. En el pipeline se usa OIDC
# con identidad de Azure, y entonces no hay ninguna clave que guardar como
# secreto de GitHub, que es la razón de usar OIDC en primer lugar. Fijar
# use_azuread_auth aquí impediría el primer caso, así que se decide fuera.
#
# El backend de azurerm bloquea con un lease sobre el blob, así que dos
# ejecuciones sobre la misma ruta esperan en lugar de pisarse, y dos proyectos
# con rutas distintas no se estorban.
#
# Para trabajar sin estado remoto basta con:
#
#   terraform init -backend=false
# ---------------------------------------------------------------------------

terraform {
  backend "azurerm" {}
}
