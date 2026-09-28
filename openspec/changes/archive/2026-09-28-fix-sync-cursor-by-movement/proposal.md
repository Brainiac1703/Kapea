## Why

La sincronización incremental pide desde **la hora del reloj** de la pasada anterior, no desde la fecha del último movimiento. Un movimiento que la plataforma publica unos segundos después de que ocurriera —o con el reloj algo desfasado— tiene una fecha anterior a esa hora pero aparece después de ella: la pasada siguiente arranca por encima de su fecha y **no se pide nunca más**. Se pierde en silencio.

El cursor es `MAX(ImportRuns.CoversUntil)`, y `CoversUntil` se rellena con el instante en que corrió la pasada. Se agrava con los movimientos de monedero de Bit2Me, que se descargan enteros y se recortan en el cliente contra ese mismo cursor.

La especificación actual describe el defecto: «solicita a la plataforma solo los movimientos posteriores al instante de la última importación correcta». Este cambio corrige las dos cosas, el código y la frase.

## What Changes

- **Se pide desde el último movimiento que la cuenta ya tiene**, no desde la hora de la última ejecución.
- **Sin columna nueva ni migración**: esa fecha ya está en los propios movimientos.
- **La relectura completa no cambia**: sigue ignorando el cursor y arrancando desde el principio.
- **Una ejecución fallida sigue sin mover el punto de partida**, ahora porque no deja movimientos.

## Capabilities

### Modified Capabilities

- `transaction-import`: la sincronización incremental parte del último movimiento conocido y no del instante de la última ejecución.

## Impact

- **`Kapea.Application/Import/ImportPorts.cs`** y **`Kapea.Infrastructure/Persistence/Stores/ImportRepository.cs`**: una consulta nueva, `MAX(OccurredAt)` por cuenta.
- **`Kapea.Application/Synchronization/SynchronizationService.cs`**: de dónde sale `from`.
- **`CoversUntil` se conserva**: sigue describiendo qué cubrió cada ejecución, sólo deja de mandar sobre desde dónde se pide.
- **Se relee de más**: lo repetido lo descarta la deduplicación, que ya existe. Lo que se dejaba de leer no volvía.
- **Fuera de alcance**: cualquier cambio en los adaptadores.
