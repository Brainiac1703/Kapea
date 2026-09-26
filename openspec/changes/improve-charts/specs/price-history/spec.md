## MODIFIED Requirements

### Requirement: Serie diaria por activo

El sistema DEBE guardar, por activo y fecha, un único precio de cierre en euros junto al origen del que proviene. Un mismo activo y fecha no PUEDEN tener dos precios distintos.

Cuando el proveedor los dé, el sistema DEBE guardar además la apertura, el máximo y el mínimo de ese día. Sin ellos no se puede saber cuánto se movió un activo dentro del día, sólo dónde acabó: un día que subió un ocho por ciento y volvió al punto de partida es indistinguible de un día plano.

Un activo cuyo proveedor no dé el recorrido DEBE seguir funcionando con su cierre, y quien consulte la serie DEBE poder saber que ese activo no lo tiene. No es lo mismo que un recorrido de cero.

#### Scenario: Precio guardado una sola vez

- **WHEN** se descarga dos veces el precio del mismo activo y la misma fecha
- **THEN** el sistema conserva una sola entrada y no duplica la serie

#### Scenario: Consulta de un día pasado

- **WHEN** se pide el precio de un activo en una fecha que está en la serie
- **THEN** el sistema lo devuelve sin consultar ningún proveedor externo

#### Scenario: Proveedor que da el recorrido del día

- **WHEN** el proveedor entrega apertura, máximo, mínimo y cierre
- **THEN** el sistema los guarda los cuatro

#### Scenario: Proveedor que sólo da el cierre

- **WHEN** el proveedor entrega únicamente el cierre
- **THEN** el sistema lo guarda y deja constancia de que ese día no tiene recorrido

#### Scenario: Recorrido incoherente

- **WHEN** un proveedor entrega un máximo menor que el mínimo, o un cierre fuera de ambos
- **THEN** el sistema rechaza el recorrido y conserva el cierre
