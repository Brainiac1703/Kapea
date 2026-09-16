# strategy-backtest Specification

## Purpose

Simula lo que habría pasado aplicando un sistema al histórico, con los costes y los impuestos reales, para poder juzgarlo antes de arriesgar dinero en él.

## Requirements

### Requirement: Simulación sin conocimiento del futuro

La simulación DEBE recorrer el histórico día a día y decidir con lo que se sabía cada día. Una operación NO PUEDE ejecutarse a un precio anterior al de la señal que la origina.

#### Scenario: Compra tras una señal

- **WHEN** una señal se emite con el cierre de un día
- **THEN** la compra se simula al precio del día siguiente, no al de la señal

### Requirement: Costes reales

La simulación DEBE aplicar las comisiones de la plataforma en cada operación, tomando las que el usuario haya declarado para ella.

#### Scenario: Comisión dentro del precio

- **WHEN** la plataforma cobra un porcentaje dentro del precio
- **THEN** cada compra y cada venta simuladas lo descuentan, y el resultado lo refleja

#### Scenario: Sistema que opera mucho

- **WHEN** un sistema genera muchas operaciones cortas
- **THEN** el resultado simulado enseña cuánto se ha ido en comisiones, aparte del resultado de mercado

### Requirement: Impuestos del resultado

La simulación DEBE calcular el resultado después de impuestos aplicando el criterio FIFO y los tramos del ahorro vigentes, declarados como dato y no escritos en el código.

#### Scenario: Ganancia realizada

- **WHEN** la simulación cierra una posición con ganancia
- **THEN** el resultado se presenta antes y después de impuestos

#### Scenario: Tramos configurables

- **WHEN** cambian los tramos del ahorro
- **THEN** se actualizan como dato, sin tocar el programa

### Requirement: Comparación con no hacer nada

Toda simulación DEBE compararse con mantener lo aportado sin operar, en el mismo periodo y con las mismas aportaciones.

#### Scenario: Sistema peor que la referencia

- **WHEN** el sistema simulado rinde menos que no hacer nada
- **THEN** el resultado lo dice con claridad

### Requirement: Lo que devuelve una simulación

Una simulación DEBE devolver el resultado, el número de operaciones, cuántas acabaron en ganancia, la caída máxima, lo pagado en comisiones y lo pagado en impuestos.

#### Scenario: Resultado detallado

- **WHEN** termina una simulación
- **THEN** el usuario puede ver cada operación simulada con su fecha, su precio y la regla que la originó
