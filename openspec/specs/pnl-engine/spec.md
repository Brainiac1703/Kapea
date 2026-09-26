# pnl-engine Specification

## Purpose

Calcula de forma determinista y auditable el coste, las posiciones abiertas y los resultados realizados de la cartera aplicando el criterio FIFO en euros, tal como exige la normativa española tanto para renta variable como para criptomonedas.

## Requirements

### Requirement: Consumo FIFO de lotes

Una venta DEBE consumir los lotes del mismo activo por orden ascendente de fecha de adquisición, agotando cada lote antes de pasar al siguiente. El criterio FIFO DEBE aplicarse por activo, no por cuenta: los lotes del mismo activo en distintas cuentas del usuario forman una única cola ordenada por fecha.

#### Scenario: Venta que consume un lote parcialmente

- **WHEN** se vende una cantidad menor que la restante del lote más antiguo
- **THEN** el sistema reduce la cantidad restante de ese lote en la cantidad vendida y no toca los demás

#### Scenario: Venta que abarca varios lotes

- **WHEN** se vende una cantidad mayor que la restante del lote más antiguo
- **THEN** el sistema agota ese lote, continúa con el siguiente por fecha de adquisición y registra el resultado desglosado por lote consumido

#### Scenario: Lotes en cuentas distintas

- **WHEN** el usuario mantiene lotes del mismo activo en dos cuentas y vende en una de ellas
- **THEN** el consumo sigue el orden global de fechas de adquisición del activo, con independencia de la cuenta

#### Scenario: Empate de fecha de adquisición

- **WHEN** dos lotes del mismo activo comparten instante de adquisición
- **THEN** el sistema los consume en un orden estable y reproducible, de modo que recalcular produce siempre el mismo resultado

#### Scenario: Venta sin lotes suficientes

- **WHEN** una venta excede la cantidad disponible en lotes
- **THEN** el sistema no genera un resultado parcial silencioso: marca una inconsistencia de datos indicando el activo, la fecha y la cantidad que falta

### Requirement: Conversión a euros en la fecha de la operación

Toda operación en divisa distinta del euro DEBE valorarse en EUR al tipo de cambio de la fecha de la operación, tanto en la adquisición como en la transmisión. El tipo aplicado y su fuente DEBEN conservarse junto al movimiento para que el cálculo sea reproducible y auditable.

#### Scenario: Compra en dólares

- **WHEN** se importa una compra denominada en USD
- **THEN** el coste del lote se registra en EUR al tipo de la fecha de la compra, y se conserva el tipo aplicado y su fuente

#### Scenario: Reproducibilidad

- **WHEN** se recalcula un resultado ya calculado anteriormente
- **THEN** se usa el tipo de cambio conservado con el movimiento y el resultado es idéntico, aunque la fuente de tipos haya cambiado sus datos

#### Scenario: Tipo de cambio no disponible

- **WHEN** no existe tipo de cambio para la fecha de la operación
- **THEN** el sistema usa el tipo publicado más reciente anterior a esa fecha y deja constancia de la sustitución en el movimiento

#### Scenario: Operación entre dos criptomonedas

- **WHEN** se permuta un activo por otro sin intervención de moneda fiduciaria
- **THEN** ambas patas se valoran en EUR en la fecha de la operación, y esa valoración fija el importe de transmisión de una y el coste de adquisición de la otra

### Requirement: Comisiones en el coste y en el resultado

Las comisiones de adquisición DEBEN incrementar el coste del lote y las comisiones de transmisión DEBEN reducir el importe de la transmisión, ambas convertidas a EUR en la fecha de la operación.

#### Scenario: Comisión de compra

- **WHEN** una compra incluye comisión
- **THEN** el coste del lote es el importe pagado más la comisión, en EUR

#### Scenario: Comisión de venta

- **WHEN** una venta incluye comisión
- **THEN** el importe de transmisión es lo recibido menos la comisión, en EUR

#### Scenario: Comisión repartida entre lotes

- **WHEN** una venta consume varios lotes
- **THEN** la comisión se reparte entre ellos en proporción a la cantidad consumida de cada uno

### Requirement: Resultado realizado por transmisión

Cada transmisión DEBE generar un resultado realizado con el importe de transmisión, el coste de adquisición de los lotes consumidos, el resultado en EUR, la fecha de devengo y el desglose por lote.

#### Scenario: Desglose del resultado

- **WHEN** el usuario consulta un resultado realizado
- **THEN** el sistema muestra importe de transmisión, coste de adquisición, resultado, fecha y la lista de lotes consumidos con su cantidad y su coste

#### Scenario: Resultados por ejercicio

