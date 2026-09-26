## MODIFIED Requirements

### Requirement: Serie de una posición

El sistema DEBE poder devolver, para un solo activo y un rango de fechas, su cotización de cada día, junto con las unidades que se tenían y su valor.

La cotización DEBE devolverse haya posición o no. Es el precio del mercado, que existe con independencia de quién tenga el activo, y es sobre lo que se calculan los indicadores y se evalúan los sistemas. Un activo que nunca se ha comprado DEBE tener serie: sin ella no hay indicadores, y sin indicadores un sistema de entrada no sirve para decidir dónde entrar.

Las unidades y el valor SÍ dependen de tener el activo: son nulos los días sin posición, porque no había nada que valorar. NO DEBEN confundirse con la ausencia de cotización.

Un día sin cotización —un festivo, o un día anterior a que el activo empezara a cotizar— DEBE devolverse sin precio y NO DEBE rellenarse arrastrando el día anterior ni interpolando.

#### Scenario: Evolución de un activo concreto

- **WHEN** se pide la serie de un activo con posición abierta
- **THEN** el sistema devuelve su cotización, su cantidad y su valor por día desde la primera adquisición

#### Scenario: Activo vigilado que nunca se ha comprado

- **WHEN** se pide la serie de un activo sin un solo movimiento
- **THEN** el sistema devuelve su cotización de cada día, y las unidades y el valor vacíos

#### Scenario: Activo vendido por completo

- **WHEN** se pide la serie de un activo que se tuvo y se vendió
- **THEN** la cotización sigue estando los días posteriores a la venta, y las unidades y el valor quedan vacíos desde ella

#### Scenario: Día en que el mercado no abrió

- **WHEN** un día del rango no tiene cotización porque el mercado estaba cerrado
- **THEN** ese día se devuelve sin precio, y no se rellena con el del día anterior

#### Scenario: Rango anterior a la primera cotización

- **WHEN** se pide un rango que empieza antes de que el activo cotizara
- **THEN** los días anteriores se devuelven sin precio, sin que eso impida devolver los siguientes

### Requirement: Días incompletos

Un día en el que falte el precio de algún activo con posición DEBE devolverse marcado como incompleto, con el valor de lo que sí se pudo valorar. El sistema NO PUEDE arrastrar el precio del día anterior ni interpolar entre dos días conocidos.

Que falte la cotización de un activo que no se tiene NO DEBE marcar el día como incompleto: no había nada que valorar, así que no falta nada.

#### Scenario: Falta el precio de un activo

- **WHEN** un día tiene precio de todos los activos menos uno
- **THEN** el sistema devuelve el valor de los demás y señala el día como incompleto

#### Scenario: Ningún precio disponible

- **WHEN** un día no tiene precio de ningún activo
- **THEN** el sistema devuelve ese día sin valor y señalado como incompleto, en lugar de omitirlo de la serie

#### Scenario: Falta el precio de algo que sólo se vigila

- **WHEN** un día no tiene cotización de un activo que el usuario vigila pero no tiene
- **THEN** el día no se marca como incompleto, porque ese activo no entra en el valor de la cartera

## ADDED Requirements

### Requirement: El rango de la serie de un activo se elige

Quien consulta la serie de un activo DEBE poder elegir el rango, y DEBE poder pedir toda la historia disponible sin tener que saber de antemano desde cuándo hay.

El rango pedido NO DEBE limitarse a lo que había cuando se escribió la pantalla: si un activo tiene veinte años de cotizaciones, se pueden ver.

#### Scenario: Más de un año

- **WHEN** el usuario pide la serie de los últimos cinco años de un activo que los tiene
- **THEN** el sistema los devuelve

#### Scenario: Toda la historia

- **WHEN** el usuario pide toda la historia disponible
- **THEN** el sistema devuelve desde la primera cotización guardada de ese activo

#### Scenario: Rango mayor que la historia que hay

- **WHEN** el rango pedido empieza antes de la primera cotización guardada
- **THEN** el sistema devuelve lo que hay, sin tratarlo como un error
