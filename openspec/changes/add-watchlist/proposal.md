## Why

Kapea sólo sabe de los activos que el usuario ya compró alguna vez. El catálogo se llena al importar movimientos, el histórico de precios cubre «desde la primera adquisición», y los sistemas se evalúan sobre lo que haya en el catálogo con precios.

De ahí sale una contradicción: un sistema de entrada existe para decir *cuándo comprar*, y Kapea sólo puede aplicarlo a lo que ya se compró. La señal que más valdría —la del activo en el que se está pensando entrar— no se puede emitir, porque ese activo no existe para la aplicación.

Y al revés: al vender entero un activo desaparece de la vista, justo cuando puede interesar seguirlo para volver a entrar. Acaba de pasar con PAXG.

## What Changes

- Una lista de **seguimiento**: los activos que el usuario vigila, con o sin posición.
- Lo que hay en cartera está en seguimiento por definición, sin tener que añadirlo ni mantenerlo a mano. Vender del todo no saca nada de la lista.
- Se pueden añadir activos que nunca se han tenido, y quitarlos. Al añadir uno, el sistema dice si hay precios para él en lugar de dejar una fila muerta.
- El histórico de precios deja de depender de haber comprado: se descarga para lo que está en seguimiento, con la profundidad que pidan los sistemas declarados.
- Las señales se presentan sabiendo la situación de cada activo: en uno sin posición, una señal de salida no dice nada; en uno con posición, cuentan las dos.
- Las ideas se apoyan en la misma lista, de modo que anotar una idea sobre un activo que no se tiene deja de ser un callejón sin salida.

## Capabilities

### New Capabilities

- `watchlist`: qué activos se vigilan, cómo entran y salen de esa lista, y qué se ve de cada uno.

### Modified Capabilities

- `price-history`: el alcance de la serie pasa a depender del seguimiento y no de la primera adquisición.
- `strategy-signals`: una señal debe decir si el activo se tiene o sólo se vigila, y una señal de salida sobre algo que no se tiene no debe presentarse como accionable.
- `external-ideas`: una idea puede registrarse sobre un activo en seguimiento aunque no se tenga, y registrarla lo pone en seguimiento.

## Impact

- `src/Kapea.Domain/Assets/`: el seguimiento como parte del catálogo, con su migración.
- `src/Kapea.Application/MarketData/`: qué activos necesitan serie y desde cuándo.
- `src/Kapea.Infrastructure/Persistence/Stores/StrategyStore.cs`: las series que alimentan el motor salen del seguimiento.
- `src/Kapea.Api/Endpoints/`: alta, baja y consulta de la lista.
- `src/Kapea.Client/Pages/`: la pantalla de Seguimiento y las señales, con sus recursos.
