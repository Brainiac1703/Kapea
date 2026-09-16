## Why

Kapea se diseñó con `UserId` en toda entidad de cartera y un filtro global en la base de datos, pero nunca ha tenido usuarios. Hoy hay uno solo: en desarrollo se atribuye todo a un identificador fijo, y en producción la API se niega a arrancar si no hay identidad configurada.

El aislamiento ya está construido y probado. Lo que falta es la puerta: poder entrar, que la aplicación sepa quién eres y que lo que ves sea tuyo y de nadie más.

Este change abre esa puerta con proveedores externos —Google al principio— y sin que Kapea guarde nunca una contraseña. Sustituye la decisión de Microsoft Entra External ID de la propuesta inicial, tomada cuando el objetivo era una identidad corporativa que ya no encaja: para una aplicación personal, iniciar sesión con la cuenta que ya tienes es más simple para quien entra y más seguro para quien guarda los datos.

## What Changes

- **Registro propio de usuarios.** Una fila por persona, a la que cuelgan sus cuentas, movimientos, lotes y resultados. Es lo que da sentido al `UserId` que el modelo ya lleva.
- **Inicio de sesión con Google.** El primer acceso crea el usuario; los siguientes lo reconocen.
- **Varias identidades por usuario.** Una persona puede enlazar más de un proveedor y entrar por cualquiera de ellos. Se diseña así desde el principio para que perder la cuenta de Google no signifique perder el histórico.
- **Apple preparado, no implementado.** El modelo y los flujos admiten un proveedor más; activarlo será configuración y una prueba. No se implementa ahora porque exige una cuenta de Apple Developer de pago.
- **Cierre de sesión y sesión visible**: quién ha entrado se ve en la aplicación, y se puede salir.
- **La autenticación de desarrollo sigue existiendo**, con las mismas garantías de hoy: solo en Development y solo si no hay proveedor configurado.
- **BREAKING**: se retira la configuración de Entra External ID de la API.

## Capabilities

### New Capabilities

- `user-identity`: registro de usuarios, inicio de sesión con proveedores externos, enlace de varias identidades a una misma persona y cierre de sesión.

### Modified Capabilities

- `portfolio-domain`: el aislamiento por usuario deja de referirse a un usuario hipotético y pasa a apoyarse en el registro real.

## Impact

- **Código**: entidad de usuario e identidades enlazadas, flujo de inicio de sesión en la API, sesión en el cliente y una pantalla de acceso.
- **Infraestructura**: credenciales de cliente de Google, guardadas como el resto de secretos. Desaparece la configuración de Entra.
- **Datos existentes**: los movimientos ya importados están atribuidos al usuario fijo de desarrollo. La migración los reasigna al primer usuario que inicie sesión, para no perder el histórico de pruebas.
- **Lo que no cambia**: el filtro global, el `UserId` en las entidades y las consultas. Ese trabajo ya está hecho y probado; este change solo le da usuarios de verdad.
- **Fuera de alcance**: roles y permisos, compartir cartera entre usuarios, y borrado de cuenta con sus datos. Nada de eso hace falta todavía.
