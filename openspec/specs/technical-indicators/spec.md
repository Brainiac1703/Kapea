# technical-indicators Specification

## Purpose

Calcula sobre una serie de precios los indicadores que sirven para leer su tendencia, y que son la base de las señales de compra y venta de fases posteriores.

## Requirements

### Requirement: Media móvil

El sistema DEBE calcular la media móvil simple y la exponencial de una serie de precios para un número de días dado. Los días anteriores a completar la ventana NO PUEDEN tener valor.

#### Scenario: Ventana incompleta

- **WHEN** se pide una media de veinte días sobre una serie de quince
- **THEN** el sistema no devuelve ningún valor, en lugar de promediar lo que hay

#### Scenario: Ventana completa

- **WHEN** se pide una media de veinte días sobre una serie de cien
- **THEN** el sistema devuelve ochenta y un valores, uno por cada día desde que la ventana se completa

### Requirement: Índice de fuerza relativa

El sistema DEBE calcular el RSI de una serie para un número de días dado, con valores entre 0 y 100.

#### Scenario: Serie que solo sube

- **WHEN** todos los días de la ventana cierran por encima del anterior
- **THEN** el RSI vale 100

#### Scenario: Serie que solo baja

- **WHEN** todos los días de la ventana cierran por debajo del anterior
- **THEN** el RSI vale 0

### Requirement: Huecos en la serie

Un indicador NO PUEDE rellenar los días sin precio. El cálculo DEBE hacerse sobre los días con precio, en su orden, e indicar a qué fecha corresponde cada valor.

#### Scenario: Serie con días ausentes

- **WHEN** la serie salta un fin de semana
- **THEN** el indicador no produce valores para esos días y los valores siguientes no se desplazan de fecha

### Requirement: Convergencia y divergencia de medias

El sistema DEBE calcular el MACD de una serie a partir de dos medias exponenciales y su línea de señal, devolviendo también la diferencia entre ambas.

#### Scenario: Serie más corta que la ventana larga

- **WHEN** la serie no llega para calcular la media larga
- **THEN** no se devuelve ningún valor

#### Scenario: Cruce de la línea de señal

- **WHEN** la línea principal pasa por encima de la de señal
- **THEN** la diferencia entre ambas cambia de signo ese día

### Requirement: Bandas de volatilidad

El sistema DEBE calcular las bandas de Bollinger: una media móvil y dos bandas separadas de ella un múltiplo de la desviación típica de la ventana.

#### Scenario: Serie plana

- **WHEN** todos los precios de la ventana son iguales
- **THEN** las dos bandas coinciden con la media

#### Scenario: Precio fuera de la banda

- **WHEN** el precio cierra por encima de la banda superior
- **THEN** el sistema lo refleja en el valor de ese día

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
