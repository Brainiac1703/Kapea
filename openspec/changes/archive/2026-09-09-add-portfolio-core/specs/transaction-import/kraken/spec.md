## Purpose

Importa el histórico de operaciones y movimientos de fondos de Kraken mediante su API REST con credenciales de solo lectura, tanto en la carga inicial completa como en las sincronizaciones incrementales programadas.

## ADDED Requirements

### Requirement: Importación vía API de Kraken

El sistema DEBE importar desde la API de Kraken las operaciones de compraventa, los depósitos, las retiradas, las comisiones y las recompensas de staking asociadas a una cuenta de plataforma `Kraken`, usando su credencial registrada.

#### Scenario: Importación de operaciones

- **WHEN** se ejecuta una importación de una cuenta Kraken con credencial activa
- **THEN** el sistema normaliza las operaciones del periodo solicitado a movimientos y produce una ejecución con sus recuentos

#### Scenario: Cuenta sin credencial activa

- **WHEN** se intenta importar de una cuenta Kraken sin credencial activa
- **THEN** el sistema rechaza la importación indicando que falta una credencial válida

### Requirement: Carga inicial completa y paginación

En la primera importación de una cuenta el sistema DEBE recuperar el histórico completo disponible, paginando hasta agotar los resultados. Una interrupción durante la paginación NO DEBE dejar la cuenta con un histórico parcial dado por completo.

#### Scenario: Histórico paginado

- **WHEN** el histórico de la cuenta excede el tamaño de página de la API
- **THEN** el sistema recorre todas las páginas antes de persistir la ejecución

#### Scenario: Interrupción durante la paginación

- **WHEN** la recuperación se interrumpe antes de agotar las páginas
- **THEN** la ejecución queda fallida, no se persiste ningún movimiento y el instante de última importación correcta no avanza

### Requirement: Respeto de los límites de la API

El sistema DEBE respetar los límites de frecuencia de Kraken, espaciando las peticiones y reintentando con espera creciente cuando la plataforma señale exceso de uso, sin perder registros ni duplicarlos.

#### Scenario: Límite de frecuencia alcanzado

- **WHEN** Kraken responde señalando exceso de peticiones
- **THEN** el sistema espera y reintenta con espera creciente, y la importación continúa desde donde estaba

#### Scenario: Reintentos agotados

- **WHEN** se agotan los reintentos configurados
- **THEN** la ejecución queda fallida con su motivo y sin movimientos persistidos

### Requirement: Normalización de símbolos y pares de Kraken

El adaptador DEBE traducir los símbolos propios de Kraken y sus pares de negociación a los activos y divisas del catálogo, incluyendo los alias históricos que la plataforma mantiene para algunos activos.

#### Scenario: Alias histórico

- **WHEN** Kraken devuelve un símbolo con su nomenclatura propia para un activo del catálogo
- **THEN** el movimiento se asocia al activo canónico correspondiente

#### Scenario: Par de negociación

- **WHEN** una operación se expresa como par de negociación entre dos activos
- **THEN** el sistema la normaliza indicando el activo operado, la divisa o activo de contrapartida y el precio unitario en esa contrapartida

#### Scenario: Símbolo no traducible

- **WHEN** un símbolo de Kraken no se puede traducir a ningún activo conocido
- **THEN** el sistema crea el activo como no verificado y marca el movimiento para revisión
