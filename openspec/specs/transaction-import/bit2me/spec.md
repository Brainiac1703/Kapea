# transaction-import/bit2me Specification

## Purpose

Importa el histórico de operaciones y movimientos de Bit2Me mediante su API REST con credenciales de solo lectura, cubriendo tanto la carga inicial completa como las sincronizaciones incrementales programadas.

## Requirements

### Requirement: Importación vía API de Bit2Me

El sistema DEBE importar desde la API de Bit2Me las operaciones de compraventa, las conversiones entre activos, los depósitos, las retiradas, las comisiones y los rendimientos asociados a una cuenta de plataforma `Bit2Me`, usando su credencial registrada.

#### Scenario: Importación de operaciones

- **WHEN** se ejecuta una importación de una cuenta Bit2Me con credencial activa
- **THEN** el sistema normaliza las operaciones del periodo solicitado a movimientos y produce una ejecución con sus recuentos

#### Scenario: Cuenta sin credencial activa

- **WHEN** se intenta importar de una cuenta Bit2Me sin credencial activa
- **THEN** el sistema rechaza la importación indicando que falta una credencial válida

### Requirement: Cobertura de los productos de la cuenta

El adaptador DEBE recuperar los movimientos de todos los productos de la cuenta que la credencial permita leer —cartera de contado y productos de rendimiento incluidos— y DEBE dejar constancia de los productos a los que no ha podido acceder.

#### Scenario: Producto inaccesible

- **WHEN** la credencial no permite leer uno de los productos de la cuenta
- **THEN** la importación continúa con el resto y la ejecución deja constancia del producto omitido y del motivo

#### Scenario: Rendimientos

- **WHEN** la cuenta genera rendimientos de un producto de ahorro
- **THEN** cada abono se normaliza como movimiento de tipo `Reward` o `Interest`, según corresponda, con su activo y su cantidad

### Requirement: Conversión directa entre activos

Una conversión de un activo a otro DEBE normalizarse como una venta del activo de origen y una compra del activo de destino, ambas con la misma fecha y enlazadas entre sí, para que el criterio FIFO y el hecho imponible se calculen correctamente.

#### Scenario: Permuta entre criptomonedas

- **WHEN** el usuario convierte directamente un activo en otro dentro de Bit2Me
- **THEN** el sistema genera una venta del activo de origen y una compra del de destino, enlazadas, con valoración en EUR en la fecha de la operación

### Requirement: Carga inicial completa, paginación y límites

En la primera importación de una cuenta el sistema DEBE recuperar el histórico completo disponible paginando hasta agotarlo, respetando los límites de frecuencia de la plataforma y reintentando con espera creciente ante errores temporales.

#### Scenario: Histórico paginado

- **WHEN** el histórico excede el tamaño de página de la API
- **THEN** el sistema recorre todas las páginas antes de persistir la ejecución

#### Scenario: Límite de frecuencia alcanzado

- **WHEN** Bit2Me responde señalando exceso de peticiones
- **THEN** el sistema espera y reintenta con espera creciente, y la importación continúa desde donde estaba

#### Scenario: Interrupción durante la paginación

- **WHEN** la recuperación se interrumpe antes de agotar las páginas
- **THEN** la ejecución queda fallida, no se persiste ningún movimiento y el instante de última importación correcta no avanza
