## ADDED Requirements

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
