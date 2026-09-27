## Context

Ver proposal.md para el porqué. Lo que condiciona el diseño es lo que ya existe:

- Un `Dockerfile` de varias fases con dos destinos, `api` y `sync`, que ya se usa en local.
- Un `docker compose` con SQL Server, la API y el trabajador, más dos volúmenes: uno para el almacén de secretos de desarrollo y otro para las claves de la cookie de sesión.
- Un puerto `ISecretStore` con dos implementaciones: Key Vault y un fichero de User Secrets. La elección se hace al componer, y sin `KeyVault:Uri` se toma la de desarrollo.
- Migraciones de EF Core que en desarrollo se aplican al arrancar y en producción, deliberadamente, no.
- El mismo patrón de despliegue ya resuelto en legacy-lens, del mismo autor: Terraform bajo `Deploy/infra`, acción compuesta que inicializa el estado remoto, y un flujo único con parada de aprobación.

## Goals / Non-Goals

**Goals:**

- Que el servicio de modelos se pueda crear y usar sin desplegar la aplicación.
- Que nada llegue a producción sin que una persona haya visto el plan.
- Que un despliegue no pueda dejar el esquema y el código desacompasados.
- Que el entorno local siga funcionando exactamente igual.

**Non-Goals:**

- Varios entornos. Hay un usuario y una instalación; inventar preproducción multiplicaría el coste y el trabajo sin nadie a quien servir.
- Dominio propio y certificado. El nombre que da el servicio vale, y añadir dominio obliga a tocar la vuelta de Google por segunda vez.
- Optimizar coste más allá de elegir los tamaños mínimos que funcionen.

## Decisions

### Se copia la estructura de legacy-lens, no se reinventa

`Deploy/infra` para Terraform, `Deploy/actions/terraform` para la acción compuesta que inicializa el estado, `.github/workflows` para los flujos. El estado remoto se configura parcialmente, para que en una máquina de desarrollo se pueda usar clave de acceso y en el flujo autenticación federada.

El motivo es práctico: ese patrón ya sobrevivió a los errores que se cometen la primera vez, y sus comentarios los documentan. Repetirlos aquí sale gratis; volver a tropezar, no.

### Container Apps, y no App Service

Dos procesos, uno sin entrada pública, y una imagen que ya está construida y probada en local. Container Apps publica ambos desde el mismo registro, escala a cero el que no recibe tráfico y no obliga a mantener dos modelos de despliegue distintos.

*Alternativa descartada:* App Service para la API y un Web Job o una función para el trabajador. Dos tecnologías, dos formas de configurar y el mismo contenedor sin usar.

### El trabajador va con una réplica fija

Dos réplicas sincronizarían a la vez. Existe un cerrojo por cuenta que lo impediría a medias, pero depender de él para algo evitable es buscarse un problema difícil de diagnosticar. Una réplica, y mínimo una: si escalara a cero, no habría quién despierte al temporizador.

### Identidad asignada por el usuario, creada antes que la aplicación

Es lo que rompe el ciclo de arranque: la aplicación no termina de aprovisionarse hasta poder autenticarse contra el registro, y los permisos sobre el registro no se pueden conceder a una identidad que nace con la aplicación. Creándola aparte, los permisos existen antes.

La misma identidad accede al registro, al Key Vault, a la base de datos y al servicio de modelos. Un solo sujeto al que conceder y del que retirar.

### La base de datos solo admite identidad de Entra

Sin usuario y contraseña no hay contraseña que rotar ni que filtrar. La consecuencia es que el flujo tiene que dar de alta a la identidad de la aplicación dentro de la base, que es un paso más y hay que ejecutarlo con la identidad administradora.

*Coste asumido:* el administrador del servidor es la misma identidad que aplica la infraestructura, porque es la que después ejecuta las migraciones.

### Las migraciones se aplican desde el flujo, no al arrancar

En desarrollo la base se crea sola porque levantar el entorno no debería exigir recordar un comando. En producción eso sería una migración ejecutándose sin que nadie mire, con la revisión ya recibiendo tráfico. Se aplica antes de publicar, donde el fallo se ve y detiene el despliegue.

### Las claves de la cookie van a un contenedor de blobs

En local viven en un volumen, y eso ya se resolvió. En Azure, cada revisión es un contenedor nuevo: sin almacén externo, publicar cerraría la sesión de quien estuviera dentro, que es exactamente el problema que costó cuatro reinicios descubrir.

Se protegen además con una clave del Key Vault, para que quien pueda leer el blob no pueda descifrar las cookies.

### Un solo despliegue de modelo, el económico

