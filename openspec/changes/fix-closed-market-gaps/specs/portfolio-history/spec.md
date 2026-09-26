## MODIFIED Requirements

### Requirement: Días incompletos

Un día en el que falte el precio de algún activo con posición DEBE devolverse marcado como incompleto, con el valor de lo que sí se pudo valorar.

Una posición cuyo mercado estuvo cerrado ese día NO DEBE dejar el día incompleto: DEBE valorarse al último cierre conocido, y la valoración DEBE declarar que ese precio viene arrastrado. El mercado no cotizó, pero la posición no dejó de valer: un sábado, una acción vale lo que valía el viernes al cierre.

Fuera de ese caso, el sistema NO PUEDE arrastrar el precio del día anterior ni interpolar entre dos días conocidos. Un precio que debería existir y no está es una laguna, y taparla daría por bueno un valor que nadie ha comprobado.

El precio arrastrado NO DEBE presentarse como si fuera del día: quien mire la serie DEBE poder ver cuáles lo son.

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

## ADDED Requirements

### Requirement: El rango completo empieza donde hay datos

Pedir la serie completa DEBE devolverla desde el primer día con datos, no desde un número fijo de años atrás.

Rellenar con años de ceros anteriores al primer movimiento aplasta la parte con información contra un extremo de la gráfica y hace parecer que no hay histórico. Cuánto hay depende de la cartera y del activo, así que lo resuelve quien conoce la serie y no quien la pide.

#### Scenario: Cartera que empieza hace año y medio

- **WHEN** el usuario pide la evolución completa de una cartera cuyo primer movimiento fue hace dieciocho meses
- **THEN** la serie empieza en ese movimiento, y no antes

#### Scenario: Cartera sin ningún movimiento

- **WHEN** no hay ningún movimiento todavía
- **THEN** el sistema lo dice en lugar de devolver años vacíos
