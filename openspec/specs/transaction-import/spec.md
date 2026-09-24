# transaction-import Specification

## Purpose

Define el contrato común de importación de movimientos: cómo se ejecuta una importación sea cual sea su origen, cómo se garantiza que reimportar no duplica, cómo se reconcilian los traspasos entre cuentas propias y qué ve el usuario cuando algo no encaja.

## Requirements

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

### Requirement: Un movimiento anulado no vuelve al reimportar

La huella de un movimiento anulado DEBE seguir contando como existente en la deduplicación. Reimportar un fichero, sincronizar una cuenta o releer su histórico completo NO DEBE volver a crear un movimiento que el usuario ha anulado.

#### Scenario: Releer el histórico tras anular

- **WHEN** el usuario anula un movimiento de una cuenta con API y después relee todo su histórico
- **THEN** ese movimiento se cuenta como duplicado, no se crea de nuevo y sigue anulado

#### Scenario: Reimportar un fichero tras anular

- **WHEN** el usuario anula un movimiento importado por fichero y vuelve a subir el mismo fichero
- **THEN** la vista previa lo cuenta como duplicado y no lo importaría

### Requirement: Coincidencias con movimientos manuales al importar

Un movimiento manual no tiene huella de origen, así que la deduplicación no puede reconocerlo. La vista previa de una importación DEBE señalar cada registro que coincide con un movimiento manual vigente de la misma cuenta en tipo, activo, cantidad y día, sin descartarlo por sí sola, para que el usuario decida si borra el manual o descarta la importación. Como una sincronización por API no tiene vista previa, *Por revisar* DEBE listar además cada movimiento manual vigente que coincide así con uno importado, hasta que el usuario borre uno de los dos o marque la coincidencia como correcta.

#### Scenario: Importar lo que ya se apuntó a mano

- **WHEN** el usuario apuntó a mano una compra y después importa el fichero de la plataforma que la incluye
- **THEN** la vista previa marca ese registro como posible duplicado de un movimiento manual y enseña cuál

#### Scenario: Sin coincidencias

- **WHEN** ningún registro del fichero coincide con un movimiento manual de la cuenta
- **THEN** la vista previa no muestra ninguna marca de coincidencia

#### Scenario: Llega por API lo que se apuntó a mano

- **WHEN** el usuario apuntó a mano una compra en su cuenta de Kraken y la siguiente sincronización importa esa misma compra
- **THEN** *Por revisar* muestra la pareja como posible duplicado y cuenta como pendiente hasta que el usuario borre el manual o marque que son distintos

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

### Requirement: Permuta entre activos registrada en dos apuntes

Cuando una plataforma registra el cambio de un activo por otro como dos apuntes con una misma referencia —uno de salida y otro de entrada— y ninguno de los dos está denominado en dinero, el sistema DEBE importarlos como las dos patas de una misma permuta: una transmisión del activo entregado y una adquisición del recibido. Ninguna de las dos patas PUEDE alterar el saldo en efectivo de la cuenta, porque en una permuta no entra ni sale dinero.

Un apunte de salida o de entrada cuya pareja no aparece NO DEBE convertirse en permuta: se importa como el movimiento suelto que es y queda pendiente de revisión.

#### Scenario: Cambio de una criptomoneda por otra

- **WHEN** se importa de una plataforma con API un cambio de una criptomoneda por otra, registrado como dos apuntes con la misma referencia
- **THEN** el activo entregado registra una transmisión de la cantidad que sale, el recibido una adquisición de la que entra, y el saldo en efectivo de la cuenta no cambia

#### Scenario: La permuta alimenta el cálculo

- **WHEN** se vende más adelante el activo recibido en una permuta
- **THEN** la venta consume el lote creado por esa permuta, con su fecha y su coste

#### Scenario: Cambio con una pata en dinero

- **WHEN** uno de los dos apuntes está denominado en dinero
- **THEN** el par se importa como la compra o la venta que es, no como una permuta

#### Scenario: Apunte sin pareja

- **WHEN** aparece un apunte de salida o de entrada cuya referencia no tiene el otro lado
- **THEN** el sistema lo importa como movimiento suelto pendiente de revisión, sin inventar la pata que falta

### Requirement: Mover a un producto de rendimiento no es un movimiento

Meter un activo en un producto de rendimiento de la propia plataforma —Earn, staking— o recuperarlo de él NO DEBE importarse como movimiento: no es una adquisición, ni una transmisión, ni un traspaso entre cuentas, y no cambia lo que el usuario tiene. El sistema DEBE contarlo entre los registros descartados por no tener efecto financiero, de modo que el usuario vea cuántos fueron en lugar de que desaparezcan en silencio.

Los rendimientos cobrados de ese producto SÍ DEBEN importarse: son renta, y tributan.

#### Scenario: El mismo paso expuesto en dos fuentes

- **WHEN** una plataforma expone el mismo paso a rendimiento en dos sitios distintos, cada uno con su propio identificador
- **THEN** no se importa ninguna de las dos caras, y el movimiento no aparece duplicado en la lista

#### Scenario: Paso automático entre el activo y su versión en rendimiento

- **WHEN** la plataforma apunta el paso como dos anotaciones del mismo activo que se anulan entre sí
- **THEN** no se importa ninguna de las dos

#### Scenario: La recompensa sí entra

- **WHEN** el producto de rendimiento paga una recompensa
- **THEN** se importa como rendimiento, con sus unidades, como cualquier otra renta

#### Scenario: El usuario ve lo descartado

- **WHEN** termina una importación que ha descartado pasos a rendimiento
- **THEN** el recuento de registros sin efecto financiero los incluye

### Requirement: Valoración estimada cuando el origen no valora

Un movimiento que el origen no valora en euros, y que el sistema no puede dejar sin valorar sin falsear el cálculo, DEBE valorarse con el precio de cierre de su activo en la fecha del movimiento tomado del histórico de precios. El importe así obtenido DEBE quedar marcado como estimado. Si no hay precio para esa fecha, el sistema NO DEBE inventar una cifra: el movimiento queda pendiente de revisión.

#### Scenario: Permuta que la plataforma no valora

- **WHEN** se importa una permuta cuya plataforma no da ningún importe en euros
- **THEN** cada pata se valora al precio de cierre de su activo en esa fecha y queda marcada como estimada

#### Scenario: Sin precio para la fecha

- **WHEN** no hay precio del activo en el histórico para la fecha del movimiento ni puede obtenerse
- **THEN** el movimiento se importa pendiente de revisión, sin importe estimado

#### Scenario: El origen sí valora

- **WHEN** el origen da el importe en euros del movimiento
- **THEN** se usa ese importe y no se marca como estimado
