## 1. Registro de usuarios

- [ ] 1.1 Implementar la entidad de usuario con su identificador interno, su nombre para mostrar y su correo; verificar con tests que el identificador no procede del proveedor
- [ ] 1.2 Implementar la entidad de identidad externa —proveedor y sujeto— asociada a un usuario, con unicidad por proveedor y sujeto; verificar con test de integración que la misma identidad no puede pertenecer a dos usuarios
- [ ] 1.3 Implementar el enlace de una identidad adicional a un usuario existente; verificar con test que ambas identidades resuelven al mismo usuario
- [ ] 1.4 Impedir desenlazar la última identidad de un usuario; verificar con test que la operación se rechaza explicando el motivo
- [ ] 1.5 Persistir usuarios e identidades con su configuración de EF Core y su migración; verificar sobre la base de datos que el esquema se crea y que la unicidad se aplica

## 2. Inicio de sesión con Google

- [ ] 2.1 Configurar el flujo de acceso con Google en la API, con el secreto de cliente en el almacén de secretos; verificar que el secreto no aparece en ninguna respuesta ni en ningún mensaje de registro
- [ ] 2.2 Implementar la resolución de identidad al volver del proveedor: crear el usuario en el primer acceso y reconocerlo en los siguientes; verificar con tests de primer acceso, acceso repetido y correo cambiado en el proveedor
- [ ] 2.3 Implementar el manejo del acceso cancelado o rechazado por el proveedor; verificar con test que no se crea ningún usuario y que el mensaje lo explica
- [ ] 2.4 Establecer la sesión con cookie del servidor y hacer que el usuario actual salga de ella; verificar con test que el identificador de la sesión no puede sustituirse por un parámetro de la petición
- [ ] 2.5 Retirar la configuración de Entra External ID; verificar que la aplicación sigue negándose a arrancar fuera de desarrollo sin proveedor configurado

## 3. Aislamiento con usuarios reales

- [ ] 3.1 Adaptar la autenticación de prueba de los tests de API para que emita identidades de proveedor en lugar de un identificador directo; verificar que la batería existente sigue pasando
- [ ] 3.2 Añadir pruebas de extremo a extremo con dos usuarios sobre la misma instalación: cuentas, movimientos, importaciones, posiciones y resultados; verificar que ninguno ve rastro del otro en ninguna de las consultas
- [ ] 3.3 Verificar que una petición con el identificador de otro usuario como parámetro devuelve los datos del usuario de la sesión, para cada endpoint que acepte parámetros

## 4. Migración de los datos existentes

- [ ] 4.1 Implementar la reasignación de los datos del usuario fijo de desarrollo al primer usuario que inicie sesión, documentando en la propia migración que solo es correcta con un único usuario; verificar sobre una base con datos que el histórico queda a su nombre
- [ ] 4.2 Verificar que tras la migración las cifras de cartera y resultados son idénticas a las de antes

## 5. Interfaz

- [ ] 5.1 Implementar la pantalla de acceso con el botón de entrar con Google y el aviso de que el acceso depende de esa cuenta; verificar manualmente el flujo completo de entrada
- [ ] 5.2 Mostrar en la aplicación quién tiene la sesión iniciada y permitir cerrarla; verificar manualmente que al salir se pierde el acceso a los datos
- [ ] 5.3 Implementar la pantalla de identidades enlazadas, con enlazar y desenlazar; verificar manualmente que no deja quitar la última
- [ ] 5.4 Llevar a la pantalla de acceso cuando la sesión caduque, en lugar de fallar con un error técnico; verificar manualmente forzando la caducidad

## 6. Preparación de Apple

- [ ] 6.1 Comprobar que el modelo y el flujo admiten un proveedor adicional sin cambios de esquema; verificar con un proveedor de prueba que se enlaza y resuelve como Google
- [ ] 6.2 Dejar documentado en el README qué hace falta para activar Apple —cuenta de desarrollador, credenciales y configuración— sin implementarlo
