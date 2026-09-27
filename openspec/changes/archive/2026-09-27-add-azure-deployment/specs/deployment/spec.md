## Purpose

Define cómo se aprovisiona y se publica Kapea: qué se crea antes de qué, qué tiene que estar aprobado, y qué no puede perderse al desplegar una revisión nueva.

## ADDED Requirements

### Requirement: Nada se despliega si las pruebas no pasan

El camino a producción DEBE compilar la solución y ejecutar la batería completa antes de tocar cualquier recurso. Un fallo en las pruebas DEBE detener el despliegue.

#### Scenario: Prueba en rojo

- **WHEN** una prueba falla en la rama principal
- **THEN** no se planifica infraestructura, no se aplica nada y no se publica ninguna revisión

### Requirement: Aprobación manual antes de aplicar

El sistema DEBE proponer el plan de infraestructura y esperar la aprobación de una persona antes de aplicarlo. Lo que se aplique DEBE ser exactamente el plan aprobado, no uno calculado de nuevo.

#### Scenario: Plan revisado y aprobado

- **WHEN** el plan se aprueba
- **THEN** se aplica el plan guardado, sin ventana para que algo cambie entre la aprobación y la aplicación

#### Scenario: Plan sin aprobar

- **WHEN** nadie aprueba el plan
- **THEN** la infraestructura queda intacta y el despliegue no continúa

#### Scenario: Plan sin cambios

- **WHEN** el plan no encuentra nada que cambiar
- **THEN** se dice y el despliegue de la aplicación puede continuar igual

### Requirement: Aprovisionamiento por etapas

El sistema DEBE permitir crear únicamente el servicio de modelos de lenguaje, sin la aplicación, para poder usarlo desde un entorno de desarrollo.

#### Scenario: Solo el servicio de modelos

- **WHEN** se aprovisiona con la aplicación desactivada
- **THEN** se crean el grupo de recursos y el servicio de modelos, y no se crea ni registro de contenedores ni aplicación

#### Scenario: Punto de acceso para desarrollo

- **WHEN** termina ese aprovisionamiento
- **THEN** el sistema informa del punto de acceso y del nombre del despliegue del modelo, que es lo que hace falta para configurarlo en local

### Requirement: El esquema va antes que el código

Las migraciones de la base de datos DEBEN aplicarse antes de publicar la revisión que las necesita. El sistema NO PUEDE publicar primero y migrar después.

#### Scenario: Revisión con una migración nueva

- **WHEN** un despliegue incluye una migración
- **THEN** la migración se aplica y solo después se publica la revisión nueva

#### Scenario: Migración que falla

- **WHEN** la migración falla
- **THEN** no se publica la revisión nueva y la anterior sigue sirviendo

### Requirement: Los dos procesos se despliegan por separado

El sistema DEBE publicar la API con entrada pública y el trabajador de sincronización sin ella. El trabajador NO PUEDE quedar accesible desde internet.

#### Scenario: Entrada del trabajador

- **WHEN** se consulta el trabajador desde fuera
- **THEN** no hay forma de alcanzarlo: no tiene entrada publicada

#### Scenario: Escalado del trabajador

- **WHEN** el trabajador está desplegado
- **THEN** corre con una sola réplica, porque dos sincronizando a la vez se pisarían

### Requirement: Comprobación después de publicar

Tras publicar, el sistema DEBE comprobar que la aplicación responde. Si no responde, el despliegue DEBE darse por fallido.

#### Scenario: La aplicación no arranca

- **WHEN** la revisión nueva no responde tras publicarse
- **THEN** el despliegue termina en fallo y lo dice, en lugar de darse por bueno

### Requirement: La sesión sobrevive a un despliegue

Las claves que cifran la cookie de sesión DEBEN guardarse fuera del proceso. Publicar una revisión nueva NO PUEDE cerrar la sesión de quien estuviera dentro.

#### Scenario: Revisión nueva con sesión abierta

- **WHEN** se publica una revisión mientras un usuario está dentro
- **THEN** su sesión sigue siendo válida

#### Scenario: Sin almacén configurado

- **WHEN** no hay almacén de claves configurado
- **THEN** la aplicación arranca igual, con las claves en memoria, y queda dicho que un reinicio cerrará las sesiones

### Requirement: Sin secretos guardados en el repositorio ni en el flujo

El acceso a Azure desde el flujo DEBE hacerse con identidad federada, sin contraseñas ni claves de cliente guardadas como secretos. Los valores propios de una instalación —suscripción, cuenta de estado, identificadores— DEBEN ser configuración y no estar escritos en los ficheros.

#### Scenario: Flujo sin configurar

- **WHEN** faltan las variables de arranque del flujo
- **THEN** los pasos que tocan Azure se omiten en lugar de fallar, para no dejar el repositorio en rojo permanente

### Requirement: El estado de la infraestructura es compartido y con bloqueo

El estado de Terraform DEBE guardarse remotamente y tomarse en exclusiva mientras se aplica, de modo que dos ejecuciones no puedan pisarse.

#### Scenario: Dos aplicaciones a la vez

- **WHEN** una aplicación ya tiene el estado tomado
- **THEN** la segunda espera en lugar de escribir sobre lo que la primera está cambiando

### Requirement: El entorno local no cambia

Levantar el entorno de desarrollo DEBE seguir funcionando igual que antes de existir el despliegue, sin credenciales de Azure y sin conexión.

#### Scenario: Desarrollo sin Azure

- **WHEN** se levanta el entorno local
- **THEN** arranca con su base de datos en contenedor y su almacén de secretos en fichero, sin necesitar ningún recurso remoto
