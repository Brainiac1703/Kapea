## 1. Pasos previos, una sola vez

- [x] 1.1 Dejar escrito en `Deploy/README.md` el guion de arranque: crear el registro de aplicación con credencial federada para la rama principal y para el entorno de producción, y concederle acceso de datos sobre el contenedor del estado que ya existe. Se verifica ejecutando los comandos del documento tal cual y obteniendo los identificadores que el flujo necesita.
- [x] 1.2 Elegir la ruta del blob de estado con el nombre del proyecto dentro y comprobar que no existe. Se verifica listando los blobs del contenedor compartido y viendo que solo está el de legacy-lens.
- [x] 1.3 Crear el entorno `production` en GitHub con revisor requerido, y dar de alta como variables la suscripción, el inquilino, el cliente, la cuenta y el contenedor del estado, que son los mismos de legacy-lens, y la ruta del blob, que no. Se verifica con un despliegue de prueba que se queda esperando la aprobación.

## 2. Terraform: el servicio de modelos primero

- [x] 2.1 Crear `Deploy/infra/providers.tf` y `Deploy/infra/backend.tf` con el proveedor de Azure fijado por versión y el estado remoto configurado parcialmente. Se verifica con `terraform init -backend-config=...` apuntando al contenedor creado en 1.1.
- [x] 2.2 Crear `Deploy/infra/variables.tf` con el nombre del proyecto, la región, la región del servicio de modelos y la variable que activa la aplicación. Se verifica con `terraform validate`.
- [x] 2.3 Crear `Deploy/infra/openai.tf` con la cuenta de servicios cognitivos y el despliegue del modelo. Se verifica con un plan que crea solo el grupo de recursos, la cuenta y el despliegue.
- [x] 2.4 Publicar como salidas el punto de acceso, el nombre del despliegue del modelo y la clave marcada como sensible. Se verifica leyéndolas con `terraform output` tras aplicar.
- [x] 2.5 Aplicar esta etapa contra la suscripción real y configurar el servicio en el entorno local con esos valores. Se verifica traduciendo un método descrito en palabras desde la pantalla de sistemas y viendo que devuelve reglas.

## 3. Terraform: la aplicación

- [x] 3.1 Crear `Deploy/infra/identity.tf` con la identidad asignada por el usuario y sus concesiones sobre el registro de contenedores, el almacén de secretos y el servicio de modelos. Se verifica en el plan: la identidad y los permisos existen antes de la aplicación.
- [x] 3.2 Crear `Deploy/infra/data.tf` con el servidor y la base de datos SQL, con la identidad que aplica la infraestructura como administradora de Entra y sin autenticación por contraseña. Se verifica conectando con `sqlcmd -G` usando esa identidad.
- [x] 3.3 Crear `Deploy/infra/secrets.tf` con el almacén de secretos, su clave de cifrado y la cuenta de almacenamiento con el contenedor donde viven las claves de sesión. Se verifica que la identidad de la aplicación puede leer y escribir secretos y que la que aplica la infraestructura puede darlos de alta.
- [x] 3.4 Crear `Deploy/infra/app.tf` con el análisis de registros, el registro de contenedores, el entorno de aplicaciones y las dos aplicaciones: la API con entrada pública y el trabajador sin entrada y con una réplica fija. Se verifica que el plan no publica ninguna entrada para el trabajador.
- [x] 3.5 Pasar la configuración a los contenedores por variables de entorno, con la cadena de conexión sin contraseña, el identificador de la identidad, la dirección del almacén y la del contenedor de claves. Se verifica que ninguna variable del plan contiene un secreto en claro.
- [x] 3.6 Envolver todo lo de este grupo en el condicional de la variable que activa la aplicación. Se verifica que con ella desactivada el plan sigue siendo el de la etapa 2, sin destruir nada.

## 4. Cambios en el código

