## Purpose

Guarda la serie diaria de precios de cada activo de la cartera, para que el valor de cualquier día pasado pueda consultarse sin volver a pedirlo y sin depender de que un proveedor externo siga vivo.

## ADDED Requirements

### Requirement: Serie diaria por activo

El sistema DEBE guardar, por activo y fecha, un único precio de cierre en euros junto al origen del que proviene. Un mismo activo y fecha no PUEDEN tener dos precios distintos.

#### Scenario: Precio guardado una sola vez

- **WHEN** se descarga dos veces el precio del mismo activo y la misma fecha
- **THEN** el sistema conserva una sola entrada y no duplica la serie

#### Scenario: Consulta de un día pasado

- **WHEN** se pide el precio de un activo en una fecha que está en la serie
- **THEN** el sistema lo devuelve sin consultar ningún proveedor externo

### Requirement: Alcance del histórico

La serie de un activo DEBE cubrir desde la fecha de su primera adquisición hasta el día de hoy. Un activo sin movimientos no necesita serie.

#### Scenario: Primera descarga de un activo

- **WHEN** un activo entra en la cartera con una adquisición de hace dos años
- **THEN** el sistema descarga su precio diario desde esa fecha

#### Scenario: Activo comprado hoy

- **WHEN** un activo se adquiere por primera vez hoy
- **THEN** el sistema descarga únicamente el precio de hoy

### Requirement: Descarga incremental

El sistema DEBE pedir a los proveedores solo los días que le faltan. Una segunda ejecución sobre una serie ya completa no PUEDE generar ninguna petición de datos ya guardados.

#### Scenario: Puesta al día

- **WHEN** la serie llega hasta anteayer y se ejecuta la actualización
- **THEN** el sistema pide únicamente los dos días que faltan

#### Scenario: Serie ya completa

- **WHEN** se ejecuta la actualización dos veces seguidas el mismo día
- **THEN** la segunda no pide ningún dato al proveedor

### Requirement: Días sin precio

Un día del que ningún proveedor da precio DEBE quedar ausente de la serie, y quien la consuma DEBE poder distinguir esa ausencia de un precio igual a cero.

#### Scenario: Fin de semana en renta variable

- **WHEN** se consulta la serie de una acción en un día que el mercado estuvo cerrado
- **THEN** el sistema indica que no hay precio para ese día, en lugar de devolver cero o el del día anterior

#### Scenario: Proveedor sin cobertura

- **WHEN** ningún proveedor cubre un activo
- **THEN** su serie queda vacía y el resto de activos se descarga igualmente

### Requirement: Degradación ante un proveedor caído

Un fallo del proveedor NO PUEDE dejar la serie a medias de forma silenciosa ni impedir que se guarde lo ya descargado. El sistema DEBE conservar lo obtenido y reintentar los días que falten en la siguiente ejecución.

#### Scenario: Corte a mitad de la descarga

- **WHEN** el proveedor falla después de entregar parte del rango pedido
- **THEN** el sistema guarda lo entregado y vuelve a pedir el resto en la siguiente ejecución
