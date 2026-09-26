## Why

Para seguir un activo hay que escribir su símbolo exacto y saber de antemano si es cripto o acción. Hay que saber que Cardano es ADA. Quien no lo sepa no puede añadirlo.

Y hay algo peor debajo. El identificador con el que CoinGecko conoce cada moneda está escrito a mano en una lista de unas veinte: fuera de ella, una cripto simplemente no tiene precios, y por tanto tampoco señales. La lista se hizo así a conciencia, porque varios símbolos los comparten monedas distintas y adivinar pondría el precio de otra cosa sin que nada fallara. Pero deja el seguimiento cojo justo donde más se usaría.

Buscar resuelve las dos cosas a la vez: el usuario escribe un nombre y elige, y el identificador del proveedor viene con lo que eligió. Deja de haber nada que adivinar.

## What Changes

- Una caja de búsqueda al añadir un activo: se escribe un nombre o un símbolo y se busca en los proveedores a la vez, sin elegir antes el tipo.
- Cada resultado dice qué es —cripto o acción—, cómo se llama y dónde cotiza, para poder distinguir dos cosas que comparten símbolo.
- Al elegir uno, el activo guarda con qué identificador lo conoce su proveedor. Los precios dejan de depender de una lista escrita a mano.
- Un resultado que corresponde a un activo que ya está en el catálogo no crea uno nuevo: se reconoce y se usa el que hay.
- Seguir sigue funcionando escribiendo el símbolo, como hasta ahora, para quien ya lo sabe.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `market-prices`: un proveedor debe poder buscar activos por nombre o símbolo, y decir con qué identificador conoce a cada uno.
- `portfolio-domain`: un activo debe poder guardar con qué identificador lo conoce cada proveedor de precios.
- `watchlist`: añadir un activo debe poder hacerse eligiendo un resultado de búsqueda, sin duplicar lo que ya existe.

## Impact

- `src/Kapea.Application/Abstractions/`: el puerto de búsqueda de activos.
- `src/Kapea.Infrastructure/MarketData/`: búsqueda en CoinGecko y en Yahoo, y el identificador guardado en lugar del mapa fijo.
- `src/Kapea.Domain/Assets/Asset.cs`: el identificador del proveedor, con su migración.
- `src/Kapea.Application/Watchlist/`: añadir desde un resultado.
- `src/Kapea.Api/Endpoints/` y `src/Kapea.Client/Pages/Watchlist.razor`, con sus recursos.
