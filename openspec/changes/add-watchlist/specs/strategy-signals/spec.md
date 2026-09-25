## ADDED Requirements

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
