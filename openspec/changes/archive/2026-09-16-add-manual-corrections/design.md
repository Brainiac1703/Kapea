## Context

Hoy todo movimiento entra importado. Lo que existe alrededor:

- **Origen.** `TransactionOrigin` tiene `Imported` y `ManualAdjustment`. `Transaction.FromManualAdjustment` exige motivo y no lo usa nada.
- **Inmutabilidad.** `portfolio-domain` fija que un importado no se edita y que las correcciones son ajustes con motivo.
- **Deduplicación.** `ImportRepository.FindExistingFingerprintsAsync` busca huellas en la tabla de movimientos. Una fila borrada vuelve en la siguiente importación.
- **Cálculo.** `PortfolioCalculationRepository` carga todos los movimientos del usuario y el motor FIFO los proyecta. `PortfolioQueries` lee movimientos en al menos seis sitios más: búsqueda, años, tipos, caja, histórico y pendientes. `PricedAssetRepository`, `InternalTransferRepository` y `ReinterpretationRepository` también.
- **Filtro por usuario.** Cada entidad lleva un filtro global sin nombre por `UserId`.
- **Traspasos.** `InternalTransfer` guarda el movimiento de salida y el de entrada, y no se deshace una vez confirmado.
- **Tipos de cambio.** `IExchangeRateProvider.ResolveAsync(currency, date)` congela el tipo al importar.
- **Pendientes.** `CountPendingReviewAsync` cuenta traspasos propuestos y movimientos sin clasificar.

## Goals / Non-Goals

**Goals:**

- Apuntar a mano un movimiento en cualquier cuenta, como dato normal, editable y borrable.
- Corregir un importado sin perder de dónde vino ni la defensa de la cifra.
- Que ninguna lectura que alimente cifras pueda olvidarse de dejar fuera un anulado.
- Que reimportar nunca resucite un anulado, y que un manual y un importado iguales no se sumen sin que nadie lo vea.

**Non-Goals:**

- Alta masiva a mano. Para eso está la importación por fichero con un perfil.
- Editar los datos de un importado o de un ajuste.
- Deshacer un traspaso confirmado.
- Modelar ejercicios «presentados». El aviso usa el año.

## Decisions

### Tres orígenes con reglas distintas

`TransactionOrigin` gana `Manual`.

| Origen | Cómo nace | Nota o motivo | Se edita | Se quita |
|---|---|---|---|---|
| `Imported` | API o fichero | — | No | Anulándolo |
| `Manual` | *Nuevo movimiento* | Nota opcional | Sí | Borrándolo |
| `ManualAdjustment` | *Corregir* un importado | Motivo obligatorio | No | Borrándolo |

El manual es un dato de primera, igual que un importado pero sin registro de origen. El ajuste sólo existe como mitad de una corrección, y su motivo es lo que la defiende.

`Transaction.FromManualEntry(…, note)` construye el manual con una huella propia, `TransactionSource.ForManualEntry(id)`, que nunca coincide con la de un importado. `Revise(…)` cambia sus datos y rechaza cualquier otro origen. Las reglas de coherencia son las mismas `EnsureConsistent` de siempre.

El manual guarda `RegisteredAt` y `RevisedAt`, que pone el servicio con el reloj inyectado. El origen se fija al construir y no tiene mutador, así que un manual no puede pasar por importado ni al revés.

*Alternativa descartada:* reutilizar `ManualAdjustment` con motivo opcional. Mezclaría dos cosas que se leen distinto en una revisión fiscal: «lo apunté yo» y «corregí lo que trajo la plataforma».

### Anular es un estado del movimiento, no un borrado

`Transaction` gana `VoidedAt` y `VoidReason`, con `Void(reason, at)` y `Restore()`. `Void` rechaza un manual, un ajuste y un motivo vacío. La comprobación del traspaso confirmado la hace el servicio, porque el movimiento no conoce los traspasos.

*Alternativa descartada:* borrar la fila y guardar la huella en una tabla de exclusiones. Pierde la fila de origen, que es lo que explica la cifra.

### Corregir es anular y registrar en una sola transacción

`CorrectAsync(transactionId, datos, motivo)` anula el importado y crea el ajuste con el mismo motivo y un único `SaveChangesAsync`. Si el ajuste no pasa las reglas, la excepción salta antes de guardar y el importado sigue vigente.

Borrar el ajuste no restaura el importado: puede que el importado estuviera mal y ya no haya que sustituirlo. Restaurar es una acción aparte.

### Los anulados quedan fuera por defecto, con filtros de consulta con nombre

EF Core 10 admite varios filtros con nombre por entidad y desactivar sólo uno. El de movimientos pasa a tener dos:

- `Owner`: el de usuario, que existe hoy sin nombre.
- `InForce`: `VoidedAt == null`.

Todas las lecturas excluyen los anulados sin tocar su código. Sólo desactivan `InForce`, nunca `Owner`:

1. La búsqueda de huellas de la deduplicación.
2. La búsqueda y la lista de *Movimientos*.
3. El servicio de movimientos, que tiene que encontrar un anulado para restaurarlo.

*Alternativa descartada:* un `Where(VoidedAt == null)` en cada lectura. La siguiente lectura que se escriba lo olvidaría, con cifras mal y sin error.

*Salvaguarda:* `IgnoreQueryFilters()` sin argumentos quita también el de usuario. Una prueba de arquitectura busca llamadas sin nombre sobre movimientos, salvo la adopción de datos que ya la usa a propósito.

### Coincidencias entre manuales e importados

Un manual no tiene huella de origen, así que la deduplicación no lo reconoce. Se detecta por coincidencia: misma cuenta, tipo, activo, cantidad y día en la zona horaria del movimiento.

