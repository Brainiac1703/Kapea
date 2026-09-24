## ADDED Requirements

### Requirement: Lo que suma y lo que resta en la lista de movimientos

La lista de movimientos DEBE decir, en cada uno, si suma a la cartera o resta de ella. El signo DEBE ir delante del importe y el color sólo DEBE reforzarlo, de modo que se lea igual sin distinguir colores.

Suma lo que entra sin pagarlo o devuelve dinero: una venta, un ingreso, un dividendo, un interés y un rendimiento cobrado, aunque llegue en unidades del propio activo y no mueva ningún euro. Resta lo que cuesta dinero: una compra, una retirada y una comisión.

Un intercambio NO DEBE mostrarse con signo ni con color: una permuta cambia una cosa por otra y un traspaso lleva algo de una cuenta propia a otra, así que ni suman ni restan. Fingir una dirección que no tienen es peor que no decir nada.

La comisión DEBE mostrarse con el mismo criterio, porque siempre resta.

#### Scenario: Una venta suma

- **WHEN** el usuario mira una venta en la lista
- **THEN** su importe aparece con signo positivo y el color que el resto de la aplicación usa para lo que suma

#### Scenario: Una compra resta

- **WHEN** el usuario mira una compra
- **THEN** su importe aparece con signo negativo y el color de lo que resta

#### Scenario: La comisión

- **WHEN** un movimiento tiene comisión
- **THEN** se muestra con signo negativo y el color de lo que resta

#### Scenario: Una recompensa cobrada en unidades

- **WHEN** el usuario mira una recompensa que le pagaron en unidades del propio activo
- **THEN** su importe aparece como lo que suma, aunque ningún euro se haya movido: se la regalaron y su cartera vale más

#### Scenario: Un intercambio

- **WHEN** el usuario mira una permuta o un traspaso entre sus cuentas
- **THEN** su importe se muestra sin signo ni color, porque ni sumó ni restó
