# broker-credentials Specification

## Purpose

Gobierna cómo Kapea recibe, custodia, usa y revoca las credenciales de acceso a las plataformas de inversión, partiendo de que el cliente Blazor WebAssembly se ejecuta en el navegador y no puede custodiar ningún secreto.

## Requirements

### Requirement: El secreto nunca llega al cliente

Una credencial de plataforma DEBE almacenarse cifrada fuera del cliente y NO DEBE devolverse nunca —ni entera ni parcialmente— en ninguna respuesta de la API. El cliente solo DEBE poder consultar metadatos: plataforma, alias, permisos declarados, fecha de alta, estado y última sincronización.

#### Scenario: Consulta de credenciales

- **WHEN** el cliente solicita la lista de credenciales registradas
- **THEN** la respuesta contiene únicamente metadatos y ningún fragmento del secreto

#### Scenario: Alta de credencial

- **WHEN** el usuario envía una API key y su secreto desde el cliente
- **THEN** el backend los almacena cifrados y responde solo con los metadatos de la credencial creada

#### Scenario: Registro y diagnóstico

- **WHEN** el sistema registra trazas o errores relacionados con una credencial
- **THEN** el secreto no aparece en el mensaje de log, ni completo ni truncado

### Requirement: Verificación en el alta

Al registrar una credencial el sistema DEBE verificarla contra la plataforma antes de darla por válida, y DEBE comprobar que sus permisos son de solo lectura cuando la plataforma exponga esa información.

#### Scenario: Credencial válida de solo lectura

- **WHEN** el usuario registra una credencial que la plataforma acepta y declara de solo lectura
- **THEN** el sistema la marca como activa y la habilita para sincronización

#### Scenario: Credencial rechazada por la plataforma

- **WHEN** la plataforma rechaza la credencial durante la verificación
- **THEN** el sistema no la persiste y devuelve el motivo del rechazo

#### Scenario: Credencial con permisos de trading o retirada

- **WHEN** la plataforma declara que la credencial tiene permisos de trading o de retirada de fondos
- **THEN** el sistema rechaza el alta y explica que Kapea solo admite credenciales de solo lectura

### Requirement: Uso exclusivamente server-side

Las credenciales solo DEBEN usarse desde procesos de servidor —la Web API y el proceso de sincronización programada—. Ningún flujo DEBE requerir que el cliente contacte directamente con la plataforma.

#### Scenario: Sincronización programada

- **WHEN** se dispara la sincronización periódica
- **THEN** las llamadas a la plataforma parten del servidor usando la credencial descifrada en memoria, sin intervención del cliente

### Requirement: Rotación y revocación

El usuario DEBE poder sustituir el secreto de una credencial y DEBE poder revocarla. Una credencial revocada NO DEBE usarse en ninguna sincronización posterior, y los movimientos ya importados con ella DEBEN conservarse intactos.

#### Scenario: Rotación

- **WHEN** el usuario sustituye el secreto de una credencial existente
- **THEN** el sistema verifica el nuevo secreto, reemplaza el almacenado y conserva el histórico de sincronizaciones de esa credencial

#### Scenario: Revocación

- **WHEN** el usuario revoca una credencial
- **THEN** el sistema la marca como revocada, deja de usarla en las sincronizaciones y mantiene sin cambios los movimientos importados previamente

### Requirement: Degradación ante credencial inválida

Cuando una plataforma rechaza una credencial durante una sincronización, el sistema DEBE marcarla como inválida, detener sus sincronizaciones automáticas y avisar al usuario, sin afectar a las credenciales de otras plataformas.

#### Scenario: Credencial caducada o revocada en la plataforma

- **WHEN** una sincronización falla por credencial no autorizada
- **THEN** el sistema marca esa credencial como inválida, la excluye de las siguientes ejecuciones programadas, deja constancia visible para el usuario y continúa sincronizando el resto de plataformas

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