- **En la vista previa**, cada registro que coincide con un manual vigente lleva una marca y el identificador del manual. No se descarta solo: puede ser una segunda compra igual el mismo día.
- **En *Por revisar***, una consulta empareja manuales con importados de la misma cuenta. Cada pareja cuenta como pendiente hasta que se borra el manual o se marca como distinta.

Marcar como distinta guarda en el manual `DistinctFrom`, el identificador del importado. Si luego llega otro importado que coincide, vuelve a aparecer.

*Alternativa descartada:* descartar automáticamente el importado que coincide con un manual. Perdería el registro de origen en favor del apunte a mano, al revés de lo que interesa.

### Un servicio en Application

`ManualMovementService` con `RegisterAsync`, `ReviseAsync`, `DeleteAsync` (manuales y ajustes), `CorrectAsync`, `VoidAsync`, `RestoreAsync` y `MarkDistinctAsync`. Cada uno comprueba cuenta y movimiento dentro del usuario, aplica la regla del dominio, guarda y llama a `PortfolioCalculationService.RecalculateAsync`, igual que confirmar una importación.

En divisa, el tipo se resuelve con `IExchangeRateProvider` antes de construir el movimiento, y al editar se vuelve a resolver si cambian fecha o divisa. Sin tipo, se rechaza con el mismo mensaje que la importación.

### Aviso de ejercicio desde la API

`GET /api/transactions/impact?date=…&previous=…` devuelve el ejercicio más antiguo de las fechas y si es anterior al actual. `previous` se pasa al editar o corregir, porque cambiar una fecha afecta a los dos ejercicios. No bloquea nada.

*Alternativa descartada:* calcularlo en el cliente. El día que Kapea sepa qué ejercicios están presentados, la regla cambia en un solo sitio.

### Endpoints

| Método | Ruta | Qué hace |
|---|---|---|
| `POST` | `/api/transactions` | Registra un manual |
| `PUT` | `/api/transactions/{id}` | Edita un manual. 409 si no lo es |
| `DELETE` | `/api/transactions/{id}` | Borra un manual o un ajuste. 409 si es importado |
| `POST` | `/api/transactions/{id}/correct` | Anula el importado y registra el ajuste |
| `POST` | `/api/transactions/{id}/void` | Anula con motivo. 409 si no es importado o está en un traspaso confirmado |
| `POST` | `/api/transactions/{id}/restore` | Deshace la anulación |
| `POST` | `/api/transactions/{id}/distinct/{importedId}` | Marca que un manual y un importado son distintos |
| `GET` | `/api/transactions/impact` | Ejercicio afectado |

La búsqueda gana `origin` y `voided`. `TransactionResponse` gana `Note`, `RegisteredAt`, `RevisedAt`, `AdjustmentReason`, `VoidedAt` y `VoidReason`; su `Origin` distingue `Api`, `File`, `Manual` y `ManualAdjustment`, sacando API o fichero de la importación de la que procede. `RealizedResultResponse` y `ConsumedLotResponse` ganan el origen de su movimiento, para el detalle fiscal. `PendingReviewResponse` gana `ManualDuplicates`. Cada fila de la vista previa gana `MatchesManualId`.

### Pantalla

En *Movimientos*:

- **Nuevo movimiento** en la barra: cuenta, tipo, activo, cantidad, precio, importe, divisa, comisión, fecha y nota.
- En cada **manual**: *Editar* y *Borrar*.
- En cada **importado**: *Corregir*, que abre el mismo formulario con sus datos y pide motivo en lugar de nota; y *Anular* o *Deshacer anulación*.
- En cada **ajuste**: *Borrar*.
- La columna *Procedencia* deja de enseñar el valor interno y dice *API*, *Fichero*, *A mano* o *Ajuste*, con un icono distinto para cada uno. La descripción emergente de un manual lleva su nota, cuándo se registró y cuándo se editó; la de un importado, la importación de la que viene.
- El anulado va atenuado y tachado, con su motivo en la descripción emergente.
- Filtros de origen y estado.

Cada acción confirma con el aviso de ejercicio cuando corresponde.

En *Inicio*, los últimos movimientos llevan el mismo icono de procedencia. En *Fiscal*, cada transmisión y cada lote consumido lo llevan también, para ver qué parte de un resultado se apoya en datos apuntados a mano.

En la vista previa de *Importar fichero*, los registros que coinciden con un manual llevan una marca con la fecha y la nota del manual. En *Por revisar*, una sección *Posibles duplicados de movimientos manuales* con *Borrar el manual* y *Son distintos*.

## Risks / Trade-offs

- **Filtros con nombre en EF Core 10** → Se comprueba al empezar. Si no se comportan como se espera, la alternativa es un método de extensión único `InForce()` por el que pasen todas las lecturas, con la prueba de arquitectura vigilando.
- **Falsos positivos de coincidencia** → Dos compras iguales el mismo día se marcan. Es un aviso, no un descarte, y *Son distintos* lo resuelve para siempre.
- **Un anulado o un manual borrado deja huecos en el FIFO** → Puede dejar sin lotes una venta posterior. El motor ya notifica esa inconsistencia en lugar de dejar cantidades negativas.
- **Recalcular en cada cambio** → Recálculo completo del usuario, lo mismo que confirmar una importación.

## Migration Plan

Migración `ManualMovements` con `Note`, `RegisteredAt`, `RevisedAt`, `VoidedAt`, `VoidReason` y `DistinctFrom` nulos en `Transactions`. El valor nuevo del enumerado no necesita migración de datos. Ningún movimiento existente cambia.

Revertir es bajar la migración, antes borrando los manuales: con la columna de origen de vuelta a dos valores, un manual quedaría con un origen desconocido.

## Open Questions

Ninguna que cambie lo que se construye.
