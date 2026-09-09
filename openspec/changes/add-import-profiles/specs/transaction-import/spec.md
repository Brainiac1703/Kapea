## MODIFIED Requirements

### Requirement: Contrato de adaptador de importación

El sistema DEBE tratar cada plataforma como un adaptador que declara su plataforma, su forma de origen (fichero subido o API remota) y que traduce registros de origen a movimientos normalizados. Añadir un adaptador nuevo NO DEBE requerir cambios en el motor de importación, en el modelo de dominio ni en el motor de P&L.

La importación desde fichero DEBE resolverse con un único adaptador genérico guiado por perfiles, de modo que dar de alta una plataforma que se importa por fichero NO requiera escribir código.

#### Scenario: Alta de un adaptador nuevo

- **WHEN** se incorpora un adaptador para una plataforma no soportada hasta ahora
- **THEN** el motor de importación, la deduplicación y el cálculo de P&L funcionan con él sin modificaciones

#### Scenario: Adaptador no disponible

- **WHEN** el usuario solicita importar desde una plataforma para la que no hay adaptador registrado
- **THEN** el sistema rechaza la solicitud indicando las plataformas soportadas

#### Scenario: Plataforma de fichero sin código propio

- **WHEN** se da de alta una plataforma que se importa por fichero y se define su perfil
- **THEN** sus ficheros se importan con el adaptador genérico, sin haber añadido ningún adaptador nuevo

### Requirement: Ejecución de importación con ciclo de vida observable

Toda importación DEBE materializarse en una ejecución con identificador propio, cuenta destino, adaptador, instante de inicio y fin, estado, y un recuento de registros leídos, importados, duplicados descartados y rechazados. El usuario DEBE poder consultar el historial de ejecuciones y el detalle de una ejecución concreta.

Cuando la importación proceda de un fichero, la ejecución DEBE registrar además el perfil y la versión con los que se interpretó.

#### Scenario: Importación correcta

- **WHEN** una importación termina sin errores
- **THEN** la ejecución queda en estado completado con los recuentos de registros leídos, importados, duplicados y rechazados

#### Scenario: Consulta del detalle

- **WHEN** el usuario abre una ejecución del historial
- **THEN** el sistema muestra sus recuentos y la lista de registros rechazados con el motivo de cada rechazo

#### Scenario: Perfil aplicado

- **WHEN** el usuario abre una ejecución procedente de un fichero
- **THEN** el sistema indica con qué perfil y qué versión se interpretó

## ADDED Requirements

### Requirement: Vista previa con registros ya interpretados

Antes de confirmar una importación de fichero, el sistema DEBE mostrar, además de los recuentos, las primeras filas ya normalizadas con su fecha, tipo, activo, cantidad e importe.

Es lo que permite detectar un mapeo equivocado antes de persistir nada, también cuando el perfil se aceptó sin pedir confirmación.

#### Scenario: Filas interpretadas en la vista previa

- **WHEN** el usuario sube un fichero y se genera la vista previa
- **THEN** el sistema muestra al menos las primeras filas importables ya normalizadas, junto a los recuentos

#### Scenario: Mapeo equivocado visible antes de confirmar

- **WHEN** el perfil aplicado interpreta mal una columna
- **THEN** el error se aprecia en las filas interpretadas de la vista previa, y el usuario puede descartar la importación sin que se haya persistido ningún movimiento

#### Scenario: Fichero sin ninguna fila importable

- **WHEN** ningún registro del fichero resulta importable
- **THEN** la vista previa lo dice explícitamente en lugar de mostrar una tabla vacía sin explicación
