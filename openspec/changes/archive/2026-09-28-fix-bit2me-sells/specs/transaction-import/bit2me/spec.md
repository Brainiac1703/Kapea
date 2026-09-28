## ADDED Requirements

### Requirement: Venta a euros desde el monedero

Una venta de un activo a euros desde el monedero DEBE normalizarse con el activo y la cantidad del **lado de origen**, que es donde la plataforma los pone, y con el ingreso del **lado de destino**, que es lo que de verdad llegó a la cuenta.

Tomar el lado equivocado deja la venta sin activo y sin cantidad, y una venta sin activo no se puede importar: la posición queda abierta y el resultado del ejercicio, incompleto.

El ingreso NO DEBE calcularse multiplicando la cantidad por el cambio publicado. La plataforma no cobra comisión aparte sino dando peor cambio, así que la cifra calculada es mayor que la recibida; la diferencia DEBE tratarse como lo que es, el diferencial que se queda la plataforma, con el mismo criterio que ya se aplica a las compras.

#### Scenario: Venta de una criptomoneda a euros

- **WHEN** el usuario vende un activo a euros desde el monedero
- **THEN** el sistema registra una venta de ese activo por la cantidad del origen, con el ingreso en euros que llegó al destino

#### Scenario: El diferencial de una venta

- **WHEN** el importe recibido es menor que la cantidad vendida al cambio publicado, dentro del margen admisible
- **THEN** la diferencia se registra como comisión de la operación, y no como un ingreso menor sin explicación

#### Scenario: Venta que agota la posición

- **WHEN** se vende toda la cantidad que se tenía de un activo
- **THEN** la posición queda cerrada y el activo deja de contar como poseído

#### Scenario: La compra sigue tomando el destino

- **WHEN** el usuario compra un activo con euros desde el monedero
- **THEN** el activo y la cantidad se toman del destino, como hasta ahora
