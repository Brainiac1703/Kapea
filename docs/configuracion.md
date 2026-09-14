# Configuración

Todo lo que se puede ajustar en Kapea, qué pasa si no se configura y qué comprueba al
arrancar.

- [Cómo se configura](#cómo-se-configura)
- [Referencia rápida](#referencia-rápida)
- [Base de datos](#base-de-datos)
- [Identidad](#identidad)
- [Secretos de las plataformas](#secretos-de-las-plataformas)
- [Sesión](#sesión)
- [Sincronización](#sincronización)
- [Servicio de modelos](#servicio-de-modelos)
- [Precios y tipos de cambio](#precios-y-tipos-de-cambio)
- [Comisiones por plataforma](#comisiones-por-plataforma)
- [Telemetría](#telemetría)
- [Qué comprueba al arrancar](#qué-comprueba-al-arrancar)
- [Problemas frecuentes](#problemas-frecuentes)

## Cómo se configura

Kapea usa la configuración estándar de .NET. Cada clave se puede dar en
`appsettings.json`, en User Secrets o como variable de entorno. En una variable de
entorno los dos puntos se escriben con dos guiones bajos:

```
Authentication:Google:ClientId   →   Authentication__Google__ClientId
```

**En local con Docker** no hace falta tocar nada de eso. El fichero `.env` de la raíz
recoge unas pocas variables cortas y `docker-compose.yml` las traduce a las claves de
.NET. Se parte de la plantilla:

```bash
cp .env.example .env
```

**En Azure** las claves las pone Terraform como variables de entorno de cada
contenedor, y los secretos se leen de Key Vault. Ver [Despliegue en
Azure](../Deploy/README.md).

**El entorno** lo decide `ASPNETCORE_ENVIRONMENT` en la API y `DOTNET_ENVIRONMENT` en
el proceso de sincronización. En docker compose es `Development`; en Azure,
`Production`. Varias cosas cambian de comportamiento según el entorno, y están
señaladas abajo.

## Referencia rápida

### Variables del `.env`

| Variable | Obligatoria | Qué hace |
|---|---|---|
| `MSSQL_SA_PASSWORD` | Sí | Contraseña del SQL Server del contenedor. Al menos 8 caracteres con mayúsculas, minúsculas, dígitos y símbolos, o el contenedor no arranca |
| `GOOGLE_CLIENT_ID` | No | Cliente OAuth de Google. Sin él se entra como usuario de desarrollo |
| `GOOGLE_CLIENT_SECRET` | No | Secreto del cliente OAuth de Google |
| `DEV_USER_ENABLED` | No | `true` conserva el usuario de desarrollo junto a Google |
| `LEGACY_OWNER_ID` | No | Dueño de los datos importados antes de que Kapea tuviera usuarios. No hace falta tocarlo |
| `KEYVAULT_URI` | No | Key Vault para las credenciales. Sin él, fichero local |
| `SYNC_INTERVAL` | No | Cada cuánto sincroniza, en formato `hh:mm:ss`. Por omisión en compose, `00:15:00` |
| `AZURE_OPENAI_ENDPOINT` | No | Punto de acceso del servicio de modelos |
| `AZURE_OPENAI_DEPLOYMENT` | No | Nombre del despliegue del modelo |
| `AZURE_OPENAI_KEY` | No | Clave del servicio de modelos |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | No | Telemetría en Application Insights |
| `API_PORT` | No | Puerto de la aplicación en tu máquina. Por omisión, `8082` |
| `SQLSERVER_PORT` | No | Puerto de SQL Server en tu máquina. Por omisión, `14331` |

### Claves de configuración

| Clave | Por omisión | Qué hace |
|---|---|---|
| `ConnectionStrings:Kapea` | — | Cadena de conexión a SQL Server |
| `Authentication:Google:ClientId` | — | Cliente OAuth de Google |
| `Authentication:Google:ClientSecret` | — | Secreto de ese cliente |
| `Authentication:DevelopmentUser:Enabled` | `false` | Conserva el acceso de desarrollo junto a Google. Sólo en `Development` |
| `Authentication:DevelopmentUser:DisplayName` | `Usuario de desarrollo` | Nombre del usuario de desarrollo |
| `Authentication:DevelopmentUser:LegacyOwnerId` | `00000000-…-000000000001` | Dueño de los datos anteriores a los usuarios |
| `KeyVault:Uri` | — | Dirección del Key Vault de las credenciales |
| `UserSecrets:Id` | `kapea-local` | Identificador del almacén de desarrollo |
| `DataProtection:BlobUri` | — | Blob donde guardar las claves de sesión |
| `DataProtection:KeyUri` | — | Clave de Key Vault que cifra esas claves |
| `DataProtection:KeysPath` | — | Directorio donde guardar las claves de sesión |
| `Synchronization:Interval` | `06:00:00` | Cada cuánto sincroniza el proceso `sync` |
| `MappingProposals:AzureOpenAi:Endpoint` | — | Punto de acceso del servicio de modelos |
| `MappingProposals:AzureOpenAi:Deployment` | — | Despliegue del modelo |
| `MappingProposals:AzureOpenAi:ApiKey` | — | Clave. Sin ella se usa la identidad del proceso |
| `MappingProposals:AzureOpenAi:ApiVersion` | `2024-10-21` | Versión de la API del servicio |
| `MappingProposals:AzureOpenAi:ConfidenceThreshold` | `0.85` | Seguridad a partir de la cual un mapeo no se pregunta |
| `MappingProposals:AzureOpenAi:TimeoutSeconds` | `20` | Cuánto se espera al servicio antes de renunciar |
| `ApplicationInsights:ConnectionString` | — | Telemetría |
| `Kraken:BaseAddress` | `https://api.kraken.com/` | API de Kraken |
| `Bit2Me:BaseAddress` | `https://gateway.bit2me.com/` | API de Bit2Me |
| `CoinGecko:BaseAddress` | `https://api.coingecko.com/` | Precios de criptomonedas |
| `Yahoo:BaseAddress` | `https://query1.finance.yahoo.com/` | Precios de acciones e histórico largo |
| `Ecb:BaseAddress` | `https://www.ecb.europa.eu/` | Tipos de cambio |

Las direcciones base sólo se cambian para apuntar a un servidor de pruebas.

## Base de datos

Kapea usa SQL Server. En docker compose va en su propio contenedor, con los datos en
el volumen `mssql-data`, y se publica en `localhost:14331`.

**Migraciones.** En `Development` la API aplica las pendientes al arrancar, así que la
base se crea sola. Fuera de desarrollo no lo hace a propósito: en Azure las aplica el
flujo de despliegue antes de publicar, donde un fallo se ve y detiene la publicación.

Para aplicarlas a mano contra otra base:

```bash
dotnet ef database update \
  --project src/Kapea.Infrastructure \
  --startup-project src/Kapea.Infrastructure \
  --connection "<cadena de conexión>"
```

**Datos de serie.** En cada arranque se dan de alta, si faltan, las tres plataformas
de serie y los tres perfiles de importación de XTB. Es idempotente.

## Identidad

Kapea no guarda contraseñas: se entra con un proveedor externo y la API emite una
cookie de sesión. El navegador nunca ve un token del proveedor.

### Google

1. En Google Cloud, crea un **ID de cliente de OAuth** de tipo aplicación web. Es
   gratuito y no exige cuenta de facturación.
2. Añade como **URI de redirección autorizado** la dirección de Kapea seguida de
   `/signin-google`. En local:

   ```
   http://localhost:8082/signin-google
   ```

3. Pon el identificador y el secreto en `GOOGLE_CLIENT_ID` y `GOOGLE_CLIENT_SECRET`.

Si la pantalla de consentimiento está en modo de prueba, sólo pueden entrar las cuentas
añadidas como usuarios de prueba.

En Azure, identificador y secreto van en Key Vault con los nombres
`Authentication--Google--ClientId` y `Authentication--Google--ClientSecret`. Los dos
guiones son la jerarquía: un nombre de secreto no admite dos puntos.

### Usuario de desarrollo

Sin Google configurado, la pantalla de acceso ofrece entrar como un usuario fijo de
desarrollo. No es un modo sin sesión: emite la misma cookie que Google, así que cerrar
sesión, caducar y volver a entrar recorren el mismo camino que en producción.

Con Google configurado desaparece. Para tener los dos botones mientras trabajas, pon
`DEV_USER_ENABLED=true`.

Todo esto **sólo** vale en `Development`. Fuera de él la API se niega a arrancar sin
Google configurado y ignora `DEV_USER_ENABLED`, de modo que copiar la bandera por
descuido a un despliegue no abre nada.

### Datos anteriores a los usuarios

Lo importado antes de que Kapea tuviera usuarios quedó a nombre de
`LEGACY_OWNER_ID`. El primer usuario que entra adopta esos datos. En una instalación
nueva no hay nada que adoptar.

### Apple

Está preparado y sin implementar. El modelo ya admite varios proveedores por usuario,
así que activarlo no toca el esquema ni los datos. Hace falta:

1. Una cuenta de Apple Developer, de pago y de renovación anual. Es el motivo por el
   que no está hecho.
2. En el portal de Apple: un identificador de aplicación con «Sign in with Apple», un
   identificador de servicio para el acceso web y una clave privada.
3. En Kapea: el paquete de autenticación de Apple para ASP.NET Core, una entrada
   `Apple` junto a la de Google en `KapeaAuthentication` y sus credenciales con la
   misma forma que las de Google.

El secreto de cliente de Apple es un JWT firmado con esa clave privada que caduca como
mucho a los seis meses. Conviene generarlo al arrancar y no dejarlo escrito.

La pantalla de acceso no cambia: pinta un botón por proveedor disponible.

## Secretos de las plataformas

Las claves de API de Kraken y Bit2Me se guardan fuera de la base de datos. La base
sólo conserva el nombre bajo el que vive cada una.

| Almacén | Cuándo | Cómo |
|---|---|---|
| Azure Key Vault | Con `KeyVault:Uri` configurado | Con la identidad de la aplicación, sin claves en la configuración |
| Fichero de User Secrets | Sin `KeyVault:Uri`, sólo en `Development` | En el volumen `broker-secrets`, compartido por la API y el proceso de sincronización |

El almacén de desarrollo **guarda en claro**, como todo User Secrets. No pongas ahí
claves de una cuenta con dinero real, aunque sean de sólo lectura.

Fuera de `Development`, sin `KeyVault:Uri` la API y el proceso de sincronización se
niegan a arrancar. Es mejor que no arranquen a que guarden credenciales en claro dentro
de un contenedor.

Para usar Key Vault desde tu máquina, pon su dirección en `KEYVAULT_URI` y ten una
sesión de `az login` con permiso de *Key Vault Secrets Officer* sobre él.

## Sesión

Las claves que cifran la cookie de sesión tienen que sobrevivir a un reinicio, o cada
reinicio cierra la sesión de quien esté dentro.

| Configuración | Dónde viven | Uso |
|---|---|---|
| `DataProtection:BlobUri` | En un blob de Azure Storage, cifradas con `DataProtection:KeyUri` si se da | Azure |
| `DataProtection:KeysPath` | En un directorio | Docker compose, en el volumen `data-protection` |
| Ninguna | En memoria | Un reinicio cierra las sesiones, y la API lo avisa en el registro |

Si hay blob y directorio a la vez, gana el blob.

La sesión dura 30 días y se renueva sola mientras se usa. Fuera de `Development` la
cookie sólo viaja por HTTPS.

## Sincronización

El proceso `sync` corre al arrancar y después cada `Synchronization:Interval`. En cada
vuelta:

1. Sincroniza las cuentas cuya credencial está activa. Una revocada o rechazada por la
   plataforma queda fuera hasta que se rote.
2. Completa el histórico diario de precios de tus activos y los tipos de cambio.

Por omisión son seis horas: suficiente para tener la cartera al día sin gastar los
límites de frecuencia de las plataformas. En docker compose son quince minutos para no
esperar al probar.

El proceso debe correr con **una sola instancia**. Dos a la vez sincronizarían la misma
cuenta.

El botón *Sincronizar ahora* no espera al proceso: lo ejecuta la API en el momento, y
sólo sobre las cuentas de quien lo pide.

## Servicio de modelos

Azure OpenAI es opcional. Sin configurar, todo funciona y estas tres cosas se hacen a
mano:

| Uso | Qué se envía | Qué vuelve |
|---|---|---|
| Proponer el mapeo de un fichero desconocido | Las cabeceras y como mucho tres filas | Qué columna es cuál |
| Traducir un método descrito con palabras | El texto que pegas | Reglas del sistema, y lo que no supo traducir |
| Sacar ideas de una publicación | El texto que pegas | Activo, sentido, entrada, objetivo y nivel de salida de cada idea |

Nunca vuelve un importe que Kapea use como cifra. Los resultados los calcula siempre el
motor determinista, así que recalcular un ejercicio ya presentado da lo mismo.

Hay un test que inspecciona la petición del mapeo y falla si se cuela una cuarta fila:
un extracto es un dato personal, y para saber qué columna es la fecha no hace falta ver
el año entero.

### Configurarlo

El servicio se crea con Terraform, en la primera etapa del despliegue, sin necesidad de
desplegar la aplicación. Después:

```bash
cd Deploy/infra
terraform output openai_endpoint
terraform output openai_deployment
terraform output -raw openai_key
```

y en el `.env`:

```dotenv
AZURE_OPENAI_ENDPOINT=https://oai-kapea.openai.azure.com/
AZURE_OPENAI_DEPLOYMENT=gpt-4.1-mini
AZURE_OPENAI_KEY=<la clave>
```

Reinicia la API con `docker compose up -d` para que lo recoja.

**Clave o identidad.** Con `ApiKey` se autentica con la clave. Sin ella, pide un token
con la identidad del proceso, que es como llama la aplicación desplegada en Azure. En tu
máquina, sin identidad administrada, usa la clave.

**Umbral de confianza.** `ConfidenceThreshold` decide cuándo un mapeo propuesto se
aplica sin preguntar. Aunque el modelo diga estar seguro, si la columna que propone como
fecha no se lee como fecha en las filas de ejemplo, se pregunta igual.

## Precios y tipos de cambio

No necesitan configuración ni claves.

| Fuente | Qué da | Límites |
|---|---|---|
| CoinGecko | Precio actual de criptomonedas e histórico | Sin clave, sólo los últimos 365 días de histórico |
| Yahoo Finance | Precio actual de acciones e histórico largo de acciones y criptomonedas | Algunos tokens pequeños sólo cotizan en dólares; se convierten con el tipo del BCE |
| Banco Central Europeo | Tipos de cambio diarios | Sólo días laborables |

Los precios actuales se guardan un minuto en memoria para no repetir peticiones al
cambiar de pantalla. El histórico se guarda en la base y lo completa el proceso de
sincronización, hacia delante y hacia atrás hasta la primera compra de cada activo.

Un activo que ninguna fuente cotiza aparece *Sin precio*. Su valor no se suma al
patrimonio y la pantalla lo avisa.

## Comisiones por plataforma

La simulación de sistemas y el balance de ideas descuentan una comisión por operación.
Sale de cada plataforma: un porcentaje sobre el importe y un fijo por operación.

| Plataforma | Porcentaje | Fijo |
|---|---|---|
| Bit2Me | 0,95 % | 0 € |
| Kraken | 1,0006 % | 0 € |
| XTB | 0 % | 0 € |

Son los de la instalación original y todavía no tienen pantalla. Para cambiarlos,
directamente en la base de datos:

```sql
UPDATE Platforms SET FeeRate = 0.0095, FixedFee = 0 WHERE Code = 'Bit2Me';
```

`FeeRate` va en tanto por uno: `0.0095` es un 0,95 %. Estas comisiones no afectan a la
cartera ni a los resultados fiscales, que usan las comisiones reales de cada
movimiento.

## Telemetría

Con `ApplicationInsights:ConnectionString`, la API y el proceso de sincronización
envían trazas, peticiones y errores a Application Insights. Sin ella no se envía nada y
todo arranca igual.

Los registros de la aplicación están en castellano.

## Qué comprueba al arrancar

Kapea prefiere no arrancar a arrancar en un estado inseguro o que fallaría más tarde
sin decir por qué.

| Condición | Resultado | Por qué |
|---|---|---|
| Fuera de `Development` sin `Authentication:Google:ClientId` | No arranca | Arrancaría con el acceso de desarrollo, abierto a cualquiera |
| Fuera de `Development` sin `KeyVault:Uri` | No arranca | Guardaría credenciales en claro |
| Almacén de desarrollo sin permiso de escritura | No arranca | El alta de una credencial fallaría mucho después con una ruta denegada |
| Sin almacén para las claves de sesión | Arranca y avisa | Sólo cuesta cerrar las sesiones al reiniciar |
| Sin servicio de modelos | Arranca | Esas tres funciones se hacen a mano |
| Sin telemetría | Arranca | No es imprescindible |

## Problemas frecuentes

**La API no arranca: no se puede escribir en el almacén de credenciales.** El volumen
se creó con otro propietario. Bórralo y vuelve a levantar:

```bash
docker compose down
docker volume rm kapea_broker-secrets
docker compose up -d --build
```

Se pierden las credenciales guardadas en local y hay que darlas de alta otra vez.

**Cambios en el código que no aparecen.** `docker compose restart` reinicia con la
imagen vieja. Hay que reconstruir:

```bash
docker compose up -d --build
```

**Me echa de la sesión en cada reinicio.** No hay almacén para las claves de sesión.
En docker compose, comprueba que el volumen `data-protection` está montado.

**Google dice `redirect_uri_mismatch`.** La URI registrada en Google no coincide con la
dirección desde la que entras. Compárala letra a letra, sin barra final, y espera unos
minutos: Google tarda en aplicar el cambio.

**Posiciones sin precio.** Mira el registro de la API. Un 403 de CoinGecko suele ser
una red que bloquea peticiones sin navegador; un activo que no aparece en ninguna fuente
se queda sin precio hasta que alguna lo cotice.

**El puerto 1433 u 8082 ya está ocupado.** Cambia `SQLSERVER_PORT` o `API_PORT` en el
`.env`.
