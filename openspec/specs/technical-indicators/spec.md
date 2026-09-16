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

El sistema DEBE calcular el recorrido medio diario de una serie, que mide cuánto se mueve un activo de un cierre al siguiente.

Se calcula sobre cierres y no sobre máximos y mínimos porque la serie guardada solo tiene cierres: no todos los proveedores dan el rango del día, y usarlo dejaría el indicador disponible para unos activos y no para otros.

#### Scenario: Activo tranquilo y activo movido

- **WHEN** se calcula sobre dos activos con recorridos distintos
- **THEN** el más movido tiene un recorrido medio mayor

#### Scenario: Serie con un solo día

- **WHEN** la serie tiene un único día
- **THEN** no se devuelve ningún valor, porque no hay variación que medir
