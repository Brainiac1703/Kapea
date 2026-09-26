# strategy-signals Specification

## Purpose

Convierte las reglas de un sistema en señales sobre la serie de precios, de forma que la misma entrada dé siempre la misma salida y cada señal pueda explicarse.

## Requirements

### Requirement: Evaluación determinista

Evaluar un sistema sobre los mismos precios DEBE producir siempre las mismas señales. Una señal DEBE decir la fecha, el activo, el sentido, la regla que la disparó y la versión del sistema.

#### Scenario: Dos evaluaciones seguidas

- **WHEN** se evalúa el mismo sistema dos veces sobre los mismos datos
- **THEN** las señales son idénticas

#### Scenario: Explicación de una señal

- **WHEN** el usuario mira una señal
- **THEN** el sistema dice qué condición se cumplió y con qué valores

### Requirement: Sin mirar el futuro

Una señal de un día NO PUEDE depender de datos posteriores a ese día.

#### Scenario: Señal de un día pasado

- **WHEN** se evalúa el sistema sobre todo el histórico
- **THEN** la señal de cada día es la misma que se habría emitido ese día con lo que se sabía entonces

### Requirement: Objetivo y nivel de salida

Una señal de entrada DEBE poder llevar un objetivo de precio y un nivel de salida, calculados por las reglas del sistema.

#### Scenario: Objetivo por múltiplo de riesgo

- **WHEN** el sistema declara un objetivo de dos veces la distancia al nivel de salida
- **THEN** la señal incluye ambos precios

#### Scenario: Sistema sin objetivo declarado

- **WHEN** el sistema no declara objetivo
- **THEN** la señal se emite igual y dice que no lo tiene, en lugar de inventar uno

### Requirement: Señales sobre datos incompletos

Un activo al que le falten precios en la ventana que una regla necesita NO PUEDE producir señal para ese día, y la ausencia DEBE decirse.

#### Scenario: Indicador sin ventana completa

- **WHEN** la media de doscientos días aún no tiene datos suficientes
- **THEN** no se emite señal para ese activo y el sistema explica que faltan días

### Requirement: Señales del día a la vista

El sistema DEBE poder devolver las señales vigentes de todos los sistemas declarados, con su activo, su sentido y su antigüedad.

#### Scenario: Varias señales el mismo día

- **WHEN** dos sistemas señalan el mismo activo
- **THEN** se enseñan las dos, cada una con el sistema del que viene

### Requirement: Una señal dice si el activo se tiene

Toda señal DEBE presentarse junto a la situación del activo al que se refiere: si el usuario tiene posición en él o sólo lo vigila.

Una señal de salida sobre un activo sin posición NO DEBE presentarse como algo que hacer, porque no hay nada que vender. Una de entrada sobre un activo que ya se tiene DEBE señalarse como lo que es, una ampliación y no una compra nueva.

#### Scenario: Entrada en algo que no se tiene

- **WHEN** un sistema señala entrada en un activo que el usuario sólo vigila
- **THEN** se presenta como una oportunidad de compra

#### Scenario: Salida en algo que se tiene

- **WHEN** un sistema señala salida en un activo con posición abierta
- **THEN** se presenta como una venta posible, con la posición que se vendería

#### Scenario: Salida en algo que no se tiene

- **WHEN** un sistema señala salida en un activo sin posición
- **THEN** no se presenta como accionable, porque no hay nada que vender

#### Scenario: Entrada en algo que ya se tiene

- **WHEN** un sistema señala entrada en un activo con posición abierta
- **THEN** se presenta como una ampliación de lo que ya se tiene
