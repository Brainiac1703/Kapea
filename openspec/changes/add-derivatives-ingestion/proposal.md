## Why

Kapea sólo mira el precio. El dinero apalancado —dónde está apostado, a qué coste y qué se liquida— lo publican gratis las plataformas de derivados, y es la entrada que a los sistemas de especulación les falta.

Explotar ese dato es un trabajo largo y todavía no sabemos si merece la pena: las reglas sobre estas magnitudes tienen menos poder predictivo del que aparentan, y la única forma de saberlo es contrastarlas. Pero **recogerlo no puede esperar a esa decisión**, y por una razón asimétrica que conviene dejar escrita:

| Magnitud | Histórico | Consecuencia |
|---|---|---|
| Tipo de financiación | Desde enero de 2020 | Se descarga cuando haga falta |
| Interés abierto | Desde septiembre de 2020 | Se descarga cuando haga falta |
| **Liquidaciones** | **No existe** | **Lo que no se grabe hoy no se tendrá nunca** |

Binance retiró el endpoint que servía las liquidaciones históricas y no las incluye en su archivo público. Sólo quedan en tiempo real. Cada semana sin grabarlas es una semana que jamás podrá contrastarse.

Este cambio recoge y guarda. No calcula señales, no las explica y no las enseña: eso queda para `add-derivatives-signals`, que se decidirá cuando haya con qué contrastar.

## What Changes

- **Una capacidad nueva para el dato de derivados**: tipo de financiación, interés abierto y liquidaciones, por contrato y fuente, con su procedencia y su instante. No cabe en `price-history`, que es una serie diaria por activo poseído: esto es intradía, va por contrato perpetuo y describe el mercado, no la cartera.
- **Cada activo aprende cuál es su contrato perpetuo** en cada fuente, con el factor por el que esa fuente multiplica la cantidad. Un activo seguido puede no tener perpetuo, y eso no es un error.
- **Grabación continua de las liquidaciones** desde el primer día, con reconexión y con el intervalo perdido anotado como hueco en lugar de disimulado.
- **Relleno del histórico de financiación e interés abierto** desde los ficheros que publica Binance, en segundo plano al asociar un contrato.
- **Cada magnitud declara desde cuándo hay dato, con qué resolución y qué huecos tiene.** Sin eso, lo recogido no sirve para contrastar nada: un backtest sobre un periodo sin dato sale plano y parece válido.
- **Fuera de alcance, y a propósito**: reglas, señales, backtest, propuestas de activos, explicación redactada y pantalla. También Hyperliquid, que aporta contraste pero no urgencia, y CoinGlass, que es de pago.

## Capabilities

### New Capabilities

- `derivatives-data`: el tipo de financiación, el interés abierto y las liquidaciones de los contratos perpetuos, con su procedencia, su granularidad y desde cuándo hay dato de cada cosa.

### Modified Capabilities

- `watchlist`: al añadir un activo, el sistema dice si cotiza como contrato perpetuo.
- `market-prices`: una fuente puede decir si un activo cotiza como perpetuo, con qué identificador y con qué factor.

## Impact

- **Datos**: series temporales muy por encima de lo que Kapea guarda hoy. El interés abierto de veinte activos en tramos finos durante cinco años son millones de filas, así que la resolución que se conserva es una decisión del diseño y no un detalle.
- **Procesos**: el relleno y el refresco van en el worker `sync`, que ya corre aparte de la API. Las liquidaciones necesitan además **una conexión permanente por WebSocket, que es una forma de trabajo que Kapea no tiene todavía**: es la pieza nueva de este cambio y la que más puede fallar.
- **Terceros**: Binance —API pública, ficheros históricos y WebSocket—, sin cuenta y sin coste.
- **Riesgo**: se recoge un dato cuya utilidad aún no está demostrada. Es una apuesta consciente y barata: si al contrastarlo resulta ser ruido, se habrá gastado almacenamiento y unas semanas de trabajo, no meses. Si se hubiera esperado, las liquidaciones no se podrían contrastar nunca.
