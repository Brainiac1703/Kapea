## Purpose

Calcula cuánto dinero sin invertir queda en cada cuenta, derivándolo de los mismos movimientos que alimentan el resto de la cartera, para que el valor total del patrimonio sea el que el usuario ve en su bróker.

## ADDED Requirements

### Requirement: Saldo de efectivo derivado de los movimientos

El sistema DEBE calcular el saldo de efectivo de cada cuenta a partir de sus movimientos, sin que el usuario lo introduzca a mano.

Un saldo escrito a mano se desfasa en cuanto se importa un movimiento nuevo, y nadie se entera hasta que las cifras dejan de cuadrar.

#### Scenario: Saldo tras un ingreso

- **WHEN** la cuenta tiene un ingreso de efectivo
- **THEN** su saldo aumenta en el importe ingresado

#### Scenario: Saldo tras una compra

- **WHEN** se compra un activo con comisión
- **THEN** el saldo disminuye en el importe pagado más la comisión

#### Scenario: Saldo tras una venta

- **WHEN** se vende un activo con comisión
- **THEN** el saldo aumenta en lo recibido menos la comisión

#### Scenario: Saldo tras un rendimiento

- **WHEN** se cobra un dividendo o un interés con retención
- **THEN** el saldo aumenta en el importe neto de la retención

#### Scenario: Recálculo

- **WHEN** se importa un movimiento con fecha anterior a otros ya registrados
- **THEN** el saldo se recalcula y refleja el movimiento nuevo

### Requirement: El efectivo se expresa en la divisa de la operación

Cada cuenta PUEDE acumular saldo en más de una divisa. El sistema DEBE mantener el saldo por divisa y NO DEBE sumar divisas distintas sin convertir.

Para el total de la cartera, los saldos en divisa distinta del euro DEBEN convertirse al tipo del día, indicando que la conversión es de hoy y no del momento de cada movimiento.

#### Scenario: Cuenta con saldo en dos divisas

- **WHEN** una cuenta ha operado en euros y en dólares
- **THEN** el sistema muestra el saldo de cada divisa por separado

#### Scenario: Total en euros

- **WHEN** se calcula el efectivo total de la cartera
- **THEN** los saldos en otras divisas se convierten a euros al tipo del día y se indica esa conversión

#### Scenario: Sin tipo de cambio del día

- **WHEN** no hay tipo de cambio disponible para una divisa
- **THEN** su saldo se muestra en su divisa y queda fuera del total en euros, diciéndolo explícitamente

### Requirement: Movimientos sin resolver y saldo

Los movimientos sin clasificar y los traspasos pendientes de confirmar NO DEBEN entrar en el saldo, y el sistema DEBE advertir de que el efectivo está incompleto mientras existan.

#### Scenario: Saldo incompleto

- **WHEN** hay movimientos sin clasificar en una cuenta
- **THEN** el saldo se calcula sin ellos y se advierte de que está incompleto

#### Scenario: Traspaso interno confirmado

- **WHEN** se confirma un traspaso interno de un activo entre dos cuentas propias
- **THEN** el efectivo de ambas cuentas no cambia, salvo por la comisión de red
