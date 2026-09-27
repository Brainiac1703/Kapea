## Why

Un fallo del proveedor de precios deja hoy un hueco de histórico **perdido para siempre**, y lo hace en silencio.

El 2026-09-27, después de poner al día producción con 50.708 días de golpe, Yahoo empezó a devolver 401 por límite de peticiones. La pantalla aguantó, que es lo que la especificación pide. Pero `YahooPriceHistoryProvider` traga el fallo, devuelve una serie vacía y anota «se reintentará», mientras que `PriceHistoryUpdater` deja constancia del tramo **aunque no haya venido nada** —que es justo el mecanismo que impide volver a pedirlo—. Las dos piezas son razonables por separado y juntas se contradicen: el reintento que el proveedor promete no ocurre nunca.

La asimetría lo empeora. Hacia delante manda lo guardado, así que un fallo en la puesta al día sí se recupera solo. Hacia atrás manda lo pedido: en cuanto `RequestedFrom` alcanza el suelo del activo, el hueco de relleno no se vuelve a producir. Y un activo sin ningún precio guardado cuyo primer tramo falla se queda sin las dos vías a la vez, porque el último día conocido pasa a salir de lo pedido. Ya se perdió así el histórico de USDG-USD.

## What Changes

- **Un proveedor puede decir que ha fallado**, y eso deja de confundirse con «no hay nada aquí». Hoy el puerto sólo devuelve una colección de precios, y una lista vacía significa las dos cosas a la vez.
- **El alcance sólo se anota cuando el proveedor ha contestado.** Un tramo que se pidió y vino vacío se sigue anotando, porque esa era la razón de existir de `PriceHistoryReach` y sigue siendo válida. Un tramo que falló no se anota, y por tanto reaparece como hueco en la vuelta siguiente.
- **El fallo no se reintenta dentro de la misma pasada.** Fue pedir demasiado lo que lo provocó; insistir en el momento sólo alarga el castigo. La siguiente ejecución programada es suficiente.
- **Lo que sí se descargó antes del fallo se conserva.** No se tira una serie de cinco años porque el último tramo no llegara.
- **Una pasada dice cuántos activos se quedaron a medias por un fallo**, no sólo cuántos días escribió. Hoy un relleno que falla entero y otro que va perfecto se leen igual en el registro, y ése es el motivo de que esto tardara meses en verse.

No hace falta campo nuevo ni migración: la corrección está en **no** escribir, no en escribir más.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `price-history`: el alcance de lo pedido distingue un tramo sin datos de un tramo que el proveedor no pudo contestar, y sólo el primero cuenta como pedido. La degradación ante un proveedor caído deja de perder historia.

## Impact

- **`Kapea.Application/Abstractions/PriceHistory.cs`**: `IPriceHistoryProvider.GetHistoryAsync` pasa a devolver un resultado que, además de los precios, dice si el proveedor pudo contestar. Es el punto que decide el resto del cambio.
- **`Kapea.Application/MarketData/PriceHistoryUpdater.cs`**: condiciona `RecordReachAsync` a que el tramo se haya contestado, y cuenta los fallos para el registro.
- **`Kapea.Infrastructure/MarketData`**: los tres implementadores del puerto —`YahooPriceHistoryProvider`, `CoinGeckoPriceHistoryProvider` y `PriceHistoryDispatcher`—. El despachador es el delicado: agrega varios proveedores, así que tiene que saber decir «traigo estos días pero el rango no está contestado entero».
- **Sin migración y sin cambio en Domain.** `PriceHistoryReach` se queda como está.
- **Sin cambio en la interfaz de usuario.** Lo que el usuario nota es que un hueco deja de ser permanente, no una pantalla nueva.
- **Fuera de alcance**: la política de reintentos de Polly, añadir proveedores, y limitar el ritmo de la primera descarga masiva. Esto último merece anotarse como mejora: el fallo apareció justamente por pedir cincuenta mil días seguidos, y con este cambio dejará de perder datos pero seguirá provocando el 401.
