## Purpose

Pone cifras a cuánto arriesgar en cada decisión, que es lo que protege el capital cuando una idea sale mal.

## ADDED Requirements

### Requirement: Tamaño de la posición por riesgo

El sistema DEBE poder calcular cuántas unidades comprar a partir del capital, del porcentaje que se acepta perder en esa operación y de la distancia hasta el nivel de salida.

#### Scenario: Activo que se mueve mucho

- **WHEN** dos activos tienen el mismo capital asignado y uno se mueve el doble que el otro
- **THEN** el que más se mueve recibe una posición menor, para arriesgar lo mismo en los dos

#### Scenario: Sin nivel de salida

- **WHEN** no hay nivel de salida declarado
- **THEN** el sistema no propone tamaño, en lugar de suponer uno

### Requirement: Tope por posición

El sistema DEBE avisar cuando una compra dejaría una posición por encima del porcentaje máximo declarado sobre el total de la cartera.

#### Scenario: Compra que concentra de más

- **WHEN** la compra propuesta llevaría un activo por encima de su tope
- **THEN** el sistema lo advierte y dice cuánto cabría sin pasarse

### Requirement: Aviso de concentración

El sistema DEBE señalar cuando unos pocos activos superan un porcentaje declarado del total.

#### Scenario: Cartera concentrada

- **WHEN** tres activos suman más del umbral declarado
- **THEN** la cartera lo advierte, diciendo cuáles y cuánto pesan

### Requirement: Bandas de rebalanceo

El sistema DEBE poder declarar un peso objetivo por activo o por clase y avisar cuando el peso real se desvía más de lo tolerado, diciendo qué operación lo devolvería al objetivo.

#### Scenario: Activo que se ha disparado

- **WHEN** un activo supera su peso objetivo más la banda
- **THEN** el sistema propone cuánto habría que recortar para volver al objetivo

#### Scenario: Desviación dentro de la banda

- **WHEN** la desviación es menor que la banda declarada
- **THEN** no se propone nada, para no generar operaciones que solo pagan comisiones

### Requirement: Seguimiento de niveles de salida

Una posición abierta DEBE poder llevar un nivel de salida y un objetivo, y el sistema DEBE avisar cuando el precio los alcanza.

#### Scenario: Precio que alcanza el objetivo

- **WHEN** el último precio supera el objetivo declarado de una posición
- **THEN** el sistema lo avisa, sin ejecutar nada
