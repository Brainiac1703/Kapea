## 1. El puerto lleva el identificador

- [x] 1.1 Añadir en `Kapea.Application/Abstractions/ExchangeRates.cs` el tipo con lo que hace falta para resolver un activo —símbolo canónico e identificador opcional— y cambiar la firma de `IMarketPriceProvider.GetPricesAsync`, dejando el diccionario devuelto indexado por símbolo. Se verifica viendo que el compilador señala los tres implementadores y los dos llamantes.
- [x] 1.2 Pasar el identificador desde `WatchlistRepository` y `PortfolioQueries`, que ya tienen la entidad del activo delante. Se verifica compilando y con la batería existente en verde.
- [x] 1.3 Adaptar `MarketPriceDispatcher` y `YahooMarketPriceProvider` sin cambiar su comportamiento: el despachador sigue clasificando y cacheando por símbolo, y Yahoo sigue traduciendo por regla. Se verifica con sus pruebas existentes en verde.

## 2. CoinGecko resuelve por identificador

- [x] 2.1 Usar el identificador guardado cuando lo haya y recurrir a `CoinIds` sólo cuando no, actualizando el comentario del diccionario. Se verifica con una prueba: una moneda con identificador que no está en el diccionario devuelve precio.
- [x] 2.2 Probar que una moneda sin identificador se sigue resolviendo por el diccionario, y que las dos vías caben en la misma petición sin que un activo irresoluble impida el precio de los demás. Se verifica con dos pruebas más.

## 3. Cierre

- [x] 3.1 Ejecutar la batería completa, también con `LANG=en_US`. Se verifica en verde.
- [x] 3.2 Comprobar en local que QNT enseña precio en la lista de seguimiento, y que las cifras validadas de la cartera no se mueven.
