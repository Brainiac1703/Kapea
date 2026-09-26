# market-prices Specification

## Purpose

Define de dónde salen los precios de mercado de los activos, tanto el actual como las series históricas, y cómo se comporta Kapea cuando un proveedor no tiene el dato.

## Requirements

### Requirement: Series históricas del proveedor

Un proveedor de precios DEBE poder entregar, además del precio de ahora, el cierre diario de un activo entre dos fechas. Un proveedor que no cubra un activo o un rango DEBE devolver lo que tenga, sin impedir que se pidan los demás.

#### Scenario: Rango completo

- **WHEN** se pide a un proveedor el histórico de un activo que cubre
- **THEN** devuelve un precio por cada día del rango en que hubo cotización

#### Scenario: Activo no cubierto

- **WHEN** se pide el histórico de un activo que el proveedor no conoce
- **THEN** devuelve una serie vacía y los activos restantes se piden igual

#### Scenario: Cuota agotada

- **WHEN** el proveedor rechaza la petición por exceso de peticiones
- **THEN** el sistema conserva lo ya obtenido y deja el resto para la siguiente ejecución, sin perder lo descargado

### Requirement: Búsqueda de activos en el proveedor

Un proveedor de precios DEBE poder buscar activos por nombre o por símbolo y devolver, de cada uno, el nombre con el que se le conoce, su símbolo, la clase de activo que es, dónde cotiza cuando eso lo distinga de otro con el mismo símbolo, y el identificador con el que ese proveedor lo conoce.

Un proveedor que no encuentre nada DEBE devolver una lista vacía, y uno que falle NO DEBE impedir que se vean los resultados de los demás.

#### Scenario: Búsqueda por nombre

- **WHEN** el usuario busca un activo por su nombre
- **THEN** el sistema devuelve los activos que coinciden, cada uno con su símbolo, su clase y dónde cotiza

#### Scenario: Búsqueda por símbolo

- **WHEN** el usuario busca escribiendo un símbolo
- **THEN** el sistema devuelve los activos que lo usan, que pueden ser varios

#### Scenario: Un proveedor caído

- **WHEN** uno de los proveedores falla durante una búsqueda
- **THEN** se devuelven los resultados de los demás y se dice que la búsqueda está incompleta

#### Scenario: Sin resultados

- **WHEN** ningún proveedor encuentra nada con ese texto
- **THEN** el sistema lo dice, y ofrece seguir el activo por su símbolo igualmente

### Requirement: El identificador del proveedor se guarda, no se adivina

Cuando un activo se da de alta eligiendo un resultado de búsqueda, el sistema DEBE guardar el identificador con el que ese proveedor lo conoce y usarlo después para pedir sus precios.

Dos activos distintos pueden compartir símbolo, así que el sistema NO DEBE deducir el identificador a partir del símbolo cuando ya tiene uno guardado: hacerlo podría traer el precio de otro activo sin que nada fallara.

#### Scenario: Activo elegido en una búsqueda

- **WHEN** el usuario añade un activo eligiéndolo de los resultados
- **THEN** sus precios se piden con el identificador que traía ese resultado

#### Scenario: Dos activos con el mismo símbolo

- **WHEN** existen dos activos distintos que comparten símbolo y el usuario elige uno
- **THEN** los precios que se piden son los del que eligió

#### Scenario: Activo sin identificador guardado

- **WHEN** un activo entró por una importación y no tiene identificador de proveedor
- **THEN** el sistema lo resuelve como hasta ahora, a partir de su símbolo
