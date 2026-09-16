## Purpose

Calcula sobre una serie de precios los indicadores que sirven para leer su tendencia, y que son la base de las señales de compra y venta de fases posteriores.

## ADDED Requirements

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
