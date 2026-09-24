## Context

Ver proposal.md para el porqué. Lo que condiciona el diseño es lo que ya existe:

- `CashEffect` ya decide qué mueve el dinero de una cuenta: un ingreso suma, una retirada resta, y lo que no pasó por caja no cuenta.
- `CashBalanceCalculator` produce el saldo por cuenta y divisa a partir de los mismos movimientos, y `CashTotal` lo pasa a euros diciendo qué divisas se quedaron sin tipo.
- `PortfolioSummary.Build` compone en el dominio las cifras de la cartera —patrimonio, coste, resultado acumulado, rendimientos— a partir de las posiciones y del efectivo, y `PortfolioQueries` sólo las traduce al contrato.
- `ValuedTransaction` ya sabe si un movimiento es la entrada o la salida de un traspaso interno confirmado, y el filtro de movimientos en vigor ya excluye los anulados antes de llegar al cálculo.
- `Wealth` ya se da siempre, aunque falte algo, diciendo qué falta. Es el patrón que sigue la aplicación con las cifras incompletas.

## Goals / Non-Goals

**Goals:**

- Que el usuario vea cuánto ha puesto y cuánto tiene, sin hacer cuentas.
- Que la cifra se calle cuando no puede ser fiel, en lugar de redondear una mentira.
- Que salga de los mismos movimientos que el saldo, para que no puedan contarse dos historias distintas del mismo dinero.

**Non-Goals:**

- Sustituir la rentabilidad ponderada por dinero. Esa mide otra cosa y ya está especificada; aquí no se pondera nada.
- Reconstruir aportaciones anteriores a lo importado. Lo que no está, no está, y se dice.
- Valorar lo que entró desde una cartera externa. Requeriría un precio de aquel día y una decisión del usuario sobre qué fue aquello; se avisa y ya.

## Decisions

### Se calcula en el dominio, junto al resto de las cifras

Un cálculo propio sobre los mismos movimientos valorados que ya recibe el saldo, y un hueco más en `PortfolioSummary`. Ni la consulta ni la pantalla hacen aritmética.

El motivo es el de siempre en este proyecto: las cifras se componen donde se pueden comprobar sin base de datos, y así el aportado y el saldo no pueden desacompasarse, porque salen del mismo sitio.

### Una sola definición de lo que es aportar

La serie de evolución ya sumaba aportaciones para separarlas del rendimiento, pero con otro criterio: contaba cualquier ingreso o retirada, incluidos los traspasos entre cuentas propias y las entradas de activos. Dos definiciones del mismo dinero acaban dando dos cifras distintas, y la de la serie era la equivocada: un traspaso interno inflaba el ahorro y hundía el rendimiento sin que hubiera pasado nada.

La regla vive ahora en un único sitio y la comparten las dos. Hoy ninguna cifra cambia, porque el histórico del usuario no tiene traspasos internos ni entradas de activos; cambiará el día que los haya, y entonces coincidirán.

*Lo que queda abierto:* un traspaso entre cuentas propias que el detector no llegue a emparejar sigue contando como dinero nuevo, porque sin esa pareja no hay forma de distinguirlo de una aportación. El que sí se ha propuesto y está pendiente de resolver no cuenta, porque se aparta antes con el resto de lo no resuelto. Se dice en la pantalla en lugar de callarlo.

### Sólo cuenta lo que es dinero

Un ingreso de dinero es el que no trae activo: es la transferencia, la tarjeta o el pago móvil. Un ingreso que trae un activo es otra cosa —cripto que llega de una cartera de fuera— y no es una aportación en dinero, así que no suma.

*Lo que esto obliga:* si existe alguno de esos, lo aportado se queda corto y compararlo con el patrimonio da una pérdida mayor de la real. Por eso el cálculo lo detecta y lo declara, igual que `Wealth` declara que le faltan precios.

*Alternativa descartada:* valorar esas entradas al precio del día y sumarlas. Sería inventar una aportación que quizá no lo fue: unas unidades regaladas, un pago recibido en cripto o un traspaso desde otra plataforma del propio usuario no son dinero puesto de su bolsillo, y cada caso pide una respuesta distinta.

### El traspaso interno confirmado no cuenta, el no confirmado tampoco suma de más

Un traspaso entre cuentas propias sale por un lado y entra por el otro; contarlo inflaría a la vez lo ingresado y lo retirado sin cambiar el neto, pero dejaría dos cifras falsas a la vista. Se excluyen las dos patas cuando el traspaso está confirmado.

Mientras un traspaso está sólo propuesto, sus movimientos ya se excluyen del cálculo por la vía que excluye lo no resuelto, así que no hace falta nada más.

### El porcentaje no existe sin aportación

Dividir entre cero no es cero por ciento. Sin aportaciones, la cifra se da vacía y la pantalla dice que no hay nada con lo que comparar. Es la misma regla que ya usa el peso de una posición sin precio.

### La diferencia se muestra donde ya se mira el patrimonio

En Cartera, junto al patrimonio, y en Inicio, con las cifras que ya están ahí. No se inventa una pantalla nueva para un dato que se lee de un vistazo.

## Risks / Trade-offs

- **Una aportación que Kapea no vio** —una plataforma que no se importa, un histórico que empieza tarde— deja la cifra corta y el porcentaje exagerado. → Se avisa cuando se detecta la causa reconocible, la entrada de activos desde fuera; lo demás no se puede saber y la documentación dirá de dónde sale el número.
- **Confundirlo con la rentabilidad** es fácil, porque se parecen. → El texto de la pantalla lo nombra por lo que es, dinero puesto frente a dinero que hay, y la rentabilidad sigue en su sitio.
- **Un ingreso mal clasificado** cambia la cifra. → Es el mismo dato que ya mueve el saldo de la cuenta; si estuviera mal, ya estaría mal el efectivo, y se corrige donde se corrige todo.

## Migration Plan

Nada que migrar: se compone de movimientos que ya están guardados y no se persiste ninguna cifra nueva. Revertir es dejar de mostrarla.

## Open Questions

Ninguna.