Los tres trabajos que Kapea encarga al modelo —deducir el mapeo de un fichero, traducir un método a reglas y extraer ideas de un texto— son de una sola pasada y con salida estructurada. El modelo pequeño llega, y el caro multiplicaría el coste sin mejorar una tarea que se valida a mano antes de guardarse.

### El servicio de modelos permite clave además de identidad

La aplicación desplegada usará su identidad. Pero el punto de este servicio es también poder desarrollar en local, y una máquina de desarrollo sin identidad administrada necesita la clave. Se deja habilitada a propósito, con la clave como salida sensible.

### El estado se guarda en la cuenta que ya existe, con otra ruta

La cuenta de almacenamiento y el contenedor del estado son los de legacy-lens. Lo único propio de Kapea es la ruta del blob, que va en su propia variable. Una cuenta menos que crear, mantener y pagar, y el bloqueo del backend es por blob, así que dos proyectos con rutas distintas no se estorban.

Lo que esto obliga a vigilar: la ruta tiene que ser distinta de la de legacy-lens, porque coincidir significaría que un proyecto aplica sobre el estado del otro y empieza a destruir recursos ajenos. Va con el nombre del proyecto dentro, y se comprueba antes del primer plan listando los blobs del contenedor.

### Registro de aplicación propio, con acceso concedido al contenedor compartido

La federación va atada al repositorio, así que reutilizar el registro de legacy-lens obligaría a añadirle credenciales del repositorio de Kapea y le daría a un mismo sujeto permiso sobre dos suscripciones de recursos. Se crea uno nuevo y se le concede acceso de datos sobre el contenedor del estado, que es lo único compartido.

*Alternativa descartada:* un único registro para los dos proyectos. Ahorra un alta y, a cambio, retirar permisos a uno se los retira al otro.

### El plan se guarda y se aplica tal cual

El plan aprobado se sube como artefacto y el paso de aplicar usa ese fichero. Recalcularlo tras la aprobación abriría una ventana en la que lo aprobado y lo aplicado no coinciden, que es justo lo que la aprobación intenta evitar.

## Risks / Trade-offs

- **La primera aplicación necesita recursos que Terraform no puede crear**: el registro de aplicación con federación y su acceso al contenedor del estado, que ya existe. → Se documentan como pasos previos manuales, una sola vez, con los comandos exactos.
- **Un estado compartido con otro proyecto**: una ruta de blob repetida haría que Kapea aplicara sobre el estado de legacy-lens. → La ruta lleva el nombre del proyecto y se comprueba antes del primer plan; queda escrito en el documento de despliegue.
- **Cuota de modelos por región** → La región se elige por disponibilidad de cuota, no por cercanía, y va en una variable para poder cambiarla sin tocar el resto.
- **Un plan aplicado a medias deja el estado tomado** → El bloqueo espera en lugar de fallar al instante, pero un proceso muerto a media ejecución deja un bloqueo que hay que romper a mano. Queda escrito cómo.
- **Google exige registrar la URL de vuelta antes de que exista el dominio** → El orden correcto es aplicar la infraestructura, leer el nombre que asigna el servicio, registrarlo en Google y después guardar el identificador y el secreto. Va en las tareas en ese orden.
- **Latencia del primer acceso** → La base va en serverless con pausa automática, como en legacy-lens, y la API escala a cero. El primer acceso tras un rato sin uso espera a que despierten las dos; la cadena de conexión da sesenta segundos y la resiliencia de EF reintenta.

## Migration Plan

Nada de lo que ya funciona depende de esto. El orden es: crear a mano lo que Terraform no puede crear, aprovisionar solo el servicio de modelos, configurarlo en local y comprobar que la traducción de un método funciona. Después activar la aplicación, registrar la vuelta de Google y desplegar.

Revertir es destruir los recursos, que Terraform sabe hacer, y seguir en local como hasta ahora.

### Las imágenes se construyen en el registro, después de aplicar

El registro nace con la infraestructura, así que en el primer despliegue no existe mientras se compila y se prueba. Las dos imágenes se construyen con `az acr build` en el trabajo de publicación: el ejecutor no necesita credenciales del registro y un fallo de pruebas sigue deteniendo todo antes de llegar ahí.

### El servicio de modelos acepta token cuando no hay clave

El código exigía la clave. Pasa a ser opcional: sin ella, un manejador pide un token con la identidad del proceso. La aplicación desplegada no lleva clave y una máquina de desarrollo sigue usando la suya.

## Open Questions

Ninguna. La que había, el tamaño de la base, se resolvió con serverless desde el principio.
