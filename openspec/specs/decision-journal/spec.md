# decision-journal Specification

## Purpose

Guarda por qué se hizo cada operación, para poder releerlo meses después y aprender de los propios aciertos y errores.

## Requirements

### Requirement: Anotar una decisión

El usuario DEBE poder anotar, sobre un movimiento o sobre una señal, por qué decidió lo que decidió. La anotación DEBE guardar cuándo se escribió.

#### Scenario: Anotación sobre una compra

- **WHEN** el usuario escribe el motivo de una compra ya importada
- **THEN** queda guardado junto al movimiento sin alterar sus cifras

#### Scenario: Anotación sobre una señal no seguida

- **WHEN** el usuario decide no seguir una señal y anota por qué
- **THEN** queda guardado junto a la señal

### Requirement: El movimiento importado no se toca

Anotar una decisión NO PUEDE modificar los datos financieros del movimiento ni su correspondencia con el registro de origen.

#### Scenario: Movimiento con anotación

- **WHEN** un movimiento anotado se vuelve a importar desde su plataforma
- **THEN** se sigue descartando como duplicado y la anotación se conserva

### Requirement: Revisión del diario

El sistema DEBE poder devolver las anotaciones de un periodo, con el movimiento o la señal a la que acompañan y el resultado que tuvo.

#### Scenario: Repaso de lo decidido

- **WHEN** el usuario repasa el diario de un trimestre
- **THEN** ve cada decisión anotada junto a lo que pasó después
