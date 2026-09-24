# portfolio-history Specification

## Purpose

Reconstruye lo que valía la cartera cada día a partir de los movimientos y de la serie de precios, para poder enseñar su evolución y no solo su foto de hoy.

## Requirements

### Requirement: Valor de la cartera día a día

El sistema DEBE poder devolver, para un rango de fechas, cuántas unidades de cada activo había cada día y cuánto valían en euros. Las unidades salen de los movimientos hasta esa fecha, y el valor del precio de ese día.

#### Scenario: Día anterior a una compra

- **WHEN** se consulta el valor de la cartera en un día anterior a la primera compra de un activo
- **THEN** ese activo no aporta nada al valor de ese día

#### Scenario: Día posterior a una venta total

- **WHEN** se consulta un día posterior a la venta completa de un activo
- **THEN** ese activo no aporta nada al valor de ese día

#### Scenario: Movimientos del propio día

- **WHEN** un activo se compra un día concreto
- **THEN** el valor de ese día ya incluye las unidades compradas

### Requirement: Días incompletos

Un día en el que falte el precio de algún activo con posición DEBE devolverse marcado como incompleto, con el valor de lo que sí se pudo valorar. El sistema NO PUEDE arrastrar el precio del día anterior ni interpolar entre dos días conocidos.

#### Scenario: Falta el precio de un activo

- **WHEN** un día tiene precio de todos los activos menos uno
- **THEN** el sistema devuelve el valor de los demás y señala el día como incompleto

#### Scenario: Ningún precio disponible

- **WHEN** un día no tiene precio de ningún activo
- **THEN** el sistema devuelve ese día sin valor y señalado como incompleto, en lugar de omitirlo de la serie

### Requirement: Aportaciones separadas del rendimiento

La serie DEBE distinguir lo que cambió por dinero aportado o retirado de lo que cambió por precio. Un ingreso seguido de una compra NO PUEDE leerse como una ganancia.

Lo que cuenta como aportación DEBE ser lo mismo aquí y en el resumen de la cartera: sólo el dinero que entra o sale de la plataforma. Un movimiento entre cuentas del propio usuario NO DEBE contar, porque ese dinero ya estaba dentro, ni tampoco la entrada de un activo, que no es dinero puesto.

#### Scenario: Ingreso y compra el mismo día

- **WHEN** el usuario ingresa mil euros y compra con ellos
- **THEN** el valor de la cartera sube mil euros y el rendimiento del día no se altera por esa operación

#### Scenario: Dinero movido entre cuentas propias

- **WHEN** el usuario traspasa dinero de una de sus cuentas a otra y el traspaso está confirmado
- **THEN** la serie no registra ninguna aportación ni ninguna retirada por ese movimiento

#### Scenario: Activo que llega de fuera

- **WHEN** entra un activo en una cuenta sin que su dinero haya pasado por la plataforma
- **THEN** no se registra como aportación, porque no es dinero puesto

### Requirement: Serie de una posición

El sistema DEBE poder devolver la misma serie para un solo activo: unidades, precio y valor por día, con las mismas reglas sobre días sin precio.

#### Scenario: Evolución de un activo concreto

- **WHEN** se pide la serie de un activo con posición abierta
- **THEN** el sistema devuelve su cantidad y su valor por día desde la primera adquisición

### Requirement: Reparto por clase de activo a lo largo del tiempo

El sistema DEBE poder devolver el valor por clase de activo para cada día del rango, de forma que se pueda ver cómo cambia el peso de cada clase.

#### Scenario: Clase incorporada más tarde

- **WHEN** la cartera solo tuvo cripto durante un año y después incorporó renta variable
- **THEN** la serie muestra la nueva clase a partir de su primera adquisición, sin alterar los días anteriores
