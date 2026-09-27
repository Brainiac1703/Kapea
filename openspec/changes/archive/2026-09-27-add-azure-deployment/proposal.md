## Why

Kapea solo existe en el portátil de su autor. Todo lo construido hasta ahora —importadores, cartera, histórico, gráficas, sistemas— se apoya en un `docker compose` local y en una base de datos que vive en un contenedor. No hay copia, no hay acceso desde otro sitio y no hay forma de que el trabajador de sincronización corra cuando el portátil está cerrado.

Hay además una dependencia que bloquea trabajo ya especificado: **la traducción de un método a reglas y la extracción de ideas de un texto necesitan un servicio de Azure OpenAI**, y sin él esas pantallas dicen que no hay servicio configurado. Ese servicio hace falta antes que el despliegue de la aplicación, y hace falta también para desarrollar en local.

## What Changes

- Los recursos de Azure se declaran en **Terraform**, con el estado en Azure Storage y autenticación OIDC, sin ninguna clave guardada como secreto de GitHub.
- El camino a producción es **un único flujo de GitHub Actions**: compila, prueba, propone el plan de infraestructura, **espera aprobación manual** y solo entonces aplica lo aprobado, migra la base de datos y publica la revisión nueva.
- El aprovisionamiento va **por etapas**: con una variable se crea solo Azure OpenAI, que es lo que hace falta para desarrollar; con ella activada se añade el resto.
- Se despliegan **los dos procesos**: la API, que sirve además el cliente Blazor, con entrada pública; y el trabajador de sincronización, sin entrada y con una réplica.
- Las **credenciales de los brókeres** pasan a guardarse en Azure Key Vault, que es la implementación de producción del puerto que ya existe. En local se sigue usando el almacén de desarrollo.
- Las **claves que cifran la cookie de sesión** dejan de depender de un volumen local: en Azure van a un contenedor de blobs, o cada revisión nueva cerraría la sesión de quien estuviera dentro.
- La **autenticación con Google** se configura con su identificador y su secreto tomados del Key Vault, y queda escrito qué URL de vuelta hay que registrar para el dominio desplegado.
- Un flujo aparte valida los pull request, sin tocar nada de Azure.

## Capabilities

### New Capabilities

- `deployment`: cómo se aprovisiona y se publica Kapea, qué exige antes de publicar y cómo se protege lo que no puede perderse.

### Modified Capabilities

- `broker-credentials`: el almacén de producción pasa a estar especificado, con acceso por identidad administrada y sin claves en configuración.

## Impact

- **Nuevo**: `Deploy/infra` con los ficheros de Terraform, `Deploy/actions` con la acción compuesta que inicializa Terraform, y `.github/workflows` con el flujo de integración y el de despliegue.
- **Api**: registro del almacén de claves de protección de datos cuando hay contenedor de blobs configurado, y lectura de configuración desde Key Vault en producción.
- **Sin cambios en local.** `docker compose up` tiene que seguir levantando el entorno igual que hoy, con su base en contenedor y su almacén de secretos en fichero.
- **Coste.** Es una cartera personal: la elección de tamaños prioriza el gasto mínimo que funcione, y queda escrito qué se puede apagar sin perder datos.
- **Fuera de alcance**: varios entornos, dominio propio con certificado, y copias de seguridad automáticas más allá de lo que el servicio traiga de serie.
