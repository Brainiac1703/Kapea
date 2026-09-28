## MODIFIED Requirements

### Requirement: Precio por clase de activo

El sistema DEBE obtener precios de criptomonedas y de renta variable, cada uno de su proveedor, y DEBE poder incorporar clases de activo nuevas sin cambiar el resto de la cartera.

Un valor que cotice en una divisa distinta del euro DEBE entregarse convertido, con el mismo tipo contrastable con el que se valoran los movimientos. NO DEBE descartarse por no cotizar en euros: la mitad de la renta variable de una cartera española cotiza en dólares, y descartarla la deja sin valor de mercado sin que nada lo explique.

Cuando el precio de un activo no se pueda obtener, los demás DEBEN obtenerse igualmente. Un fallo NO PUEDE dejar sin precio a una clase entera.

#### Scenario: Precios de las dos clases

- **WHEN** la cartera tiene posiciones abiertas en acciones y en criptomonedas
- **THEN** el sistema obtiene el precio de unas y otras y las muestra en la misma página

#### Scenario: Clase sin proveedor

- **WHEN** la cartera tiene una posición de una clase para la que no hay proveedor de precios
- **THEN** la posición se muestra con su cantidad y su coste medio, indicando que el valor de mercado no está disponible

#### Scenario: Valor cotizado en dólares

- **WHEN** se pide el precio de un valor que cotiza en una divisa distinta del euro
- **THEN** el sistema lo devuelve convertido a euros, y no lo descarta

#### Scenario: Un valor falla y los demás no

- **WHEN** el proveedor no puede dar el precio de uno de los valores pedidos
- **THEN** los demás se devuelven igualmente

#### Scenario: Sin tipo de cambio para convertir

- **WHEN** no hay tipo de cambio con el que convertir el precio de un valor
- **THEN** ese valor se queda sin precio y lo dice, en lugar de entregar una cifra en otra divisa como si fuera en euros
