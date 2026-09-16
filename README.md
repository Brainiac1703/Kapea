# Kapea

Kapea es una aplicación personal de gestión de inversiones. Reúne en un solo sitio
todo lo que tienes repartido entre brókeres y exchanges, calcula qué has ganado o
perdido como lo calcula Hacienda, y te ayuda a decidir con reglas escritas en lugar de
con corazonadas.

## Qué hace

**Consolida tu cartera**

- Descarga el histórico de las plataformas con API mediante credenciales de sólo
  lectura. Kraken y Bit2Me vienen configuradas y se sincronizan solas cada pocas horas.
- Importa cualquier plataforma que exporte una tabla de movimientos. El formato se
  describe desde la aplicación con un perfil de importación, sin programar. XTB viene
  configurado de serie.
- Si un fichero no lo reconoce ningún perfil, puede proponer el mapeo con Azure OpenAI.
  Al servicio sólo van las cabeceras y tres filas de ejemplo; los importes los calcula
  siempre Kapea.
- Descarta duplicados al repetir una importación, detecta traspasos entre tus propias
  cuentas y aparta lo que no sabe clasificar para que lo revises.
- Permite apuntar movimientos a mano, y corregir o anular un importado sin perder de
  dónde vino. Cada movimiento dice si llegó por API, por fichero, a mano o como ajuste.

**Te dice cómo vas**

- Posiciones abiertas con coste medio, precio de mercado, valor, resultado latente y
  realizado, comisiones y peso.
- Patrimonio con el efectivo de cada cuenta, convertido a euros con el tipo del Banco
  Central Europeo.
- Evolución diaria del patrimonio frente a lo aportado, reparto por clase de activo y
  gráfica de cada activo con medias y fuerza relativa.
- Rendimiento medido como el de un fondo: rentabilidad de tus decisiones, de tu dinero,
  volatilidad y mayor caída, comparado con haber puesto lo mismo en tu mayor posición.
- Avisos de concentración cuando pocas posiciones pesan demasiado.

**Prepara la información fiscal**

- Ganancias y pérdidas patrimoniales por ejercicio con criterio FIFO en euros, con el
  tipo de cambio de la fecha de cada operación.
- Desglose por activo y hasta los lotes de compra que consume cada venta.
- Rendimientos cobrados, con su retención.

**Te ayuda a especular con método**

- Sistemas de especulación con reglas sobre precio, medias, RSI, MACD, bandas de
  Bollinger y recorrido diario, con objetivo y nivel de salida. También se pueden
  describir con palabras y dejar que Azure OpenAI proponga las reglas.
- Señales de entrada y salida con la condición que las disparó.
- Simulación de un sistema sobre el histórico real de un activo, con tus comisiones y
  los impuestos del ahorro, comparada con comprar y no tocar.
- Seguimiento de ideas de fuentes externas hasta su desenlace, con el balance de
  aciertos de cada fuente.
- Diario de decisiones para anotar por qué hiciste lo que hiciste.

Kapea no ejecuta órdenes ni recomienda comprar o vender: aplica lo que tú escribes y te
enseña lo que ha pasado.

## Documentación

- [Guía de uso](docs/uso.md): cómo se trabaja con cada pantalla y cómo leer sus cifras.
- [Configuración](docs/configuracion.md): variables, identidad, secretos, servicios
  externos y comprobaciones de arranque.
- [Despliegue en Azure](Deploy/README.md): infraestructura, flujo de publicación y
  costes.

## Empezar en local

Hace falta Docker. No hace falta ni SQL Server ni una cuenta de Azure.

```bash
cp .env.example .env      # ajusta al menos MSSQL_SA_PASSWORD
docker compose up --build
```

- Aplicación en <http://localhost:8082>
- SQL Server en `localhost:14331`, base de datos `Kapea`

Sin Google configurado, la pantalla de acceso ofrece entrar como usuario de desarrollo.
El primer paso dentro es dar de alta una cuenta; la [guía de uso](docs/uso.md) sigue
desde ahí.

## Cómo está hecho

| Pieza | Qué hace |
|---|---|
| `api` | Web API en ASP.NET Core que además sirve el cliente Blazor WebAssembly. Es la única que ve las credenciales de las plataformas. |
| `sync` | Proceso aparte que sincroniza las cuentas con API y completa el histórico de precios. Un fallo hablando con un tercero no arrastra a la API. |
| `sqlserver` | Base de datos. En desarrollo la API aplica las migraciones al arrancar. |

.NET 10, Blazor WebAssembly con Fluent UI, EF Core sobre SQL Server y arquitectura por
capas: dominio sin dependencias, casos de uso con sus puertos, e infraestructura y API
en los bordes. Las decisiones y especificaciones de cada fase están en `openspec/`.

```bash
dotnet test                                   # los de integración necesitan Docker
dotnet run --project src/Kapea.Api            # necesita ConnectionStrings__Kapea
```

Los tests de integración levantan su propio SQL Server con Testcontainers, así que
Docker tiene que estar en marcha aunque el resto se ejecute en local.
