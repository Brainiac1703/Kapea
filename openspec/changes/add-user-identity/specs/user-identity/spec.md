## Purpose

Describe cómo una persona entra en Kapea y cómo el sistema sabe que los datos que ve son suyos, apoyándose en proveedores externos para que Kapea nunca custodie una contraseña.

## ADDED Requirements

### Requirement: Registro de usuarios

El sistema DEBE mantener un registro propio de usuarios. Cada usuario DEBE tener un identificador interno estable, que es al que cuelgan sus cuentas, movimientos, lotes y resultados.

El identificador que devuelve un proveedor externo NO DEBE usarse como identificador interno: cambiar de proveedor o enlazar otro dejaría los datos colgando de una referencia que ya no vale.

#### Scenario: Primer acceso

- **WHEN** una persona inicia sesión por primera vez con un proveedor
- **THEN** el sistema crea su usuario y lo deja listo para dar de alta cuentas

#### Scenario: Accesos siguientes

- **WHEN** esa persona vuelve a iniciar sesión con el mismo proveedor
- **THEN** el sistema la reconoce y le devuelve sus datos, sin crear un usuario nuevo

#### Scenario: El identificador interno no cambia

- **WHEN** una persona enlaza o desenlaza un proveedor
- **THEN** su identificador interno sigue siendo el mismo y sus datos siguen siendo suyos

### Requirement: Inicio de sesión con un proveedor externo

El sistema DEBE permitir iniciar sesión con un proveedor externo y NO DEBE almacenar ninguna contraseña.

La identidad se DEBE reconocer por el identificador de sujeto que emite el proveedor. El correo electrónico NO DEBE usarse como clave de identidad: un proveedor puede entregar un correo enmascarado o cambiarlo.

#### Scenario: Sesión iniciada

- **WHEN** la persona completa el acceso con el proveedor
- **THEN** el sistema establece su sesión y toda petición posterior se atribuye a su usuario

#### Scenario: Acceso cancelado o rechazado

- **WHEN** la persona cancela el acceso o el proveedor lo rechaza
- **THEN** el sistema no crea ningún usuario y explica que no se ha podido iniciar sesión

#### Scenario: Correo cambiado en el proveedor

- **WHEN** una persona vuelve con el mismo identificador de sujeto pero un correo distinto
- **THEN** el sistema la reconoce como el mismo usuario y actualiza el correo que muestra

#### Scenario: Sin proveedor configurado fuera de desarrollo

- **WHEN** la aplicación arranca fuera de desarrollo sin ningún proveedor configurado
- **THEN** no arranca, en lugar de quedar accesible sin autenticación

### Requirement: Varias identidades para una misma persona

Un usuario DEBE poder tener enlazadas varias identidades de proveedores distintos, y DEBE poder entrar por cualquiera de ellas.

Se DEBE impedir que un usuario quede sin ninguna identidad enlazada, porque perdería toda vía de acceso a sus datos.

#### Scenario: Enlazar un proveedor más

- **WHEN** un usuario con sesión iniciada enlaza otro proveedor
- **THEN** el sistema asocia esa identidad a su usuario y ambas sirven para entrar

#### Scenario: Entrar por la identidad enlazada

- **WHEN** esa persona inicia sesión con el proveedor enlazado después
- **THEN** el sistema la reconoce como el mismo usuario y le devuelve sus datos

#### Scenario: Identidad ya usada por otro usuario

- **WHEN** se intenta enlazar una identidad que ya pertenece a otro usuario
- **THEN** el sistema rechaza el enlace y lo explica, sin mover ningún dato entre usuarios

#### Scenario: Desenlazar la última identidad

- **WHEN** un usuario intenta desenlazar su única identidad
- **THEN** el sistema lo rechaza y explica que se quedaría sin forma de acceder

### Requirement: Aislamiento entre usuarios

Los datos de un usuario NO DEBEN ser visibles ni modificables por otro bajo ninguna petición, con independencia de los parámetros que esta lleve.

El usuario de una petición DEBE deducirse siempre de su sesión y nunca de un dato de entrada.

#### Scenario: Datos de otro usuario

- **WHEN** un usuario consulta la lista de cuentas, movimientos, posiciones o resultados
- **THEN** solo obtiene los suyos

#### Scenario: Identificador ajeno en la petición

- **WHEN** una petición incluye el identificador de otro usuario como parámetro
- **THEN** el sistema lo ignora y responde con los datos del usuario de la sesión

#### Scenario: Entidad ajena por identificador

- **WHEN** un usuario solicita por identificador una entidad que pertenece a otro
- **THEN** el sistema responde como si no existiera, sin revelar que existe

### Requirement: Sesión visible y cierre de sesión

La aplicación DEBE mostrar quién tiene la sesión iniciada y DEBE permitir cerrarla.

#### Scenario: Identidad a la vista

- **WHEN** una persona navega por la aplicación con la sesión iniciada
- **THEN** ve con qué cuenta ha entrado

#### Scenario: Cierre de sesión

- **WHEN** la persona cierra la sesión
- **THEN** deja de tener acceso a los datos y vuelve a la pantalla de acceso

#### Scenario: Sesión caducada

- **WHEN** la sesión caduca y la persona intenta una operación
- **THEN** el sistema la lleva a iniciar sesión de nuevo en lugar de fallar con un error técnico

### Requirement: Acceso de desarrollo acotado

En desarrollo, y solo cuando no haya ningún proveedor configurado, el sistema PUEDE atribuir toda petición a un usuario fijo para poder trabajar sin depender de un proveedor externo.

Esa vía NO DEBE estar disponible fuera de desarrollo bajo ninguna configuración.

#### Scenario: Desarrollo sin proveedor

- **WHEN** la aplicación arranca en desarrollo sin proveedor configurado
- **THEN** funciona atribuyendo todo a un usuario fijo y avisa de que la autenticación de desarrollo está activa

#### Scenario: Desarrollo con proveedor configurado

- **WHEN** la aplicación arranca en desarrollo con un proveedor configurado
- **THEN** usa el proveedor y no la vía de desarrollo
