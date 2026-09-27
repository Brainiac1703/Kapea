# Verificación en producción

Lo comprobado contra el despliegue real, para que la tarea marcada no dependa de
la memoria de nadie. Instancia: `https://ca-kapea-api.nicemoss-217be14b.francecentral.azurecontainerapps.io`.

## 6.2 · Credenciales y sincronización · 2026-09-27

**El secreto vive en el almacén gestionado.** `kv-kapea` guarda una entrada por
credencial, con el nombre que la entidad conserva:

```
broker-bit2me-73f09e02…-4730a899…   12:41:39
broker-kraken-73f09e02…-76b82022…   12:46:16
```

**No vive en la base de datos.** El `SELECT` que ejecuta producción sobre
`BrokerCredentials` devuelve `Id, AccountId, Alias, CreatedAt, InvalidReason,
LastSynchronizedAt, Platform, RotatedAt, Scopes, SecretName, Status, UserId`.
Sólo el nombre. No hay columna donde quepa la clave.

**La sincronización trajo los movimientos**, sin un fallo:

| Cuenta | Resultado |
|---|---|
| Bit2Me | 2.232 movimientos nuevos, 1.974 duplicados descartados |
| Kraken | 103 movimientos nuevos de 120 apuntes, 0 rechazados, 14 sin efecto |

La segunda pasada de Bit2Me, cuatro minutos después, trajo 0 nuevos y 0
duplicados: la descarga incremental arranca desde el último instante importado y
no vuelve a pedir lo que ya tiene.

**Los permisos son los mínimos**, y por eso la comprobación del contenedor de
claves no se puede hacer con la cuenta de una persona. La identidad `id-kapea`
tiene:

| Rol | Ámbito |
|---|---|
| Storage Blob Data Contributor | sólo el contenedor `data-protection` |
| Key Vault Crypto User | sólo la clave `data-protection` |
| Key Vault Secrets Officer | `kv-kapea` |
| Cognitive Services OpenAI User | `oai-kapea` |
| AcrPull | `acrkapea` |

## 6.3 · La sesión sobrevive a una publicación · 2026-09-27

Con la sesión abierta se fusionó el arreglo del registro y se aprobó el
despliegue. Se publicaron `ca-kapea-api--0000010` (15:11:51) y
`ca-kapea-sync--0000009` (15:12:07), las dos en marcha y con todo el tráfico. El
usuario siguió navegando sin volver a identificarse.

Es lo que tenía que pasar: las claves que cifran la cookie viven en el
contenedor `data-protection` y no en el sistema de ficheros de la revisión, así
que una revisión nueva las encuentra donde las dejó la anterior.

## Lo que el despliegue trajo consigo

La sincronización posterior dejó el histórico de producción al día: **50.708 días
nuevos de 23 activos**, 36.232 de ellos de historia antigua, 0 activos con
relleno pendiente y 0 sin cobertura. Las dos cuentas se importaron sin fallos y
sin duplicados nuevos.

El ruido del registro desapareció: en el tramo posterior a la revisión, 323
líneas útiles y **1 sola** de `HttpClient` o `DbCommand`, frente a las 72.556 de
las seis horas anteriores.

## Pendiente, y no de este cambio

Al final del relleno **Yahoo empezó a devolver 401**: tres veces la cotización
del día —«las acciones se mostrarán sin valor de mercado»— y una el histórico de
USDG-USD. No es un bloqueo de Azure: desde el portátil, a la misma hora, Yahoo
devolvía 429. Es límite de peticiones después de descargar cincuenta mil días de
golpe, y la degradación fue la que la especificación pide: la pantalla siguió en
pie.

Lo que sí merece mirarse aparte es una consecuencia que el despliegue ha dejado
a la vista. `YahooPriceHistoryProvider` traga el fallo, devuelve una serie vacía
y anota «se reintentará»; pero `PriceHistoryUpdater` registra el alcance del
tramo **aunque no haya venido nada**, que es justo lo que impide volver a
pedirlo. Hacia delante da igual, porque el último día conocido sale de lo
guardado. Hacia atrás no: el tramo de relleno que falló por un 401 queda marcado
como pedido y no se repite nunca. Un fallo del proveedor y un tramo
genuinamente vacío son hoy indistinguibles, y el comentario del proveedor
promete un reintento que el alcance no deja ocurrir.

## Observado de paso

La API escribió 70.854 líneas en ocho horas, casi todas `Start processing HTTP
request` de los monederos de Earn —que se piden uno a uno— y `Executed DbCommand`
de EF Core. No rompía nada, pero a ese ritmo Log Analytics cuesta dinero y
encontrar un error de verdad ahí dentro es imposible.

Arreglado bajando a `Warning` las dos categorías culpables,
`System.Net.Http.HttpClient` y `Microsoft.EntityFrameworkCore.Database.Command`,
en el `appsettings.json` de la API y del trabajador. Se devuelven a `Information`
en el de desarrollo: en local esas dos son justo lo que se mira cuando algo no
importa bien, y en producción son ruido que tapa los mensajes propios.

Lo que cuenta la historia de una sincronización vive bajo `Kapea.*` y sigue en
`Information`, así que la comprobación de arriba se podrá repetir igual.
