## Why

Las acciones no tienen precio de mercado. Ninguna: «Sin precio para 6 de 6 activos de Equity», igual en local que en producción. La especificación ya exige lo contrario —«obtiene el precio de unas y otras y las muestra en la misma página»—, así que esto es un incumplimiento.

Son **dos causas encadenadas**, y arreglar sólo la primera no enseñaría ni un precio:

1. **El endpoint devuelve 401.** `v7/finance/quote` exige desde hace tiempo cookie y *crumb* de sesión, y Kapea no gestiona ninguna de las dos. No es límite de peticiones ni algo pasajero: los nueve intentos registrados en producción son 401, sin un solo 200. El histórico, en cambio, funciona —bajó 50.708 días— porque usa `v8/finance/chart`, que no pide crumb.
2. **Lo que cotiza fuera del euro se descarta.** Aunque el 401 se arreglara, el código salta cualquier activo cuya divisa no sea EUR y lo deja sin precio. MREO cotiza en dólares, así que seguiría sin aparecer.

## What Changes

- **La cotización pasa a pedirse por `v8/finance/chart`**, el mismo endpoint que el histórico ya usa con éxito en este despliegue. Su cabecera `meta` trae `regularMarketPrice` y `regularMarketTime`, que es la cotización del momento y su instante: no se pierde el precio en vivo con el mercado abierto.
- **Lo que cotiza en otra divisa se convierte a euros** con el tipo del BCE, igual que ya hace el proveedor de histórico y con el mismo origen contrastable. Deja de descartarse.
- **Un valor que falle no impide el precio de los demás.** El endpoint es de uno en uno, así que un fallo deja de ser todo o nada como lo era con la llamada única.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `market-prices`: la cotización de renta variable se entrega en euros aunque el valor cotice en otra divisa, y el fallo de un valor no arrastra a los demás.

## Impact

- **`Kapea.Infrastructure/MarketData/YahooMarketPriceProvider.cs`**: cambia el endpoint, la lectura de la respuesta y añade la conversión. Necesita `IExchangeRateProvider`, que ya existe y ya usa el proveedor de histórico.
- **`DependencyInjection`**: una dependencia más en el registro del proveedor.
- **Más peticiones**: una por valor en vez de una para todos. Con seis posiciones son seis, amortiguadas por la caché de un minuto que ya existe. Es el precio de usar el único endpoint que responde.
- **Sin migración y sin cambios en la interfaz.**
- **Fuera de alcance**: negociar el crumb de `v7/finance/quote` y cambiar de proveedor de renta variable. Las dos quedan abiertas si esto resulta insuficiente.