- [x] 4.1 Guardar las claves de protección de datos en el contenedor de blobs cuando esté configurado, manteniendo la ruta en disco para local y las claves en memoria cuando no haya ninguna de las dos. Se verifica con tres pruebas de composición, una por cada caso.
- [x] 4.2 Negarse a arrancar fuera de desarrollo sin almacén gestionado configurado, con un mensaje que diga qué falta. Se verifica con una prueba que compone en producción sin dirección de almacén y espera el fallo.
- [x] 4.3 Añadir un punto de comprobación de estado que responda sin tocar la base de datos, para que el servicio pueda decidir si la revisión está viva. Se verifica con una prueba contra la fábrica de la API.
- [x] 4.4 Leer el identificador y el secreto de Google desde el almacén gestionado en producción. Se verifica con una prueba de composición que los toma del almacén y no de la configuración.
- [x] 4.5 Hacer opcional la clave del servicio de modelos y autenticar la llamada con la identidad del proceso cuando falte, manteniendo la clave para desarrollo local. Se verifica con cuatro pruebas del manejador: manda el token, no manda clave, reutiliza el token vigente y renueva el que está a punto de caducar.

## 5. Acción compuesta y flujos

- [x] 5.1 Crear `Deploy/actions/terraform/action.yml` que instala Terraform e inicializa el estado remoto con la configuración recibida. Se verifica desde el flujo de despliegue, que la usa en el paso de planificación.
- [x] 5.2 Crear `.github/workflows/ci.yml` que compila y ejecuta la batería completa en cada pull request, sin tocar Azure. Se verifica abriendo un pull request y viéndolo pasar.
- [x] 5.3 Crear el trabajo de compilación y pruebas de `.github/workflows/deploy.yml`; las dos imágenes se construyen en el registro con `az acr build` en el trabajo de publicación, porque el registro no existe antes del primer apply. Se verifica que un fallo de prueba corta el flujo antes de planificar.
- [x] 5.4 Añadir el trabajo que planifica la infraestructura y guarda el plan como artefacto, diciendo si hay cambios. Se verifica descargando el artefacto y leyéndolo con `terraform show`.
- [x] 5.5 Añadir el trabajo que aplica, con el entorno `production` para que espere la aprobación, usando el plan guardado y no uno nuevo. Se verifica con una ejecución que se detiene hasta aprobar y aplica exactamente lo propuesto.
- [x] 5.6 Añadir el paso que da de alta a la identidad de la aplicación en la base de datos y le concede lectura y escritura. Se verifica que es idempotente ejecutándolo dos veces seguidas.
- [x] 5.7 Añadir el paso que aplica las migraciones antes de publicar, y comprobar que un fallo deja la revisión anterior sirviendo. Se verifica provocando un fallo de migración en una ejecución de prueba.
- [x] 5.8 Añadir el paso que publica las dos aplicaciones con la etiqueta recién construida y espera a que la revisión quede activa. Se verifica en la consola del servicio: la revisión nueva recibe todo el tráfico.
- [x] 5.9 Añadir la comprobación posterior contra el punto de estado, que falla el despliegue si no responde. Se verifica apuntándola a una dirección inexistente y viendo que el flujo termina en rojo.
- [x] 5.10 Omitir los pasos que tocan Azure cuando falten las variables de arranque, en lugar de fallar. Se verifica en una bifurcación sin variables configuradas: el flujo termina en verde sin desplegar.

## 6. Puesta en marcha y cierre

- [x] 6.1 Activar la aplicación, desplegar y registrar en Google la URL de vuelta con el nombre que asigna el servicio. Se verifica entrando con la cuenta de Google en la aplicación desplegada.
- [ ] 6.2 Dar de alta una credencial de bróker en la aplicación desplegada y sincronizar. Se verifica viendo el movimiento importado y el secreto guardado en el almacén gestionado, no en la base de datos.
- [ ] 6.3 Publicar una revisión con una sesión abierta y comprobar que no se cierra. Se verifica navegando después del despliegue sin volver a entrar.
- [x] 6.4 Levantar el entorno local desde cero tras todos los cambios. Se verifica con `docker compose up` sin credenciales de Azure y la aplicación funcionando como antes.
- [x] 6.5 Escribir en `Deploy/README.md` el coste mensual estimado, qué recursos se pueden apagar sin perder datos y cómo romper un bloqueo de estado que quedó tomado. Se verifica leyéndolo y siguiendo el procedimiento del bloqueo.
