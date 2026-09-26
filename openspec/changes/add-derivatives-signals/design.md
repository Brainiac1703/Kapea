## Context

Ver proposal.md para el porqué. Este cambio se apoya en `add-derivatives-ingestion`, que aporta el dato y, sobre todo, **el alcance de cada magnitud**: desde cuándo hay, con qué resolución y con qué huecos. Ese alcance es lo que aquí se convierte en la diferencia entre un resultado contrastado y uno que sólo lo parece.

Lo demás que condiciona el diseño ya existe en Kapea:

- El motor de señales evalúa reglas sobre indicadores calculados a partir de la serie diaria, de forma determinista y sin mirar el futuro.
- Una regla se expresa como condición sobre indicadores, precio o posición, y un sistema con una regla que el motor no sepa evaluar no se puede guardar.
- El backtest simula con costes e impuestos y se compara siempre con no hacer nada.
- Hay tres usos de modelo de lenguaje —proponer el mapeo de un formato, traducir un sistema descrito en palabras, extraer ideas de una publicación— y los tres siguen la misma forma: el modelo propone, el sistema valida y una persona aprueba.
- `external-ideas` ya define que una idea es un hecho con fecha y origen que el usuario acepta o descarta, y que puede hablar de algo que no se tiene.

## Goals / Non-Goals

**Goals:**

- Que una regla sobre derivados se evalúe con las mismas garantías que una sobre precio.
- Que un resultado de backtest no pueda confundirse con uno contrastado cuando no lo está.
- Que el usuario nunca confunda una cifra calculada con un texto redactado, ni un dato observado con uno estimado.

**Non-Goals:**

- Volver a tocar cómo se recoge o se guarda el dato. Eso es del cambio anterior.
- Ejecutar órdenes.

## Decisions

### El backtest declara lo que ha podido contrastar

Hoy una simulación devuelve un resultado sobre el periodo pedido. Con magnitudes que empiezan en fechas distintas eso deja de bastar: una regla sobre liquidaciones evaluada sobre 2021 no daría ninguna señal, y el resultado saldría plano y aparentemente válido.

El resultado pasa a declarar, por magnitud, desde cuándo había dato a la resolución que la regla necesita, y a negarse a dar por contrastado lo anterior. Es la traducción al backtest del principio que el motor de señales ya sigue: no inventar lo que no se sabe.

*Por qué esto es lo primero y no un detalle final:* sin ello, el resto del cambio produce confianza injustificada, que es peor que no producir nada.

### Una señal dice si su dato era observado o estimado

Binance estima el interés abierto y las liquidaciones suponiendo apalancamientos; Hyperliquid publica posiciones reales en cadena. Presentar ambas igual haría creer que tienen el mismo fundamento.

Guardarlas por separado —cosa que ya hace la ingesta— permite además ver cuándo se contradicen. Promediarlas destruiría justamente esa información.

### Las propuestas de activos nuevos son ideas, no altas

Un activo con la financiación disparada que el usuario no sigue se le propone; no se añade. Encaja con `external-ideas`, que ya define el ciclo de aceptar o descartar.

El límite de cuántas se proponen no es cosmético: en un movimiento general del mercado la condición la cumple todo, y una lista de cien propuestas es una lista que nadie mira. Se proponen las más destacadas y se dice cuántas quedaron fuera.

*Lo que esto obliga:* recoger dato de activos que el usuario no sigue, que la ingesta deliberadamente no hace. Decidir cuánto ampliar la recogida es parte de este cambio, con el coste de almacenamiento ya medido.

### La capa de IA recibe cifras y devuelve texto, y se comprueba

El modelo recibe las señales ya calculadas, sus magnitudes y la posición del usuario. Devuelve una explicación. Antes de enseñarla, el sistema comprueba que toda cifra del texto esté entre las que envió; si no, el texto se descarta.

Esa comprobación es lo que convierte el principio en algo exigible en vez de una buena intención, y es la misma forma que ya siguen los otros tres usos del modelo.

*Por qué no se le pide predecir:* lo hace mal y con seguridad aparente. Un pronóstico equivocado y bien redactado es peor que ninguno, porque se parece a un análisis.

### Hyperliquid entra aquí y no antes

No aporta urgencia —su dato de financiación tiene histórico descargable— pero sí contraste, que es lo que hace falta cuando ya se están juzgando señales.

## Risks / Trade-offs

- **La mayoría de estas métricas tienen menos poder predictivo del que aparenta.** → Es la razón de que el backtest y la declaración de cobertura formen parte del cambio. El resultado esperado razonable es descartar reglas.
- **Las liquidaciones tardarán meses en poder contrastarse.** → Se dice desde el principio y no se ofrece un backtest que las cubra. Por eso se empezó a grabarlas antes.
- **Ampliar la recogida a activos no seguidos multiplica el volumen.** → Se decide con el coste ya medido en el cambio anterior, y sólo para las magnitudes que las propuestas necesitan.
- **Un texto redactado convence aunque esté mal.** → Se comprueban las cifras, se marca visualmente lo redactado y las señales se pueden ver sin él.

## Migration Plan

Aditivo. Las reglas existentes siguen funcionando igual; las nuevas no se pueden guardar para activos sin contrato, que es lo que ya ocurre con un indicador que el motor no calcula.

Revertir es dejar de ofrecer las reglas de derivados y la pantalla. El dato recogido se conserva.

## Open Questions

- **Qué umbral hace destacable una condición** para proponer un activo. Se afina viendo cuántas propuestas salen en un mes normal; el mecanismo y su límite no dependen de la cifra.
