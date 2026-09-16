# external-ideas Specification

## Purpose

Recoge lo que una fuente externa propone sobre un valor concreto como idea fechada, y mide después lo que dio, para que la confianza en esa fuente sea un número y no una impresión.

## Requirements

### Requirement: La idea es un hecho con fecha y origen

Una idea DEBE guardar el activo, el sentido, la fecha, la fuente de la que viene y, cuando la fuente los dé, el nivel de entrada, el objetivo y el nivel de salida. La fuente DEBE poder consultarse después, con el enlace a la publicación original.

#### Scenario: Idea anotada desde una publicación

- **WHEN** el usuario registra una idea a partir de una publicación con su enlace
- **THEN** queda guardada con su fecha, su fuente y los niveles que traía

#### Scenario: Idea sin niveles

- **WHEN** la fuente no da objetivo ni nivel de salida
- **THEN** la idea se guarda igual y dice que no los tiene, en lugar de calcularle unos

### Requirement: Solo se guarda lo extraído, no la obra ajena

El sistema NO PUEDE conservar la transcripción ni el texto completo de la publicación de un tercero. DEBE guardar las ideas extraídas y la referencia a la publicación.

#### Scenario: Texto pegado para extraer

- **WHEN** el usuario pega el texto de una publicación y acepta las ideas propuestas
- **THEN** se guardan las ideas y el enlace, y el texto pegado no queda almacenado

### Requirement: Extracción asistida con aprobación

El sistema PUEDE proponer ideas a partir de un texto pegado. La propuesta DEBE presentarse para revisión y NO PUEDE guardarse sin que una persona la acepte. Lo que no se haya sabido extraer DEBE señalarse en lugar de completarse.

#### Scenario: Varias ideas en un mismo texto

- **WHEN** el texto menciona dos valores con niveles distintos
- **THEN** el sistema propone dos ideas y el usuario acepta o descarta cada una

#### Scenario: Texto sin ninguna idea concreta

- **WHEN** el texto solo contiene comentario general sin valor ni nivel
- **THEN** el sistema no propone ninguna idea y lo dice

### Requirement: Aviso de publicación nueva

El sistema PUEDE vigilar una fuente declarada y avisar de que hay publicación nueva, usando únicamente interfaces que la fuente ofrezca para ello.

#### Scenario: Publicación nueva en una fuente vigilada

- **WHEN** aparece una publicación posterior a la última conocida
- **THEN** el sistema lo señala con su título, su fecha y su enlace

#### Scenario: Fuente que no se puede vigilar

- **WHEN** la fuente no ofrece forma de consultarla
- **THEN** el sistema permite registrar ideas a mano y lo dice, sin impedir nada

### Requirement: Seguimiento del resultado de una idea

Una idea con niveles DEBE seguirse contra la serie de precios hasta que alcance su objetivo, alcance su nivel de salida o caduque, y el sistema DEBE decir en cuál de los tres está.

#### Scenario: Idea que alcanza el objetivo

- **WHEN** el precio llega al objetivo antes que al nivel de salida
- **THEN** la idea queda resuelta como alcanzada, con la fecha en que ocurrió

#### Scenario: Idea que salta por el nivel de salida

- **WHEN** el precio llega al nivel de salida antes que al objetivo
- **THEN** la idea queda resuelta como fallida, con la fecha en que ocurrió

#### Scenario: Idea aún viva

- **WHEN** el precio no ha alcanzado ninguno de los dos niveles
- **THEN** la idea sigue abierta, y se dice desde cuándo

### Requirement: Balance por fuente

El sistema DEBE poder resumir, por fuente y periodo, cuántas ideas se resolvieron a favor, cuántas en contra, cuántas siguen abiertas y qué habría rendido seguirlas todas, con las comisiones de la plataforma incluidas.

#### Scenario: Resumen de una fuente

- **WHEN** el usuario consulta el balance de una fuente con ideas ya resueltas
- **THEN** el sistema da el recuento y el rendimiento que habrían dado, neto de comisiones

#### Scenario: Fuente sin ideas resueltas todavía

- **WHEN** ninguna idea de la fuente se ha resuelto
- **THEN** el sistema lo dice en lugar de dar un rendimiento de cero
