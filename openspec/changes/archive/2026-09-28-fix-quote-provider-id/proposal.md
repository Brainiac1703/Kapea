## Why

Una criptomoneda añadida buscándola no tiene precio, y no lo tendrá nunca por mucho que se refresque.

El usuario añadió QNT el 2026-09-28 y la lista lo enseña sin precio. En los registros: «Sin precio para 1 de 10 activos de Crypto». La causa es que `CoinGeckoMarketPriceProvider` traduce símbolo a identificador con `CoinIds`, un diccionario escrito a mano de unas veinticinco monedas, y QNT no es una de ellas.

Kapea **sí guardó** el identificador de CoinGecko de QNT al añadirlo: `add-asset-search` lo hace precisamente para esto, y el camino del histórico lo usa. El de la cotización nunca se enteró, porque el puerto sólo recibe símbolos.

`market-prices` ya lo exige: guardar el identificador «y usarlo después para pedir sus precios». Esto es un incumplimiento, no una funcionalidad que falte.

No tiene nada que ver con estar o no en cartera. Lo que está en cartera entró importando y son las monedas grandes, que sí están en el diccionario. Si el usuario compra QNT mañana, seguirá sin precio.

## What Changes

- **El puerto del precio actual recibe el identificador del proveedor** cuando el activo lo tiene guardado, igual que ya hace el del histórico.
- **CoinGecko lo usa si lo hay y recurre al diccionario si no.** El diccionario no desaparece: sigue siendo la única vía para lo que entró importando y no tiene identificador.
- **Yahoo no cambia.** Traduce por regla y no por lista, así que el identificador no le hace falta.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `market-prices`: el identificador guardado vale para las dos peticiones, la de la serie y la de la cotización, no sólo para la primera.

## Impact

- **`Kapea.Application/Abstractions/ExchangeRates.cs`**: la firma de `IMarketPriceProvider.GetPricesAsync`.
- **`Kapea.Infrastructure/MarketData`**: los tres implementadores —`MarketPriceDispatcher`, `CoinGeckoMarketPriceProvider` y `YahooMarketPriceProvider`—.
- **`WatchlistRepository`** y **`PortfolioQueries`**, los dos llamantes, que ya tienen la entidad del activo delante.
- **Sin migración**: el identificador ya está guardado.
- **Fuera de alcance, y decidido con el usuario**: guardar el precio de ahora en base de datos, el refresco periódico, y recurrir al último cierre del histórico cuando no hay cotización.
- **Fuera de alcance, y de otro tamaño**: el 401 de `v7/finance/quote` de Yahoo, que deja las acciones sin precio. No es límite de peticiones sino que ese endpoint exige cookie y *crumb* de sesión, que Kapea no gestiona. El histórico usa `v8/finance/chart`, que no lo pide, y por eso funciona.
