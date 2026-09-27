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

## 6.3 · Pendiente

Exige estar dentro cuando se publica. La pieza que lo sostiene ya está en su
sitio —contenedor de blobs, clave de cifrado y los dos permisos de arriba—, pero
eso no sustituye a navegar después del despliegue sin volver a entrar.

## Observado de paso

La API escribió 70.854 líneas en ocho horas, casi todas `Start processing HTTP
request` de los monederos de Earn, que se piden uno a uno. No rompe nada, pero a
ese ritmo Log Analytics cuesta dinero y encontrar un error de verdad ahí dentro
es imposible. Conviene subir el nivel mínimo de `System.Net.Http.HttpClient` en
producción.
