## Why

Kapea dice que un activo tiene precios y luego lo enseña sin ninguno.

El usuario añadió QNT escribiendo su símbolo. No hubo aviso, y QNT lleva días en la lista con la columna de precio vacía. La comprobación **no se equivocó**: QNT tiene 2.971 días de histórico guardado. Lo que hizo fue preguntar por la mitad equivocada.

`HasPricesAsync` pregunta al proveedor del **histórico**. Lo que la lista enseña en esa columna es la **cotización de ahora**, que sale de otro proveedor al que nadie pregunta al dar de alta. Un activo puede tener gráfica y no tener precio, y ese es justo el caso que se cuela sin avisar.

Por qué depende de cómo se añada, que no era lo que parecía: el alta desde la búsqueda guarda el identificador del proveedor y la cotización ya lo usa, así que resuelve. El alta por símbolo no lo guarda, y si el símbolo no está en la lista escrita a mano de CoinGecko, la cotización no resuelve nunca. La serie sí, porque Yahoo traduce por regla y no por lista.

## What Changes

- **La comprobación cubre las dos cosas que el usuario va a ver**: la serie histórica y la cotización de ahora.
- **El aviso dice cuál falta**, porque el remedio no es el mismo. Sin cotización, volver a añadirlo eligiéndolo de la búsqueda suele arreglarlo: el sistema reconoce el activo que ya existe y le graba el identificador. Sin serie no hay nada que hacer desde la aplicación.
- **El alta sigue sin bloquearse.** Lo que hoy no tiene precio puede tenerlo mañana, y esa decisión no cambia.
- **Un proveedor caído sigue contándose como ausencia de precio.** Desde un aviso de alta no se distingue, y al usuario le da igual: lo que ve es que no hay precio.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `watchlist`: al añadir un activo, el sistema comprueba la serie y la cotización, y dice cuál de las dos falta en lugar de un sí o no que puede acertar y engañar a la vez.

## Impact

- **`Kapea.Application/Watchlist/WatchlistService.cs`**: `HasPricesAsync` pasa a consultar también `IMarketPriceProvider` y a devolver qué falta, no un booleano.
- **`Kapea.Shared/Contracts/WatchlistContracts.cs`**: `WatchAssetResponse.HasPrices` deja de ser un `bool`.
- **`Kapea.Client/Pages/Watchlist.razor`** y los recursos `UiStrings`: dos avisos donde había uno.
- **Una petición más al dar de alta**: dos en vez de una. Es una acción manual y puntual, y la alternativa es que el usuario lo descubra días después mirando una fila vacía.
- **Sin migración.**
- **Fuera de alcance**: quitar la vía de alta por símbolo; deducir el identificador a partir del símbolo, que es justamente lo que la lista escrita a mano evita a propósito porque varios símbolos los comparten monedas distintas; y arreglar los activos ya dados de alta sin identificador, que se resuelve volviéndolos a añadir desde la búsqueda.
