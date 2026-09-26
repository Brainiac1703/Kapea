## Context

Ver `proposal.md`. Lo que sigue parte de dos fuentes: el código y las especificaciones que ya existen, y los ficheros con los que el usuario lleva hoy su seguimiento —una hoja de acciones alimentada desde XTB y otra de cripto—, más los dos scripts que actualizan sus precios.

Esas hojas son la mejor descripción disponible de lo que el usuario mira de verdad, y de ahí salen tres huecos del diseño actual: el efectivo de la cuenta, el peso de cada activo y el resultado acumulado de toda la vida de la cartera. También resuelven la pregunta abierta del proveedor de precios de renta variable, porque su script ya usa uno con una regla de conversión de símbolos probada contra sus datos.

Lo que ya está construido y no se toca: el motor FIFO, la proyección de lotes y resultados, el proveedor de precios de cripto y la ausencia de precio como estado legítimo de una posición.

## Goals / Non-Goals

**Goals:**

- Que la pantalla responda de un vistazo: qué tengo, cuánto vale, cuánto he ganado y cómo está repartido.
- Que el valor total sea el mismo que el usuario ve en su bróker, efectivo incluido.
- Que incorporar una clase de activo nueva sea un grupo más, no una pantalla más.
- Que un proveedor de precios caído degrade la pantalla sin romperla.

**Non-Goals:**

- Gráficas de evolución, velas e indicadores. Fase 4 del roadmap.
- El planificador de aportaciones con porcentaje objetivo y rebalanceo. Es una idea buena y un change propio.
- Histórico del valor de la cartera. Exige guardar una serie temporal y decidir cada cuánto; no hace falta para responder «cuánto tengo hoy».

## Decisions

### El efectivo se deriva de los movimientos, no se introduce

El saldo de una cuenta es la suma del efecto en caja de sus movimientos. Es exactamente lo que hace la hoja del usuario, y tiene la propiedad que importa: no se puede desfasar, porque no es un dato aparte.

**Alternativa descartada** — un saldo editable a mano: se queda obsoleto en la siguiente importación y nadie se entera hasta que las cifras dejan de cuadrar. Y la cifra que descuadra es la que más se mira.

### El efectivo se lleva por divisa

Una cuenta puede acumular saldo en euros y en dólares. Sumarlos sin convertir es el mismo error que el tipo `Money` impide en todo el núcleo, así que aquí tampoco se hace.

Para el total sí hay que convertir, y ahí la conversión es **al tipo de hoy**, no al de cada movimiento: el efectivo es lo que tienes ahora, no la suma histórica de lo que entró. Es una diferencia deliberada con el criterio del coste de los lotes, que sí se congela, y se dice en la pantalla para que no se confunda con aquel.

### El valor total incluye el efectivo

Sin él, «valor total de la cartera» no coincide con lo que el bróker enseña, y una cifra que no cuadra con la fuente es una cifra que no se usa.

### El peso se calcula sobre lo que se ha podido valorar

Si falta el precio de un activo, el peso del resto se calcula sobre el valor conocido y se dice que es parcial.

**Alternativa descartada** — tratar la posición sin precio como valor cero: repartiría su peso entre las demás y daría porcentajes falsos con aspecto de buenos. Es el mismo criterio que ya rige en el resto de la cartera: una ausencia se dice, no se rellena.

### El resultado acumulado es otra pregunta que el fiscal

El motor ya calcula resultados realizados con su fecha de devengo, y la consulta por ejercicio responde qué se declara. Sumarlos todos responde cuánto se ha ganado con la cartera. Son dos cifras distintas y las dos hacen falta; se calculan de los mismos datos.

### Los precios de renta variable van detrás del mismo puerto que los de cripto

Ya existe un puerto de precios de mercado con CoinGecko detrás. Se añade una implementación para renta variable y un despachador que elige por clase de activo.

El proveedor elegido para empezar es el que el usuario ya usa. No es una API oficial y puede dejar de funcionar sin aviso; el puerto es justamente lo que permite cambiarlo sin tocar la cartera, y las especificaciones ya exigen que una posición sin precio se muestre igual.

### La conversión de símbolos es una regla con excepciones explícitas

El bróker entrega símbolos con sufijo de mercado; el proveedor espera otra cosa según el mercado. La regla general cubre el caso común, y una tabla de excepciones cubre el activo que no encaje.

Es la misma forma que ya tienen los alias de Kraken, y la razón es la misma: sin excepciones explícitas, el activo raro obliga a retorcer la regla general hasta que rompe el caso común.

### Los precios se piden al abrir y al refrescar, con una caché corta

Refrescar solo cada pocos segundos consumiría cuota sin cambiar ninguna decisión: el precio de hace cinco minutos vale lo mismo para decidir. Una caché breve en el servidor evita repetir la consulta si la página se recarga varias veces seguidas.

## Risks / Trade-offs

- **El proveedor de renta variable no es oficial y puede romperse** → Asumido y acotado: detrás de un puerto, y con la ausencia de precio ya especificada como estado normal. Si se rompe, las posiciones siguen mostrando cantidad, coste y resultado realizado.
- **El efectivo puede no cuadrar con el bróker** si falta algún movimiento por importar o hay alguno sin clasificar → Se advierte de que el saldo está incompleto mientras queden movimientos sin resolver, en lugar de enseñar una cifra que parece firme.
- **Convertir el efectivo al tipo de hoy mezcla dos criterios en la misma pantalla** —coste congelado, efectivo actual— → Es correcto, pero se presta a confusión, así que se dice en la propia pantalla.
- **Una cartera con muchos activos hace muchas consultas de precio** → Los dos proveedores admiten pedir varios símbolos en una sola llamada, y así se hace.

## Open Questions

- **Qué duración tiene la caché de precios.** No cambia el diseño; se fija con un valor prudente y se ajusta con el uso.
- **Si el efectivo debería tener su propia vista** con el detalle de qué movimientos lo componen. Hoy basta con el saldo y su advertencia; si al usarlo hace falta explicar una cifra, se añade después.
