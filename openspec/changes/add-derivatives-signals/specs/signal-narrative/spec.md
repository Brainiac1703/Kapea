## Purpose

Redacta en palabras lo que las señales ya calculadas dicen, para que el usuario entienda qué está pasando y qué riesgo tiene sin tener que interpretar cifras sueltas.

## ADDED Requirements

### Requirement: El texto explica lo calculado y no calcula nada

La redacción DEBE partir de señales y magnitudes ya calculadas por el sistema. NO DEBE producir ninguna cifra que no venga en lo que recibe, ni recalcular, corregir o completar las que recibe.

Una cifra que aparezca en el texto DEBE ser una de las recibidas. El sistema DEBE poder comprobarlo antes de enseñarlo y DEBE descartar el texto que no lo cumpla.

#### Scenario: Texto con una cifra recibida

- **WHEN** la redacción menciona el valor que disparó una señal
- **THEN** ese valor coincide con el calculado

#### Scenario: Texto con una cifra inventada

- **WHEN** la redacción incluye una cifra que no estaba en lo recibido
- **THEN** el sistema no la enseña y deja constancia

### Requirement: El texto no predice ni recomienda

La redacción NO DEBE afirmar hacia dónde irá un precio, ni recomendar comprar o vender. DEBE limitarse a explicar qué dice el dato, por qué suele considerarse relevante y qué riesgo comporta.

#### Scenario: Petición de explicación

- **WHEN** el usuario pide que le expliquen una señal
- **THEN** obtiene qué ocurre, por qué podría importar y qué riesgo tiene, sin un pronóstico

#### Scenario: Ausencia de fundamento

- **WHEN** no hay señales ni magnitudes destacables que explicar
- **THEN** el sistema lo dice y no redacta nada

### Requirement: Lo redactado se distingue de lo calculado

La interfaz DEBE distinguir sin ambigüedad qué parte de lo que se enseña la ha calculado el sistema y qué parte la ha redactado un modelo de lenguaje.

El usuario DEBE poder ver las señales y sus cifras sin la redacción. Que el modelo falle, tarde o no esté disponible NO DEBE impedir ver lo calculado.

#### Scenario: Señal con su explicación

- **WHEN** el usuario mira una señal explicada
- **THEN** ve cuál es el dato y cuál es el texto, marcados de forma distinta

#### Scenario: Modelo no disponible

- **WHEN** el modelo de lenguaje falla o no responde
- **THEN** las señales y sus cifras se enseñan igual, diciendo que falta la explicación
