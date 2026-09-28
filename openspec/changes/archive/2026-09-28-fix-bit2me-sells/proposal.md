## Why

Una venta en Bit2Me no entra y nadie se entera.

El usuario vendió QNT desde **Bit2Me > Vender** a las 11:05 del 28 de septiembre y la posición sigue abierta. La venta **sí llega** del proveedor: está en la base de datos, guardada como rechazada, con el motivo «Una compra o una venta necesita activo». Ni el registro ni la pantalla dijeron nada; la sincronización se declaró terminada sin fallo.

El movimiento que devuelve Bit2Me está completo y es correcto —0,95674537 QNT es exactamente la suma de las dos compras—, con el activo en el **origen** y los euros en el **destino**:

```
"subtype":"sell", "denomination":{"amount":"0.95674537","currency":"QNT"},
"origin":{"amount":"0.95674537","currency":"QNT","rate":{"value":"198.08"}},
"destination":{"amount":"184.63426553","currency":"EUR"}
```

`Bit2MeImportAdapter.FromWalletTransaction` elige el lado con `Destination ?? Origin ?? Denomination`: el destino manda siempre. En una compra es correcto —origen euros, destino cripto— y por eso las compras entran bien. En una venta es al revés, así que se queda con el lado de los euros: sin activo y con cantidad cero. El validador lo rechaza, con razón. El error no está en el validador sino en elegir el lado.

**No es un caso aislado de hoy.** En toda la base hay dos movimientos rechazados con ese motivo y los dos son ventas: el QNT de hoy y 99,0595 EURC del 6 de mayo de 2025. Lleva casi año y medio perdiendo ventas en silencio.

## What Changes

- **El activo de una venta se toma del origen**, que es donde Bit2Me lo pone. La compra sigue tomándolo del destino, como hasta ahora.
- **El ingreso de una venta es lo que llegó al destino**, 184,63 €, y no la cantidad por el cambio publicado, 189,51 €. La diferencia de 4,88 € es el diferencial que se queda la plataforma, y ya hay un cálculo que lo trata: `Spread` contempla la venta con `published - paid` desde que se escribió, sólo que nunca llegaba a dispararse por faltarle las entradas.
- **Un rechazo se ve.** La sincronización pasa a decir cuántos registros quedaron rechazados, en el registro y en el informe que llega a la pantalla. Hoy sólo cuenta los nuevos y los duplicados, y por eso esto ha tardado meses en salir.
- **Sin reproceso automático** de lo ya rechazado: el reproceso manual existe y es el camino.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `transaction-import/bit2me`: una venta a euros desde el monedero se normaliza con el activo del origen y el ingreso del destino, en lugar de quedarse sin activo.
- `transaction-import`: el recuento de rechazados sale de la ejecución y llega a quien lanzó la sincronización. Conservarlo en la ejecución ya se cumplía; lo que faltaba era que se viera.

## Impact

- **`Kapea.Infrastructure/Import/Bit2Me/Bit2MeImportAdapter.cs`**: la elección del lado y el origen de los euros. Es el corazón del cambio y son pocas líneas.
- **`Kapea.Application/Synchronization/SynchronizationService.cs`**: `AccountSynchronizationResult` gana el recuento de rechazados, y la línea de registro lo dice.
- **`Kapea.Shared`** y **`Kapea.Client/Pages/Credentials.razor`**: el informe lo transporta y la pantalla lo enseña, por la vía de problemas que ya existe.
- **Sin migración.** Los dos rechazos ya están guardados con su contenido original, así que se recuperan reprocesándolos.
- **Las cifras validadas quedan desfasadas.** Con la venta de mayo de 2025 dentro, el resultado realizado de 85,26 € cambia, y el contraste que se hizo contra la declaración habrá que rehacerlo. No es efecto secundario: es la corrección de un dato que faltaba.
- **Fuera de alcance**: el adaptador de Kraken y el de ficheros de XTB, y reprocesar solo lo antiguo.
