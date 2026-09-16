## Purpose

Define qué es un sistema de especulación en Kapea: un conjunto de reglas con nombre, guardado como dato y versionado, que se puede crear, corregir y comparar sin tocar el programa.

## ADDED Requirements

### Requirement: El sistema de especulación es un dato

Un sistema DEBE poder crearse, consultarse y modificarse desde la aplicación, sin cambios en el código. El sistema DEBE permitir tener varios a la vez.

#### Scenario: Alta de un sistema

- **WHEN** el usuario declara un sistema con sus reglas de entrada y de salida
- **THEN** queda guardado y disponible para simularlo y para emitir señales

#### Scenario: Dos sistemas conviviendo

- **WHEN** existen dos sistemas declarados
- **THEN** cada uno produce sus señales por separado y se pueden comparar entre sí

### Requirement: Versionado de las reglas

Corregir un sistema DEBE crear una versión nueva y conservar la anterior. Una señal ya emitida DEBE seguir diciendo con qué versión se emitió.

#### Scenario: Corrección de una regla

- **WHEN** el usuario cambia el umbral de una regla
- **THEN** se guarda una versión nueva y las señales anteriores siguen atribuidas a la versión con la que salieron

#### Scenario: Simulación de una versión anterior

- **WHEN** se simula una versión que ya no es la vigente
- **THEN** el sistema usa las reglas de esa versión y no las actuales

### Requirement: Reglas expresables y comprobables

Una regla DEBE expresarse como una condición sobre indicadores, precio o posición, combinable con otras por conjunción o disyunción. Un sistema con una regla que el motor no sepa evaluar NO PUEDE guardarse.

#### Scenario: Regla sobre un cruce de medias

- **WHEN** se declara que la entrada ocurre cuando la media de cincuenta días supera a la de doscientos
- **THEN** el sistema la acepta y la puede evaluar

#### Scenario: Regla con un indicador desconocido

- **WHEN** se declara una regla sobre un indicador que el motor no calcula
- **THEN** el sistema la rechaza al guardarla y dice cuál es

### Requirement: Traducción asistida de un sistema descrito en palabras

El sistema PUEDE proponer reglas a partir de la descripción en palabras de un método. La propuesta DEBE presentarse para revisión y NO PUEDE guardarse sin que una persona la acepte.

#### Scenario: Descripción convertida en reglas

- **WHEN** el usuario pega la descripción de un método
- **THEN** el sistema propone las reglas equivalentes, señala lo que no ha sabido traducir y espera la aceptación

#### Scenario: Servicio de traducción no disponible

- **WHEN** no hay servicio de traducción configurado
- **THEN** el sistema permite declarar las reglas a mano y lo dice, sin impedir nada

### Requirement: La asistencia no decide

El servicio de traducción NO PUEDE emitir señales, calcular cifras ni fijar umbrales por su cuenta, y NO PUEDE tomar como entrada una imagen de una gráfica.

#### Scenario: Origen de una señal

- **WHEN** se emite una señal
- **THEN** procede de evaluar reglas guardadas, y el sistema puede decir cuál las disparó
