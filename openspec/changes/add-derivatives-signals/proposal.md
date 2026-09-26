## Why

`add-derivatives-ingestion` recoge y guarda el dato de derivados, pero no hace nada con él. Este cambio lo convierte en señales, las explica y las enseña.

Va aparte y después a propósito. El dato de financiación e interés abierto se puede descargar hacia atrás, así que en cuanto haya reglas se podrán contrastar desde 2020; las liquidaciones sólo se acumulan hacia delante, y una regla sobre ellas no se podrá juzgar hasta tener meses grabados. **Hasta entonces no se sabe si nada de esto sirve**, y construir la explicación bonita de unas señales antes de saber si esas señales valen algo es hacer el trabajo en el orden equivocado.

Cuando se aplique, se aplicará sabiendo cosas que hoy no se saben: cuánto ocupa el dato, qué huecos tiene de verdad y si las condiciones que se quieren detectar aparecen alguna vez.

## What Changes

- **Las reglas de un sistema pueden hablar del dato de derivados**: financiación extrema, divergencia entre precio e interés abierto, acumulación de liquidaciones cerca del precio. Se evalúan con el mismo motor determinista que ya existe.
- **Una señal dice sobre qué dato se apoya**, de qué fuente y a qué instante, y si ese dato era observado o estimado. No se emite señal en un instante sin dato.
- **El backtest declara lo que ha podido contrastar.** Una regla sobre liquidaciones evaluada sobre un periodo anterior a la primera grabación saldría plana y con aspecto de válida; el resultado tiene que negarse a darlo por bueno.
- **Kapea puede proponer activos que el usuario no sigue.** El dato es del mercado entero, así que sabe de algo con la financiación disparada aunque no esté en la lista. Se ofrece como sugerencia, nunca se añade solo.
- **Una capa de IA que explica lo ya calculado**: recibe las señales, sus cifras y la posición del usuario, y redacta qué está pasando y qué riesgo tiene. **No predice precios ni calcula nada**, igual que la IA que propone el mapeo de un formato o traduce un sistema descrito en palabras.
- **Una pantalla** con el estado de los derivados de los activos seguidos y sus señales.
- **Hyperliquid como segunda fuente**, que publica posiciones reales en cadena frente a las estimadas de Binance. Guardadas por separado: que discrepen es información.
- **Fuera de alcance**: ejecutar órdenes. CoinGlass queda anotado como mejora futura de pago.

## Capabilities

### New Capabilities

- `derivatives-signals`: qué puede afirmar una señal derivada del dato de derivados, cómo se contrasta y cómo se distingue de una señal sobre precio.
- `signal-narrative`: qué recibe la capa de IA, qué no puede afirmar y cómo se distingue en pantalla lo calculado de lo redactado.

### Modified Capabilities

- `strategy-rules`: una regla puede referirse a magnitudes de derivados, no sólo a la serie de precios.
- `strategy-backtest`: un backtest debe declarar el periodo realmente cubierto por cada magnitud que usa, y negarse a dar por contrastado lo que no lo está.

## Impact

- **Depende de `add-derivatives-ingestion`**, que aporta el dato, la correspondencia con el contrato y el alcance de cada magnitud. Sin eso, nada de esto se puede evaluar ni contrastar.
- **Terceros**: Hyperliquid, API pública y sin cuenta. El modelo de lenguaje de la capa de IA, que ya se usa en el proyecto.
- **Coste**: sólo el del modelo.
- **Riesgo**: la mayoría de estas métricas tienen menos poder predictivo del que aparenta, así que el resultado esperado razonable es descartar reglas, no acumularlas. Por eso el backtest y la honestidad sobre lo que no se ha podido contrastar son parte del cambio y no un añadido.
