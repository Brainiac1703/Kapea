## MODIFIED Requirements

### Requirement: Alcance del histórico

La serie de un activo en seguimiento DEBE cubrir hasta hoy desde tan atrás como sus proveedores tengan dato. Un activo que no está en seguimiento no necesita serie.

El sistema DEBE garantizar, como mínimo, la historia desde la primera adquisición cuando la haya, y la que los sistemas declarados necesiten para evaluarse con su ventana completa. Ese mínimo es un suelo que hay que cubrir, NO el tope de lo que se descarga: una serie más larga permite juzgar un sistema con más operaciones, y evita que declarar una ventana más larga obligue a esperar una descarga.

Que un proveedor no tenga historia más allá de cierta fecha NO DEBE tratarse como falta de cobertura. Un activo que no existía antes de esa fecha tiene la serie completa que puede tener.

#### Scenario: Primera descarga de un activo

- **WHEN** un activo entra en la cartera con una adquisición de hace dos años
- **THEN** el sistema descarga su precio diario desde esa fecha y sigue hacia atrás mientras el proveedor tenga

#### Scenario: Activo comprado hoy

- **WHEN** un activo se adquiere por primera vez hoy
- **THEN** el sistema descarga su historia disponible, y no sólo el precio de hoy

#### Scenario: Activo que nunca se ha tenido

- **WHEN** el usuario añade al seguimiento un activo que nunca ha comprado
- **THEN** el sistema descarga su historia disponible, que cubre al menos lo que los sistemas declarados necesitan

#### Scenario: Activo más joven que la historia pedida

- **WHEN** el proveedor no tiene dato anterior a la fecha en que el activo empezó a cotizar
- **THEN** la serie empieza ahí y el activo no se cuenta como falto de cobertura

#### Scenario: Sistema con una ventana más larga

- **WHEN** se declara un sistema que necesita más historia de la que necesitaban los anteriores
- **THEN** la serie ya la cubre, sin esperar a una descarga

#### Scenario: Activo que deja de seguirse

- **WHEN** el usuario quita un activo del seguimiento
- **THEN** su serie deja de actualizarse, y la ya descargada se conserva

### Requirement: Descarga incremental

El sistema DEBE pedir a los proveedores solo los días que le faltan. Una segunda ejecución sobre una serie ya completa no PUEDE generar ninguna petición de datos ya guardados.

El sistema DEBE recordar hasta qué fecha hacia atrás ha pedido la serie de cada activo, y NO DEBE volver a pedir por debajo de ella. Recordar sólo lo guardado no basta: un tramo pedido que no devolvió nada volvería a pedirse en cada ejecución, indefinidamente y sin que nada lo delatara.

Un tramo DEBE volver a pedirse cuando cambie lo que se busca: si el suelo baja por debajo de lo ya pedido, o si se resuelve el activo con un identificador de proveedor distinto del usado entonces.

#### Scenario: Puesta al día

- **WHEN** la serie llega hasta anteayer y se ejecuta la actualización
- **THEN** el sistema pide únicamente los dos días que faltan

#### Scenario: Serie ya completa

- **WHEN** se ejecuta la actualización dos veces seguidas el mismo día
- **THEN** la segunda no pide ningún dato al proveedor

#### Scenario: Tramo pedido que no devolvió nada

- **WHEN** se pidió la historia anterior a la primera cotización de un activo y el proveedor no devolvió nada
- **THEN** las ejecuciones siguientes no vuelven a pedir ese tramo

#### Scenario: Suelo que baja

- **WHEN** se quiere historia anterior a la ya pedida
- **THEN** el sistema pide sólo el tramo nuevo, no el que ya había pedido

#### Scenario: Activo que aprende su identificador de proveedor

- **WHEN** un activo pasa a resolverse con un identificador de proveedor distinto del usado en la petición anterior
- **THEN** el sistema puede volver a pedir su historia, porque ahora pregunta por otra cosa

## ADDED Requirements

### Requirement: El relleno hacia atrás no retrasa la puesta al día

Completar la historia antigua de un activo PUEDE llevar mucho más que ponerlo al día, sobre todo la primera vez. El sistema DEBE poner al día antes de rellenar hacia atrás, y el relleno NO DEBE impedir que el precio de hoy esté disponible.

El relleno DEBE poder interrumpirse y reanudarse donde se quedó. Una ejecución que agota su tiempo o la cuota del proveedor DEBE conservar lo descargado.

#### Scenario: Primera pasada con muchos activos

- **WHEN** se rellena por primera vez la historia de todos los activos seguidos
- **THEN** el precio de hoy de cada uno queda disponible sin esperar a que termine el relleno

#### Scenario: Cuota agotada a mitad del relleno

- **WHEN** el proveedor deja de responder durante el relleno
- **THEN** lo descargado se conserva y la siguiente ejecución sigue donde se quedó

#### Scenario: Relleno ya terminado

- **WHEN** todos los activos tienen toda la historia que sus proveedores dan
- **THEN** las ejecuciones siguientes sólo ponen al día
