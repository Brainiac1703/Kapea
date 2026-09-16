## Purpose

Convierte las reglas de un sistema en señales sobre la serie de precios, de forma que la misma entrada dé siempre la misma salida y cada señal pueda explicarse.

## ADDED Requirements

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
