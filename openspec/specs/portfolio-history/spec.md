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

Un día en el que falte el precio de algún activo con posición DEBE devolverse marcado como incompleto, con el valor de lo que sí se pudo valorar.

Una posición cuyo mercado estuvo cerrado ese día NO DEBE dejar el día incompleto: DEBE valorarse al último cierre conocido, y la valoración DEBE declarar que ese precio viene arrastrado. El mercado no cotizó, pero la posición no dejó de valer: un sábado, una acción vale lo que valía el viernes al cierre.

Fuera de ese caso, el sistema NO PUEDE arrastrar el precio del día anterior ni interpolar entre dos días conocidos. Un precio que debería existir y no está es una laguna, y taparla daría por bueno un valor que nadie ha comprobado.

El precio arrastrado NO DEBE presentarse como si fuera del día: quien mire la serie DEBE poder ver cuáles lo son.

Que falte la cotización de un activo que no se tiene NO DEBE marcar el día como incompleto: no había nada que valorar, así que no falta nada.

#### Scenario: Falta el precio de un activo

- **WHEN** un día tiene precio de todos los activos menos uno, y el mercado de ése estaba abierto
- **THEN** el sistema devuelve el valor de los demás y señala el día como incompleto

#### Scenario: Ningún precio disponible

- **WHEN** un día no tiene precio de ningún activo y no es por cierre de mercado
- **THEN** el sistema devuelve ese día sin valor y señalado como incompleto, en lugar de omitirlo de la serie

#### Scenario: Fin de semana con acciones en cartera

- **WHEN** se consulta un sábado y la cartera tiene acciones y criptomonedas
- **THEN** las acciones se valoran al cierre del viernes, el día no queda incompleto y el valor total no cae

#### Scenario: Festivo de una sola bolsa

- **WHEN** una bolsa cierra por festivo y otra opera ese día
- **THEN** lo de la bolsa cerrada se arrastra y lo de la abierta se valora al precio del día

#### Scenario: Precio arrastrado a la vista

- **WHEN** el usuario mira un día valorado con precios arrastrados
- **THEN** puede saber que lo son, en lugar de creer que son cierres de ese día

#### Scenario: Antes de que hubiera ninguna cotización

- **WHEN** no hay ningún cierre anterior del que tirar
- **THEN** no se arrastra nada y el día queda incompleto

#### Scenario: Falta el precio de algo que sólo se vigila

- **WHEN** un día no tiene cotización de un activo que el usuario vigila pero no tiene
- **THEN** el día no se marca como incompleto, porque ese activo no entra en el valor de la cartera

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

### Requirement: Reparto por clase de activo a lo largo del tiempo

El sistema DEBE poder devolver el valor por clase de activo para cada día del rango, de forma que se pueda ver cómo cambia el peso de cada clase.

#### Scenario: Clase incorporada más tarde

- **WHEN** la cartera solo tuvo cripto durante un año y después incorporó renta variable
- **THEN** la serie muestra la nueva clase a partir de su primera adquisición, sin alterar los días anteriores

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

### Requirement: El rango completo empieza donde hay datos

Pedir la serie completa DEBE devolverla desde el primer día con datos, no desde un número fijo de años atrás.

Rellenar con años de ceros anteriores al primer movimiento aplasta la parte con información contra un extremo de la gráfica y hace parecer que no hay histórico. Cuánto hay depende de la cartera y del activo, así que lo resuelve quien conoce la serie y no quien la pide.

#### Scenario: Cartera que empieza hace año y medio

- **WHEN** el usuario pide la evolución completa de una cartera cuyo primer movimiento fue hace dieciocho meses
- **THEN** la serie empieza en ese movimiento, y no antes

#### Scenario: Cartera sin ningún movimiento

- **WHEN** no hay ningún movimiento todavía
- **THEN** el sistema lo dice en lugar de devolver años vacíos
