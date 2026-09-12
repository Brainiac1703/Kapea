## Why

Kapea ya sabe qué se tiene, qué costó y qué ha valido cada día, pero no sabe si se está gestionando bien ni cuándo convendría entrar o salir. El usuario quiere un sistema de especulación que proponga momentos de compra y objetivos de venta, tomando como referencia los sistemas de analistas conocidos.

Dos hechos, salidos de sus propios datos, deciden cómo se construye:

- **Operar le cuesta casi un 1,9 % por operación completa.** Bit2Me cobra un 0,95 % dentro del precio en cada dirección. Una regla que no gane eso pierde dinero aunque acierte la dirección. Cualquier sistema tiene que medirse con ese coste dentro.
- **No hay forma de saber si algo funciona.** Hoy se puede ver el valor y el resultado, pero no la rentabilidad comparable, ni el riesgo asumido, ni si habría sido mejor no hacer nada. Sin eso, adoptar un sistema es un acto de fe.

Por eso este change no empieza por las señales sino por poder juzgarlas.

## What Changes

- Se mide la cartera como se mide un fondo: **rentabilidad ponderada por tiempo** —que juzga las decisiones— y **ponderada por dinero** —que juzga el resultado—, volatilidad, caída máxima desde el pico y tiempo de recuperación.
- Se compara contra una **referencia**: no hacer nada con lo aportado, o un activo tomado como índice. Un sistema que no bate a no hacer nada sobra.
- Un **sistema de especulación es un dato**, no código: un conjunto de reglas con nombre, versionado, que el usuario crea, corrige y compara. Igual que los perfiles de importación.
- Un **motor determinista** evalúa esas reglas sobre la serie diaria y produce señales de compra, de venta, su objetivo y su nivel de salida. La misma entrada da siempre la misma salida y cada señal dice qué regla la disparó.
- Un **simulador** aplica un sistema al histórico sin mirar el futuro, cobrando las comisiones reales y los impuestos del ahorro, y dice qué habría pasado frente a la referencia.
- **La IA traduce y explica, no decide.** Convierte un sistema descrito en palabras en reglas explícitas que el usuario revisa antes de guardar, y redacta en castellano lo que las reglas están diciendo hoy. No produce cifras, no emite señales y no lee imágenes de gráficas.
- Se añaden los indicadores que faltan para expresar sistemas reales: **MACD**, **bandas de Bollinger**, **ATR** y la **media de 200 días** como filtro de tendencia.
- **Gestión del riesgo**: tamaño de la posición según lo que se mueve el activo, tope por posición, nivel de salida y objetivo, bandas de rebalanceo y aviso de concentración.
- **Diario de decisiones**: cada operación puede guardar por qué se hizo y contra qué señal, para poder releerlo meses después.

## Capabilities

### New Capabilities

- `portfolio-performance`: rentabilidad, riesgo y comparación con una referencia.
- `strategy-rules`: qué es un sistema de especulación, cómo se declara, se versiona y se valida.
- `strategy-signals`: cómo se evalúan las reglas sobre la serie y qué es una señal.
- `strategy-backtest`: cómo se simula un sistema sobre el histórico con costes e impuestos.
- `risk-management`: tamaño de posición, niveles de salida, concentración y rebalanceo.
- `decision-journal`: el registro de por qué se hizo cada operación.

### Modified Capabilities

- `technical-indicators`: se añaden MACD, bandas de Bollinger y ATR a los ya existentes.

## Impact

- **Domain**: las métricas, el evaluador de reglas, el simulador y el cálculo de tamaño de posición, todo como funciones puras sobre movimientos y precios.
- **Infrastructure**: persistencia de los sistemas y sus versiones, de las señales emitidas y del diario; y el traductor de sistemas descritos en palabras sobre el servicio de Azure OpenAI que ya existe.
- **Api** y **Client**: pantallas de rendimiento, de sistemas, de señales del día y del resultado de una simulación.
- **Fuera de alcance**: ejecutar órdenes automáticamente contra las plataformas, y el análisis de sentimiento y noticias. Van en changes aparte.
- **Sin consejo de inversión.** Kapea calcula y enseña lo que dicen las reglas que el usuario ha escrito; no recomienda comprar ni vender, y la pantalla lo dice.
