## ADDED Requirements

### Requirement: Almacén de secretos en producción

Fuera de desarrollo, el secreto de una credencial DEBE guardarse en un almacén gestionado, y el acceso DEBE hacerse con la identidad de la aplicación. La aplicación NO PUEDE llevar en su configuración ninguna clave de acceso a ese almacén.

#### Scenario: Alta de una credencial en producción

- **WHEN** el usuario da de alta una credencial
- **THEN** el secreto se guarda en el almacén gestionado y la aplicación solo conserva su referencia

#### Scenario: Acceso sin clave

- **WHEN** la aplicación necesita el secreto para sincronizar
- **THEN** lo obtiene autenticándose con su propia identidad, sin ninguna clave guardada

#### Scenario: Almacén inalcanzable

- **WHEN** el almacén no responde
- **THEN** la sincronización de esa cuenta falla y lo dice, sin que el fallo afecte a las demás cuentas ni a la aplicación

### Requirement: Arranque sin almacén configurado

La aplicación NO PUEDE arrancar en producción con el almacén de desarrollo, que guarda en claro. Si no hay almacén gestionado configurado fuera de desarrollo, el arranque DEBE fallar diciendo qué falta.

#### Scenario: Producción sin almacén

- **WHEN** la aplicación arranca fuera de desarrollo sin almacén gestionado configurado
- **THEN** se niega a arrancar en lugar de guardar secretos en claro