- **WHEN** el usuario consulta los resultados realizados de un ejercicio fiscal
- **THEN** el sistema devuelve los resultados cuya fecha de devengo cae dentro de ese ejercicio, agregados por activo y con el total del ejercicio

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

### Requirement: Splits

Un split DEBE ajustar la cantidad y el coste unitario de los lotes del activo anteriores a su fecha efectiva, conservando el coste total de cada lote y su fecha de adquisición original.

#### Scenario: Split aplicado

- **WHEN** se registra un split con una proporción determinada
- **THEN** las cantidades restantes de los lotes anteriores a su fecha efectiva se multiplican por esa proporción, el coste total de cada lote no varía y su fecha de adquisición se mantiene

#### Scenario: Split posterior a la venta

- **WHEN** un split tiene fecha efectiva posterior a una venta ya calculada
- **THEN** el resultado realizado de esa venta no se modifica

### Requirement: Dividendos

Un dividendo en efectivo NO DEBE alterar los lotes del activo. DEBE registrarse como rendimiento del capital, en EUR a la fecha de abono, conservando por separado el importe bruto y las retenciones cuando el origen las aporte.

#### Scenario: Dividendo en efectivo

- **WHEN** se importa un dividendo en efectivo
- **THEN** el sistema lo registra como rendimiento del capital sin modificar las cantidades ni los costes de los lotes

#### Scenario: Dividendo con retención

- **WHEN** el origen aporta el importe bruto y la retención practicada
- **THEN** el sistema conserva ambos importes por separado, en EUR a la fecha de abono

### Requirement: Recálculo determinista

El cálculo DEBE ser una función determinista del conjunto de movimientos: sobre los mismos movimientos, recalcular desde cero DEBE producir exactamente los mismos lotes, posiciones y resultados. Importar un movimiento con fecha anterior a otros ya procesados DEBE provocar el recálculo de todo lo afectado desde esa fecha.

#### Scenario: Recálculo idéntico

- **WHEN** se recalcula la cartera completa sin haber cambiado ningún movimiento
- **THEN** los lotes, posiciones y resultados obtenidos son idénticos a los anteriores

#### Scenario: Movimiento retroactivo

- **WHEN** se importa un movimiento con fecha anterior a otros ya calculados
- **THEN** el sistema recalcula los lotes y resultados posteriores a esa fecha y refleja las diferencias

### Requirement: Exclusión de movimientos no clasificados

Los movimientos de tipo `Unknown` y los pendientes de confirmar un traspaso interno NO DEBEN participar en el cálculo, y el sistema DEBE advertir de forma visible de que los resultados están incompletos mientras existan.

#### Scenario: Resultados incompletos

- **WHEN** el usuario consulta posiciones o resultados y existen movimientos sin clasificar
- **THEN** el sistema devuelve el cálculo excluyéndolos y advierte de cuántos movimientos quedan pendientes de clasificar

### Requirement: Precisión y redondeo

Los cálculos intermedios DEBEN realizarse con aritmética decimal exacta, sin coma flotante binaria. El redondeo a dos decimales solo DEBE aplicarse en la presentación de importes en euros, nunca en los valores intermedios almacenados.

#### Scenario: Suma de resultados

- **WHEN** se agregan los resultados de un ejercicio compuesto por muchas operaciones
- **THEN** el total coincide con la suma de los resultados individuales sin desviación acumulada por redondeo

### Requirement: Incoherencias del cálculo visibles

Las incoherencias que el cálculo detecta —una venta sin lotes suficientes, un split sobre un activo sin posición y cualquier otra— DEBEN llegar a quien consulta la cartera, con el activo, la fecha y lo que falta, en un texto comprensible y no como un código interno. Mientras exista alguna, las cifras de la cartera DEBEN presentarse como incompletas, igual que cuando hay movimientos sin clasificar. El sistema NO DEBE presentar como completas unas cifras que ha calculado descartando un movimiento.

#### Scenario: Venta sin lotes suficientes en la cartera

- **WHEN** el usuario consulta la cartera y una venta ha quedado sin procesar por no encontrar lotes suficientes
- **THEN** la cartera advierte de esa incoherencia indicando el activo, la fecha y la cantidad que falta, y no presenta las cifras como completas

#### Scenario: Cartera sin incoherencias

- **WHEN** el cálculo no detecta ninguna incoherencia
- **THEN** la cartera no muestra ninguna advertencia de este tipo

#### Scenario: Incoherencia resuelta

- **WHEN** el usuario corrige el dato que faltaba y se recalcula
- **THEN** la advertencia desaparece de la cartera sin necesidad de ninguna otra acción

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
