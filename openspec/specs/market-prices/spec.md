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
