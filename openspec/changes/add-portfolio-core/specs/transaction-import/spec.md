## Purpose

Define el contrato común de importación de movimientos: cómo se ejecuta una importación sea cual sea su origen, cómo se garantiza que reimportar no duplica, cómo se reconcilian los traspasos entre cuentas propias y qué ve el usuario cuando algo no encaja.

## ADDED Requirements

### Requirement: Contrato de adaptador de importación

El sistema DEBE tratar cada plataforma como un adaptador que declara su plataforma, su forma de origen (fichero subido o API remota) y que traduce registros de origen a movimientos normalizados. Añadir un adaptador nuevo NO DEBE requerir cambios en el motor de importación, en el modelo de dominio ni en el motor de P&L.

#### Scenario: Alta de un adaptador nuevo

- **WHEN** se incorpora un adaptador para una plataforma no soportada hasta ahora
- **THEN** el motor de importación, la deduplicación y el cálculo de P&L funcionan con él sin modificaciones

#### Scenario: Adaptador no disponible

- **WHEN** el usuario solicita importar desde una plataforma para la que no hay adaptador registrado
- **THEN** el sistema rechaza la solicitud indicando las plataformas soportadas

### Requirement: Ejecución de importación con ciclo de vida observable

Toda importación DEBE materializarse en una ejecución con identificador propio, cuenta destino, adaptador, instante de inicio y fin, estado, y un recuento de registros leídos, importados, duplicados descartados y rechazados. El usuario DEBE poder consultar el historial de ejecuciones y el detalle de una ejecución concreta.

#### Scenario: Importación correcta

- **WHEN** una importación termina sin errores
- **THEN** la ejecución queda en estado completado con los recuentos de registros leídos, importados, duplicados y rechazados

#### Scenario: Consulta del detalle

- **WHEN** el usuario abre una ejecución del historial
- **THEN** el sistema muestra sus recuentos y la lista de registros rechazados con el motivo de cada rechazo

### Requirement: Importación atómica

Los movimientos de una ejecución DEBEN persistirse todos o ninguno. Un fallo a mitad de proceso NO DEBE dejar movimientos parcialmente importados.

#### Scenario: Fallo a mitad de la importación

- **WHEN** la persistencia falla después de haber procesado parte de los registros
- **THEN** la ejecución queda en estado fallido con su motivo y ningún movimiento de esa ejecución queda persistido

### Requirement: Idempotencia y deduplicación

Cada movimiento importado DEBE llevar una huella estable derivada de la cuenta, el adaptador y el identificador natural del registro de origen —o, cuando el origen no aporte identificador, de sus datos financieros—. Reimportar un registro cuya huella ya existe en la misma cuenta NO DEBE crear un movimiento nuevo.

#### Scenario: Reimportación del mismo fichero

- **WHEN** el usuario importa por segunda vez un fichero ya importado en la misma cuenta
- **THEN** el sistema no crea movimientos nuevos y contabiliza todos los registros como duplicados descartados

#### Scenario: Periodos solapados

- **WHEN** dos importaciones cubren rangos de fechas que se solapan
- **THEN** solo se persisten los movimientos del solape que no existían ya

#### Scenario: Operaciones idénticas y legítimas

- **WHEN** el origen aporta dos registros con identificador natural distinto pero datos financieros idénticos
- **THEN** el sistema importa ambos movimientos

### Requirement: Registros rechazados sin bloquear la importación

Un registro que no se puede normalizar NO DEBE abortar la ejecución. El sistema DEBE conservarlo con su contenido original y el motivo del rechazo, y DEBE permitir al usuario revisarlo y reprocesarlo tras corregir el problema.

#### Scenario: Fila ilegible en un fichero

- **WHEN** una fila del fichero de origen no se puede interpretar
- **THEN** el sistema la registra como rechazada con su motivo, continúa con el resto y refleja el recuento en la ejecución

#### Scenario: Reproceso de rechazados

- **WHEN** el usuario reprocesa los registros rechazados de una ejecución tras corregir la causa
- **THEN** los que ya se pueden normalizar se importan y los que siguen fallando conservan su estado de rechazo con el motivo actualizado

### Requirement: Detección de traspasos entre cuentas propias

Cuando una salida de una cuenta del usuario se corresponde con una entrada del mismo activo en otra cuenta suya —cantidades compatibles dentro de la tolerancia por comisión de red y fechas dentro de una ventana configurable—, el sistema DEBE proponer emparejarlas como un único traspaso interno.

Un traspaso interno confirmado NO DEBE tratarse como venta ni como compra: DEBE trasladar los lotes de origen a la cuenta de destino conservando su coste y su fecha de adquisición original.

#### Scenario: Traspaso detectado

- **WHEN** una retirada de un activo en una cuenta coincide con un depósito del mismo activo en otra cuenta del usuario dentro de la ventana y la tolerancia configuradas
- **THEN** el sistema propone el emparejamiento al usuario en lugar de darlo por hecho

#### Scenario: Traspaso confirmado

- **WHEN** el usuario confirma el emparejamiento propuesto
- **THEN** los lotes se trasladan a la cuenta de destino conservando coste y fecha de adquisición, y no se genera ningún resultado realizado

#### Scenario: Traspaso rechazado

- **WHEN** el usuario rechaza el emparejamiento propuesto
- **THEN** ambos movimientos se mantienen como operaciones independientes y el sistema no vuelve a proponer ese mismo emparejamiento

#### Scenario: Comisión de red

- **WHEN** la cantidad recibida es menor que la enviada por una comisión de red
- **THEN** el emparejamiento sigue siendo posible y la diferencia se registra como comisión del traspaso

### Requirement: Eliminación de una ejecución de importación

El usuario DEBE poder eliminar una ejecución completa junto con los movimientos que creó, siempre que ninguno de ellos participe en un traspaso confirmado o en un ajuste manual dependiente.

#### Scenario: Eliminación posible

- **WHEN** el usuario elimina una ejecución cuyos movimientos no tienen dependencias
- **THEN** el sistema borra la ejecución y sus movimientos, y los lotes y resultados derivados se recalculan

#### Scenario: Eliminación bloqueada

- **WHEN** el usuario intenta eliminar una ejecución con movimientos implicados en un traspaso confirmado
- **THEN** el sistema rechaza la operación y enumera las dependencias que lo impiden

### Requirement: Sincronización periódica de los adaptadores de API

Los adaptadores de origen API DEBEN sincronizarse de forma programada desde el servidor, importando desde el instante de la última importación correcta de esa cuenta. Una sincronización solapada de la misma cuenta NO DEBE ejecutarse en paralelo.

#### Scenario: Sincronización incremental

- **WHEN** se dispara la sincronización programada de una cuenta ya sincronizada antes
- **THEN** el sistema solicita a la plataforma solo los movimientos posteriores al instante de la última importación correcta

#### Scenario: Ejecución solapada

- **WHEN** se dispara una sincronización de una cuenta que ya tiene otra en curso
- **THEN** la nueva no se ejecuta y queda constancia de que se omitió por solape

#### Scenario: Plataforma no disponible

- **WHEN** la plataforma no responde o devuelve un error temporal
- **THEN** el sistema reintenta con espera creciente y, si agota los reintentos, deja la ejecución en estado fallido sin alterar el instante de última importación correcta
