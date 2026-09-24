## ADDED Requirements

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
