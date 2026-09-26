## MODIFIED Requirements

### Requirement: Recorrido medio diario

El sistema DEBE calcular el recorrido medio diario de una serie, que mide cuánto se mueve un activo.

Cuando la serie tenga máximo y mínimo, el recorrido de un día DEBE calcularse con ellos, que es lo que el activo se movió de verdad. Cuando no los tenga, DEBE calcularse de un cierre al siguiente, y el sistema DEBE decir cuál de las dos formas ha usado: no son comparables entre sí, y presentarlas igual haría parecer más tranquilo a un activo del que sólo se conocen los cierres.

#### Scenario: Activo tranquilo y activo movido

- **WHEN** se calcula sobre dos activos con recorridos distintos
- **THEN** el más movido tiene un recorrido medio mayor

#### Scenario: Serie con un solo día

- **WHEN** la serie tiene un único día
- **THEN** el sistema no devuelve recorrido, en lugar de devolver cero

#### Scenario: Serie con máximos y mínimos

- **WHEN** la serie tiene el recorrido de cada día
- **THEN** el indicador se calcula con máximos y mínimos y lo declara

#### Scenario: Serie con sólo cierres

- **WHEN** la serie no tiene máximos ni mínimos
- **THEN** el indicador se calcula de cierre a cierre y lo declara

## ADDED Requirements

### Requirement: La dispersión se presenta como lo que es

El sistema PUEDE entregar, para dibujarlas sobre la serie, bandas que describan cuánto se ha movido un activo: las bandas de volatilidad y una envolvente del recorrido medio.

Esas bandas DEBEN presentarse como una descripción de lo ya ocurrido. El sistema NO DEBE presentarlas como un pronóstico, ni como una probabilidad de que el precio vaya a estar dentro de ellas, ni etiquetarlas de forma que lo sugiera.

Que un precio haya estado dentro de una banda el noventa por ciento del tiempo no dice que vaya a estarlo el noventa por ciento de las veces. Rotularlo así convertiría una medida del pasado en una promesa sobre el futuro, que es exactamente lo que ninguna de estas medidas puede sostener.

#### Scenario: Bandas sobre la serie

- **WHEN** el usuario pide ver la dispersión de un activo
- **THEN** obtiene las bandas con el periodo sobre el que se han calculado

#### Scenario: Cómo se rotulan

- **WHEN** las bandas se enseñan
- **THEN** dicen que describen el recorrido pasado, y no la probabilidad de nada

#### Scenario: Serie demasiado corta

- **WHEN** no hay días suficientes para la ventana
- **THEN** el sistema no devuelve bandas, en lugar de devolverlas calculadas sobre menos días
