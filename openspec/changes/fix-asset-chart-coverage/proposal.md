## Why

La pantalla de evolución de un activo dibuja su precio con las medias móviles y la fuerza relativa encima. Para casi todo lo que el usuario sigue, esa pantalla está vacía.

Hay dos series distintas que hoy son la misma, y de ahí salen los tres síntomas:

- **La serie de la posición**: cuántas unidades había y cuánto valían. Sólo tiene sentido mientras se tiene, y sus huecos son información.
- **La serie del mercado**: a cuánto cotizó el activo. Existe se tenga o no, y es la que necesitan las medias, la fuerza relativa y cualquier sistema.

La pantalla pide la primera y dibuja la segunda. Comprobado hoy contra los datos reales:

| Activo | Días devueltos | Con precio | En la base |
|---|---|---|---|
| BTC, en cartera todo el año | 366 | 366 | 366 |
| NOW.US, comprado y vendido varias veces | 366 | **87** | 251 |
| ADA, vigilado y nunca comprado | 366 | **0** | 366, y 3.244 desde 2017 |

La serie diaria está completa: en el último año todas las criptomonedas tienen los 366 días y las acciones 250 o 251, que son los días hábiles. No falta dato; la consulta lo descarta.

Encima, la pantalla pide 365 días escritos a mano y no ofrece cambiarlo, así que no se puede ver más de un año por mucho que haya. Tras ampliar el histórico hay 53.614 precios desde el año 2000.

Esto choca de frente con la razón de ser de la lista de seguimiento, que ya está especificada: sin precios no hay señales, y sin señales un sistema de entrada no sirve para decidir dónde entrar. Y bloquea lo siguiente que el usuario quiere hacer, que es traer ideas de fuera y mirar el gráfico de activos que todavía no ha comprado.

## What Changes

- **La serie de un activo deja de depender de tenerlo.** El precio y los indicadores salen de la cotización del mercado, exista posición o no. La cantidad y el valor de la posición siguen siendo lo que eran: nulos cuando no se tiene, porque no había nada.
- **Un activo sin un solo movimiento tiene serie.** Hoy la lista de activos que se consultan sale de los movimientos, así que lo que sólo se vigila no entra.
- **Se puede elegir el rango.** La pantalla de un activo gana el selector de periodo que la de la cartera ya tiene, incluido ver todo lo que haya.
- **Un día sin cotización se sigue distinguiendo de un día sin posición.** El fin de semana de una acción no es un hueco de la serie, y no se rellena con nada.
- **Fuera de alcance**: cómo se calcula la evolución de la cartera entera, y rellenar días sin cotización con valores inventados.

## Capabilities

### Modified Capabilities

- `portfolio-history`: la serie de un activo pasa a existir aunque nunca se haya tenido, y a llevar el precio de mercado de todos los días, no sólo de aquellos con posición.

## Impact

- **Consulta**: hoy se piden los precios de los activos que aparecen en movimientos. Habrá que pedir además los del activo consultado, y la evolución de la cartera entera no debe cambiar por ello.
- **Interfaz**: un selector de periodo más en la pantalla de un activo, con sus textos en los dos idiomas.
- **Rendimiento**: pedir todo el histórico de un activo son miles de puntos —MSTR.US tiene 6.723 días— frente a los 366 de ahora. Hay que ver qué aguanta la gráfica antes de ofrecerlo.
- **Lo que no cambia**: ninguna cifra de la cartera, ningún resultado fiscal y ninguna señal. Esto sólo añade días a una consulta que hoy los descarta.
