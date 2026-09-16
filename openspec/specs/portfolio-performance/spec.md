# portfolio-performance Specification

## Purpose

Mide la cartera como se mide un fondo, para que el usuario pueda saber si lo está haciendo bien y comparar lo que hace con no hacer nada.

## Requirements

### Requirement: Rentabilidad ponderada por tiempo

El sistema DEBE calcular la rentabilidad ponderada por tiempo de un periodo, neutralizando el efecto de las aportaciones y las retiradas.

#### Scenario: Aportación sin rendimiento

- **WHEN** el usuario ingresa dinero y compra, y los precios no se mueven
- **THEN** la rentabilidad ponderada por tiempo del periodo es cero

#### Scenario: Dos periodos encadenados

- **WHEN** la cartera sube un diez por ciento, recibe una aportación y vuelve a subir un diez por ciento
- **THEN** la rentabilidad del conjunto es del veintiuno por ciento, y no depende del tamaño de la aportación

### Requirement: Rentabilidad ponderada por dinero

El sistema DEBE calcular la rentabilidad ponderada por dinero, que sí tiene en cuenta cuándo entró cada euro.

#### Scenario: Acierto en el momento de aportar

- **WHEN** la mayor aportación se hace justo antes de una subida
- **THEN** la rentabilidad ponderada por dinero es superior a la ponderada por tiempo

#### Scenario: Periodo sin movimientos

- **WHEN** no hay aportaciones ni retiradas en el periodo
- **THEN** las dos rentabilidades coinciden

### Requirement: Riesgo asumido

El sistema DEBE calcular, para un periodo, la volatilidad de los rendimientos diarios, la caída máxima desde un máximo anterior y cuánto tiempo se tardó en recuperarla.

#### Scenario: Caída y recuperación

- **WHEN** la cartera cae un veinte por ciento desde su máximo y tarda tres meses en volver a él
- **THEN** el sistema informa de esa caída máxima y de esos tres meses

#### Scenario: Caída aún no recuperada

- **WHEN** la cartera está por debajo de su máximo al final del periodo
- **THEN** el sistema informa de la caída y señala que la recuperación sigue pendiente

### Requirement: Comparación con una referencia

El sistema DEBE poder comparar la rentabilidad de la cartera con la de una referencia elegida, aplicando a esta las mismas aportaciones en las mismas fechas.

#### Scenario: Mismas aportaciones en la referencia

- **WHEN** se compara con un activo tomado como referencia
- **THEN** el sistema calcula qué habría valido invertir en él las mismas cantidades en las mismas fechas

#### Scenario: Referencia sin precio algún día

- **WHEN** a la referencia le falta el precio de algún día del periodo
- **THEN** la comparación se marca como incompleta, en lugar de rellenar el hueco

### Requirement: Días incompletos y métricas

Una métrica calculada sobre un periodo con días sin precio DEBE devolverse marcada como incompleta.

#### Scenario: Periodo con huecos

- **WHEN** el periodo contiene días a los que les falta el precio de algún activo
- **THEN** las métricas se devuelven con la advertencia de que están calculadas sobre datos incompletos
