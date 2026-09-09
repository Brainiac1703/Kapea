# Kapea

Plataforma personal de gestión de inversiones: consolida las operaciones repartidas
entre las plataformas que uses, calcula el resultado con criterio FIFO en euros y
prepara la información fiscal.

Kapea no está atada a un conjunto cerrado de brókeres. Las plataformas que exponen una
API se integran con su adaptador —hoy Kraken y Bit2Me—, y cualquiera que exporte una
tabla de movimientos se da de alta desde la propia aplicación definiendo cómo se lee su
fichero. XTB viene configurado de serie por esa vía.

## Levantar el entorno de desarrollo

Hace falta Docker. No hace falta ni SQL Server ni una cuenta de Azure.

```bash
cp .env.example .env      # ajusta al menos MSSQL_SA_PASSWORD
docker compose up --build
```

- Aplicación en <http://localhost:8082>
- SQL Server en `localhost:14331`, base de datos `Kapea`

Levanta tres piezas:

| Servicio | Qué hace |
|---|---|
| `api` | Web API y, además, los estáticos del cliente Blazor WebAssembly. Es la única que ve las credenciales de los brokers. |
| `sync` | Sincronización periódica con Kraken y Bit2Me. Vive aparte para que un fallo hablando con un tercero no arrastre a la API. |
| `sqlserver` | Base de datos. Las migraciones las aplica la API al arrancar en desarrollo. |

Sin credenciales de broker configuradas el entorno funciona igual: se pueden crear
cuentas e importar ficheros de XTB, y la sincronización simplemente no encuentra nada
que sincronizar.

### Identidad en desarrollo

Kapea no guarda contraseñas: se entra con un proveedor externo. Sin
`GOOGLE_CLIENT_ID` configurado, la pantalla de acceso ofrece entrar como un usuario
fijo de desarrollo. No es un modo sin sesión: emite la misma cookie que emitiría
Google, así que cerrar sesión, caducar y volver a entrar recorren el mismo camino que
en producción. Es deliberado que esto **solo** valga en `Development`: fuera de él la
API se niega a arrancar sin identidad configurada, en lugar de abrirse a cualquiera.

Para activar Google, crea un ID de cliente de OAuth en Google Cloud con la URI de
redirección `http://localhost:8082/signin-google` y rellena `GOOGLE_CLIENT_ID` y
`GOOGLE_CLIENT_SECRET` en el `.env`.

#### Activar Apple

Apple queda preparado y sin implementar. El modelo ya admite varios proveedores por
usuario —una identidad es un par de proveedor y sujeto—, así que activarlo no toca el
esquema de la base de datos ni los datos existentes. Lo que hace falta:

1. Una cuenta de Apple Developer, que es de pago y de renovación anual. Es el único
   motivo por el que esto no está hecho ya.
2. En el portal de Apple: un identificador de aplicación con «Sign in with Apple»
   habilitado, un identificador de servicio para el acceso web, y una clave privada
   para generar el secreto de cliente.
3. En Kapea: el paquete de autenticación de Apple para ASP.NET Core, una entrada
   `Apple` junto a la de Google en `KapeaAuthentication`, y sus credenciales en la
   configuración con la misma forma que las de Google.

El secreto de cliente de Apple no es una cadena fija: es un JWT firmado con esa clave
privada y caduca como mucho a los seis meses, así que hay que renovarlo. Conviene
resolverlo al arrancar y no dejarlo escrito en el `.env`.

La pantalla de acceso no necesita ningún cambio: pinta un botón por cada proveedor que
la API declara disponible.

### Secretos de los brokers

Sin `KEYVAULT_URI` se usa el almacén de desarrollo, un fichero de User Secrets en un
volumen del contenedor. Guarda en claro, como todo User Secrets: no pongas ahí claves
de una cuenta con dinero real. En producción manda Key Vault.

## Desarrollo sin contenedores

```bash
dotnet test                                   # los de integración necesitan Docker
dotnet run --project src/Kapea.Api            # necesita ConnectionStrings__Kapea
```

Los tests de integración levantan su propio SQL Server con Testcontainers, así que
Docker tiene que estar en marcha aunque el resto se ejecute en local.
