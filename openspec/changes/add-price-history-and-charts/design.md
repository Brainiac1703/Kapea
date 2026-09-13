## Context

Kapea ya tiene un despachador de precios que reparte los símbolos por clase de activo entre CoinGecko y Yahoo, y que cachea un minuto el precio de ahora. Lo que no hay es serie: nada se guarda, así que no existe el ayer. Ver proposal.md para el porqué.

Dos hechos comprobados contra los proveedores el 12 de septiembre de 2026, y que condicionan todo lo demás:

- **CoinGecko gratuito solo sirve los últimos 365 días.** Tanto `market_chart/range` como `history` responden con el error 10012 para fechas anteriores. El histórico del usuario empieza en mayo de 2025, así que sus primeros meses quedan fuera.
- **Yahoo sí da histórico largo y en euros, también de cripto.** Devuelve cierres diarios de `BTC-EUR`, `ETH-EUR`, `XRP-EUR`, `SOL-EUR`, `AVAX-EUR`, `LINK-EUR` y `DOGE-EUR` para mayo de 2025. No cubre `PAXG-EUR`, `B2M-EUR` ni `PEPE-EUR`.

## Goals / Non-Goals

**Goals:**

- Que el histórico se traiga una vez y quede guardado, y que ponerlo al día cueste una petición por activo y no una por día.
- Que la serie de la cartera salga de los movimientos ya importados, sin datos nuevos que mantener en paralelo.
- Que un hueco se vea como un hueco.

**Non-Goals:**

- Intradía. La unidad es el día de cierre.
- Interpretación de las gráficas, sentimiento y señales de compra y venta. Van en fases siguientes y se apoyan en esta.
- Reconstruir el histórico de un activo que ningún proveedor gratuito cubra. Se dice y se deja vacío.

## Decisions

### Yahoo es la fuente del histórico; CoinGecko, el respaldo de los últimos 365 días

Yahoo cubre renta variable y la mayoría de las criptomonedas con años de profundidad, en euros y sin clave. CoinGecko conoce mejor los tokens pequeños pero su capa gratuita no llega más allá del año.

El orden es: Yahoo primero para cualquier activo; lo que no cubra, CoinGecko dentro de su ventana. Un activo que ninguno cubra queda sin serie, y su posición aparece sin valorar en los días afectados, igual que ya ocurre con el precio de ahora.

*Alternativas descartadas:* pagar CoinGecko, que resolvería el histórico completo pero introduce un coste recurrente en un proyecto personal; y pedir día a día con `coins/{id}/history`, que además de ser miles de peticiones tampoco pasa del año.

### Una fila por activo y día, con su origen

Tabla propia con clave primaria compuesta por activo y fecha. Guardar el origen permite saber de dónde salió cada cifra y volver a pedir solo lo que vino de un proveedor concreto si resulta estar mal.

El volumen es pequeño: doce activos y algo más de año y medio son unos cinco mil registros.

*Alternativa descartada:* guardar la serie como documento JSON por activo. Ahorra filas pero obliga a reescribir el documento entero cada día y complica preguntar por un rango de fechas.

### La serie de la cartera se calcula, no se guarda

Cuántas unidades había cada día sale de recorrer los movimientos en orden, que es lo mismo que ya hace el motor FIFO. Guardar además una foto diaria de la cartera duplicaría la fuente de verdad, y una importación con fecha anterior la dejaría desactualizada en silencio.

Se calcula en el dominio, como función pura de movimientos y precios, y se cachea en el servidor igual que el precio de ahora.

*Alternativa descartada:* una tabla de valor diario mantenida por el importador. Más rápida de leer, pero vuelve a introducir el problema que el recálculo desde cero resolvió: una cifra guardada que puede quedar vieja sin que nada lo note.

### Las gráficas se dibujan en SVG propio

Las tres son series de líneas o áreas apiladas. Una librería de gráficas traería interoperabilidad con JavaScript, una dependencia más que vigilar y su propio formato de números y fechas, justo lo que la capa de formato y localización ya resuelve.

Con SVG generado en el propio componente, el eje, el formato de los importes y los textos salen del mismo sitio que el resto de la aplicación, y el tema claro y oscuro se hereda.

*Alternativa descartada:* ApexCharts o Chart.js. Se reconsiderará si aparece una gráfica que el SVG a mano no sostenga, como velas con zoom.

### La puesta al día vive en el trabajador que ya existe

`Kapea.Sync` ya recorre las cuentas cada pocas horas. Añadir ahí el relleno de precios evita un proceso más, y la primera carga larga se hace sola en segundo plano en lugar de bloquear una pantalla.

Cada ejecución mira hasta dónde llega la serie de cada activo y pide solo lo que falta. Un fallo de un proveedor deja los días pendientes para la siguiente vuelta.

## Risks / Trade-offs

- **Los primeros meses de la cartera pueden quedar sin precio para PAXG, B2M y PEPE** → La gráfica arrancará con esos activos sin valorar y el día saldrá marcado como incompleto. Es visible y explicable; inventar el dato no lo sería.
- **Yahoo no tiene contrato público y puede cambiar o cortar** → Los precios quedan guardados, así que un corte no borra la historia: solo detiene la puesta al día, y CoinGecko sigue cubriendo el último año.
- **El símbolo de Yahoo para cripto no es el del catálogo** (`BTC` es `BTC-EUR`) → Se resuelve con la misma tabla de conversión de símbolos que ya existe, ampliada, y con un test que comprueba que la cartera del usuario queda cubierta.
- **Recorrer dos mil cuatrocientos movimientos por cada consulta de la serie** → A esta escala son milisegundos, y el resultado se cachea. Si creciera, la tabla diaria descartada arriba vuelve a estar sobre la mesa.

## Migration Plan

La tabla nace vacía y se rellena sola en la primera vuelta del trabajador. Nada de lo que ya funciona depende de ella: mientras esté vacía, las gráficas se ven vacías y el resto de la aplicación sigue igual. Revertir es dejar de pedir el histórico; los datos guardados no estorban.
