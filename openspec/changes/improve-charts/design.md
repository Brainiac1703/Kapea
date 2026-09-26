## Context

Ver proposal.md para el porqué. Lo que condiciona el diseño:

- `DailyPrice` es `(AssetId, Date, PriceInEuros, Source)`: un número por día. 53.614 filas desde el 3 de enero de 2000.
- `LineChart` dibuja en SVG a mano, con un `polyline` por tramo sin huecos y un `rect` invisible por banda para el cursor, limitado a 200. Desde el cambio anterior reduce a mil los puntos que dibuja, y por eso veintiséis años se pintan al instante.
- `AssetEvolution.razor` ya tiene selector de periodo con 3 meses, 1 año, 5 años y todo, y otro para la ventana del indicador.
- `TechnicalIndicators` calcula media simple y exponencial, fuerza relativa, convergencia de medias, bandas de volatilidad y recorrido medio. Los indicadores se calculan sólo sobre días con precio, a propósito.
- Cobertura medida: Yahoo cubre la renta variable y las criptomonedas grandes; POL, PEPE y TAO sólo los cubre CoinGecko, con un año de historia y sin recorrido diario en su capa gratuita.
- `fix-closed-market-gaps` va antes y resuelve los cortes de fin de semana. Este cambio da por hecho que una posición en día cerrado ya se valora al último cierre.

## Goals / Non-Goals

**Goals:**

- Que se pueda mirar un mes, un trimestre o lo que va de año sin saltar a todo.
- Que se vea cuánto se movió un activo cada día, no sólo dónde acabó.
- Que la gráfica diga cifras en lugar de obligar a estimarlas a ojo.
- Que la cobertura desigual se note como una limitación del proveedor y no como una pantalla rota.

**Non-Goals:**

- Intradía. Queda anotado y es un cambio aparte, del tamaño de la ingesta de derivados.
- Una librería de gráficas. El componente actual es de cien líneas y hace lo que hace falta; cambiarlo traería su tema, su localización y su peso.
- Predecir. Las bandas describen el pasado y se rotulan así.

## Decisions

### El recorrido se guarda junto al cierre, no en otra tabla

Tres columnas opcionales en la fila que ya existe. Un día es un día: separarlo obligaría a unir dos tablas en cada consulta de serie, que es la consulta más frecuente del sistema.

*Que sean opcionales es parte del diseño*, no una concesión: hay activos que nunca los tendrán. Nulo significa «este proveedor no lo da», y eso se distingue de un recorrido de cero.

*Cómo se rellenan los 53.614 días que ya hay:* volviendo a pedirlos donde el proveedor los dé. Es una pasada larga, y ya existe el mecanismo del cambio anterior para que no retrase el precio de hoy. Lo que ya está guardado no se pierde: el cierre se conserva y las columnas nuevas se completan.

### La agregación se calcula al pedirla, no se guarda

Una vela semanal sale de sus días. Guardarla sería duplicar el dato y tener que rehacerla cada vez que llega un precio corregido.

Son decenas de miles de filas como mucho, agrupadas por semana o por mes: es una operación barata comparada con la consulta que ya se hace.

*Regla que no puede romperse:* el máximo de un tramo es el mayor de los máximos de sus días, no el mayor de sus cierres. Es el error clásico al agregar velas, y deja los extremos sistemáticamente por debajo de la realidad.

### El periodo sugiere la agregación, y no la impone

Cinco años en días son más puntos que píxeles; en semanas se ve la forma. Pero quien quiera ver cinco años día a día debe poder.

Así que el periodo trae una agregación por omisión —días hasta seis meses, semanas hasta dos años, meses más allá— y se puede cambiar. Es lo que hacen las aplicaciones que el usuario cita: el rango y el intervalo son dos controles, no uno.

*Por qué no dejar que el componente reduzca y ya está:* reducir para dibujar tira puntos, y con ello los extremos. Una vela semanal conserva el máximo de la semana; un muestreo se lo salta. Para mirar años, agregar es mejor que reducir.

### Velas cuando aportan, banda cuando no

Con pocos puntos y recorrido disponible, velas. Con muchos puntos, la vela se vuelve una línea vertical de un píxel y no aporta: mejor la línea del cierre con una banda que marque el recorrido del tramo. Sin recorrido, la línea sola.

Las tres formas conviven en el mismo componente y se eligen por lo que hay, no por una opción que el usuario tenga que entender.

### Las cifras salen del periodo elegido

El bloque bajo la gráfica dice la apertura, el máximo y el mínimo del último día; el máximo, el mínimo y la variación del periodo; y el máximo y el mínimo de 52 semanas, que es una referencia estándar e independiente del periodo.

*Por qué del periodo y no del día:* mirando cinco años, el máximo del último día no interesa. La cifra tiene que responder a lo que se está mirando.

### La dispersión se rotula por lo que es

Las bandas de volatilidad y la envolvente del recorrido medio ya se calculan. Se dibujan diciendo sobre qué ventana están calculadas y que describen lo ocurrido.

*Lo que no se hace, y por qué:* ninguna banda dice hacia dónde va el precio. Que haya estado dentro el noventa por ciento del tiempo no es la probabilidad de que lo esté mañana. Rotularlo como probabilidad convertiría una medida del pasado en una promesa, y es el tipo de cifra que lleva a arriesgar más de la cuenta.

### El cursor lee la serie entera, no la dibujada

Ya ocurre: la lectura busca el día en los puntos completos mientras el dibujo usa los reducidos. Con la agregación hay que mantenerlo: el cursor sobre una vela semanal dice la semana, no un día suelto de ella.

## Risks / Trade-offs

- **Volver a descargar 53.614 días.** → Pasada larga y puntual, con el mecanismo que ya existe. El cierre guardado no se toca.
- **Cobertura desigual entre activos.** → Tres de veintitrés se quedan sin recorrido. La pantalla lo dice; es la misma limitación que ya tienen con la historia de un año.
- **Casi todo el trabajo es de interfaz**, que es donde menos red hay. → Las pruebas manuales de las tareas son parte del trabajo, no un extra.
- **Más controles pueden abrumar.** → Dos: periodo e intervalo, como en las aplicaciones que el usuario ya usa. El resto se decide solo.

## Migration Plan

Las tres columnas nacen vacías y todo sigue funcionando: un día sin recorrido se dibuja como hoy. El relleno entra poco a poco, activo por activo.

Revertir es dejar de pedir el recorrido y volver a la línea de cierres. Lo descargado no estorba.

## Open Questions

- **Cuántos puntos hacen que una vela deje de aportar** y convenga la banda. Se decide mirándolo, y no cambia las specs ni el reparto de tareas.
