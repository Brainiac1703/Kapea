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

Sin `AUTH_AUTHORITY` configurado, la API arranca con autenticación de desarrollo y
atribuye toda petición a un usuario fijo. Es deliberado que esto **solo** valga en
`Development`: fuera de él la API se niega a arrancar sin identidad configurada, en
lugar de abrirse a cualquiera.

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
