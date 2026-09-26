## MODIFIED Requirements

### Requirement: Posiciones abiertas

El sistema DEBE calcular, por activo, la cantidad en cartera y su coste medio en EUR a partir de los lotes con cantidad restante. Cuando exista un precio de mercado disponible, DEBE calcular además el valor actual y el resultado latente, indicando el instante al que corresponde ese precio.

Cada posición DEBE informar también de su peso sobre el valor total de la cartera, de las comisiones acumuladas en sus adquisiciones y del resultado ya realizado en ese activo.

#### Scenario: Posición con precio disponible

- **WHEN** el usuario consulta sus posiciones abiertas y hay precio de mercado del activo
- **THEN** el sistema devuelve cantidad, coste medio, valor actual, resultado latente y el instante del precio usado

#### Scenario: Posición sin precio disponible

- **WHEN** no hay precio de mercado para un activo en cartera
- **THEN** el sistema devuelve cantidad y coste medio, y señala explícitamente que el valor actual y el resultado latente no están disponibles

#### Scenario: Activo totalmente vendido

- **WHEN** todos los lotes de un activo tienen cantidad restante cero
- **THEN** el activo no aparece entre las posiciones abiertas y sus resultados realizados siguen siendo consultables

#### Scenario: Peso de una posición

- **WHEN** el usuario consulta sus posiciones abiertas y todas tienen precio
- **THEN** cada una indica qué porcentaje del valor total de la cartera representa, y la suma de los pesos es el total

#### Scenario: Peso con precios incompletos

- **WHEN** falta el precio de alguna posición
- **THEN** los pesos se calculan sobre el valor de las posiciones que sí se han podido valorar, y se indica que son parciales

#### Scenario: Comisiones acumuladas de una posición

- **WHEN** el usuario consulta una posición cuyas adquisiciones tuvieron comisión
- **THEN** la posición informa de la suma de esas comisiones

## ADDED Requirements

### Requirement: Resultado acumulado de toda la cartera

El sistema DEBE calcular el resultado realizado acumulado desde el inicio, el resultado latente de las posiciones abiertas y la suma de ambos.

Es una pregunta distinta de la de un ejercicio fiscal: aquella responde qué se declara este año, y esta cuánto se ha ganado con la cartera.

#### Scenario: Resultado total

- **WHEN** el usuario consulta el resumen de la cartera
- **THEN** obtiene el resultado realizado acumulado, el latente y su suma

#### Scenario: Resultado acumulado por activo

- **WHEN** el usuario consulta una posición abierta de un activo que ya ha vendido antes en parte
- **THEN** la posición informa del resultado ya realizado en ese activo, además del latente

#### Scenario: Resultado latente incompleto

- **WHEN** falta el precio de alguna posición
- **THEN** el resultado latente se presenta como incompleto, indicando cuántas posiciones no se han podido valorar

### Requirement: Valor total del patrimonio

El sistema DEBE calcular el valor total como la suma del valor de mercado de las posiciones abiertas y el efectivo disponible en las cuentas.

#### Scenario: Total con posiciones y efectivo

- **WHEN** el usuario consulta el resumen de la cartera
- **THEN** obtiene el valor de las posiciones, el efectivo y su suma

#### Scenario: Total incompleto

- **WHEN** falta el precio de alguna posición o el saldo de alguna cuenta no se puede calcular
- **THEN** el total se presenta como incompleto, indicando qué falta

### Requirement: Agrupación por clase de activo

Las posiciones DEBEN poder consultarse agrupadas por clase de activo, con el subtotal de coste, valor y resultado de cada grupo y su peso sobre el total.

Incorporar una clase de activo nueva NO DEBE requerir una vista aparte.

#### Scenario: Cartera con acciones y criptomonedas

- **WHEN** el usuario consulta la cartera
- **THEN** las posiciones aparecen agrupadas por clase, cada grupo con su subtotal y su peso

#### Scenario: Clase de activo nueva

- **WHEN** se incorpora una posición de una clase de activo que no existía en la cartera
- **THEN** aparece como un grupo más, sin ninguna vista específica para ella

#### Scenario: Rendimientos cobrados

- **WHEN** el usuario consulta el resumen de la cartera
- **THEN** obtiene los dividendos, intereses y recompensas cobrados, con su retención, agrupados por clase de activo
