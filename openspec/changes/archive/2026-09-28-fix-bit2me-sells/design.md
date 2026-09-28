## Context

Ver `proposal.md` — Why. Aquí sólo lo que condiciona la forma de arreglarlo.

`FromWalletTransaction` procesa un movimiento de monedero de Bit2Me en tres pasos: separa el caso de la permuta, deduce el tipo de operación de `type`/`subtype`, y elige de qué lado saca el activo y la cantidad. Los dos primeros funcionan; el tercero es el que falla.

Las piezas que ya existen y no hay que escribir:

- **`Spread(type, amount, paidInEuros)`** contempla la venta desde que se escribió: `published - paid`, aceptado sólo si sale positivo y por debajo del 5 %. Con los datos reales daría 189,51 − 184,63 = 4,88 €. Nunca llegaba a dispararse porque `paidInEuros` venía nulo.
- **`Bit2MeAmount.ValueInEuros`**, que valora un lado con el cambio que la plataforma manda en él.
- **`ImportRun.RecordsRejected`**, que ya cuenta los rechazados de una ejecución.
- **`SynchronizationReport.Problems`**, la lista de avisos que la pantalla de credenciales ya enseña uno a uno y sin caducar.

La permuta cripto-por-cripto se resuelve antes y no se toca: ya parte el movimiento en dos apuntes y ya toma cada lado por separado, que es justamente lo que aquí falta.

## Goals / Non-Goals

**Goals:**

- Que una venta a euros entre con su activo, su cantidad y su ingreso real.
- Que el diferencial se registre como comisión, no como un ingreso menor sin explicar.
- Que un rechazo llegue a quien lanzó la sincronización.

**Non-Goals:**

- Reprocesar solo lo ya rechazado. El reproceso manual existe; automatizarlo es otra conversación y mete una decisión —cuándo y con qué criterio— que este arreglo no necesita.
- Tocar Kraken o el fichero de XTB. Sus adaptadores no comparten este código.
- Rehacer las cifras validadas. Es consecuencia, no tarea: saldrán solas cuando la venta de 2025 entre.

## Decisions

### El lado se elige por el tipo, no por precedencia fija

`type` ya está calculado justo antes, así que basta con usarlo:

- **Venta**: el activo está en el origen.
- **Todo lo demás**: se mantiene `Destination ?? Origin ?? Denomination`, que es lo que hace funcionar compras, ingresos y retiradas.

*Alternativas descartadas:*

- **Elegir el lado que no sea euros.** Funciona para una venta a euros y se rompe en cuanto los dos lados son cripto, que es exactamente la permuta. Además convierte una regla explicable —«en una venta el activo es lo que sale»— en una heurística.
- **Tratar la venta como una permuta de dos apuntes.** Daría una venta de QNT y una compra de euros; los euros no son un activo de cartera y habría que filtrarlos después. Más piezas para el mismo resultado.

### El ingreso sale del lado en euros, venga de donde venga

Hoy `paidInEuros` sólo mira `denomination`, y en una venta esa viene en la moneda del activo. Pasa a tomarse del lado que esté en euros: `denomination` si lo está —que es el caso de la compra y manda, porque es lo que se pagó— y si no, el lado contrario al del activo.

Esto es lo que además hace que `Spread` empiece a funcionar en las ventas sin tocarlo.

*Alternativa descartada:* calcular el ingreso como cantidad × cambio publicado. Da 189,51 € cuando llegaron 184,63 €, y el error se acumula operación a operación en el resultado del ejercicio. El dato real lo manda la plataforma; no hay por qué estimarlo.

### El rechazo viaja con el resultado, no con un registro aparte

`AccountSynchronizationResult` gana el recuento de rechazados y de ahí sale a dos sitios: la línea de registro y la lista de problemas del informe.

Va por `Problems` y no por un aviso nuevo porque es exactamente lo que esa lista significa —algo que el usuario tiene que mirar— y porque ya se enseña sin caducar. Un aviso que desaparece solo no sirve para algo que lleva año y medio pasando desapercibido.

Sólo se avisa cuando hay alguno. Un aviso que aparece siempre deja de leerse, y entonces volveríamos a estar donde estábamos.

## Risks / Trade-offs

- **Cambian cifras que se dieron por validadas** → Es el objetivo, no un daño: faltaba una venta. Conviene rehacer el contraste contra la declaración después y no dar por bueno el anterior.
- **El tope del 5 % del diferencial podría dejar fuera una venta con mucho deslizamiento** → Entonces el ingreso se registra sin comisión y el resultado sale algo peor, nunca inventado. Es el mismo criterio que ya se aplica a las compras y conviene no tocarlo en este cambio.
- **Puede haber otras formas de venta que Bit2Me exprese distinto** → Este arreglo cubre lo que la cuenta del usuario contiene, que son las dos rechazadas. Con el recuento a la vista, cualquier otra forma aparecerá la primera vez en lugar de dentro de un año.

## Migration Plan

Ninguna migración de datos. Los dos rechazos están guardados con su contenido original, así que entran reprocesando sus ejecuciones o releyendo el histórico completo, que es lo que el usuario ya sabe hacer.

Después de que entren, rehacer el contraste fiscal.
