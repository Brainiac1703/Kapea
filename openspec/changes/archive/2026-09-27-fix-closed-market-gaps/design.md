## Context

Ver proposal.md para el porqué y las cifras medidas. Lo que condiciona el diseño:

- `PortfolioHistory.Build` compone un `PortfolioDay` por fecha con un `AssetDay` por activo con posición, y marca `IsComplete = false` cuando a alguno le falta el precio de ese día.
- `PortfolioQueries.DaysAsync` pide los precios del rango con `IPriceHistoryStore.GetAsync(assetIds, from, to)`. **Sólo del rango**: para arrastrar un cierre anterior al primer día pedido hace falta mirar antes de él.
- `Evolution.razor` ofrece 90, 365 y 3.650 días; el endpoint usa 365 por omisión. `AssetEvolution.razor` ya sabe pedir la serie completa sin fecha de inicio desde el cambio anterior, y el servidor la resuelve como la primera cotización guardada de ese activo.
- La serie de precios está completa y medida: las criptomonedas tienen los 366 días del último año, las acciones 250 o 251. El único hueco real de cripto en veintiséis años es AVAX entre julio y septiembre de 2020.

## Goals / Non-Goals

**Goals:**

- Que el patrimonio de un fin de semana sea el de verdad, y la línea no se corte.
- Que «incompleto» vuelva a significar algo: falta un precio que debería estar.
- Que se vea qué días llevan precio arrastrado.
- Que la serie completa empiece donde hay datos.

**Non-Goals:**

- Un calendario de festivos por bolsa. Habría que mantenerlo, se rompe solo, y no hace falta para esto.
- Rellenar lagunas reales. Un precio que debería existir y no está sigue siendo un hueco.
- Tocar la serie de precios. Un día sin cotización sigue sin tenerla.
- La escala de rangos, las velas, el cursor y las cifras: van aparte.

## Decisions

### El mercado estaba cerrado si ningún activo de esa clase cotizó

La señal sale de los propios datos: si ninguna acción del catálogo tiene precio el 4 de julio, la bolsa estaba cerrada. Si cinco lo tienen y una no, a esa una le falta el dato.

*Por qué no un calendario:* habría que mantener uno por bolsa, con sus festivos móviles, y cada activo tendría que saber a qué bolsa pertenece. Es más código, más que mantener, y se equivoca en silencio el día que cambia un festivo. Los propios datos ya lo dicen.

*Sus límites, que hay que escribir:* con una sola acción en el catálogo, un día que le falte el dato parecerá mercado cerrado. Es el precio de no mantener un calendario, y con seis acciones de tres bolsas la señal es firme. La clase es la unidad porque es lo que Kapea distingue hoy; si algún día conviven bolsas con calendarios muy distintos dentro de la renta variable, habrá que afinar a mercado y no a clase.

*Por qué la clase y no «todos los activos»:* las criptomonedas no cierran nunca. Mezclarlas haría que ningún día pareciera cerrado jamás.

### El arrastre vive en la valoración, no en la serie de precios

La serie dice lo que cotizó el mercado; la valoración dice lo que vale la cartera. Son dos preguntas distintas y el arrastre sólo responde a la segunda.

*Lo que esto evita:* que los indicadores, los sistemas y el backtest vean precios que nadie cotizó. Una media móvil sobre cierres arrastrados daría el promedio de un relleno. El requisito que prohíbe inventar cotizaciones se queda intacto, y se cumple.

### Un precio arrastrado se marca, y se marca hasta arriba

Cada valoración dice si el precio es del día o arrastrado, y de cuándo. El día completo dice si alguna de sus posiciones lo lleva.

*Por qué no basta con que salga bien el número:* un patrimonio de sábado calculado con cierres del viernes es correcto y además no es un dato nuevo. Presentarlo igual que el del lunes haría creer que el mercado se movió cuando estaba cerrado.

### Para arrastrar hay que mirar antes del rango

Pedir sólo el rango no basta: el primer día pedido puede ser un sábado. La consulta necesita el último cierre anterior de cada activo con posición.

Se resuelve pidiendo, además del rango, el último precio conocido antes de su inicio. Es una consulta más y acotada, no traerse la serie entera.

### «Todo» lo resuelve quien conoce la serie

En el patrimonio, el primer día con movimientos. Es el mismo criterio que ya se aplicó a la serie de un activo, donde «todo» se resuelve como su primera cotización guardada, y por la misma razón: cuánto hay no lo sabe quien pregunta.

*Alternativa descartada:* subir los 3.650 días a un número mayor. Sólo mueve el problema y sigue rellenando con ceros lo anterior al primer movimiento.

## Risks / Trade-offs

- **El patrimonio histórico cambia en 167 días.** → Es la corrección de un error, y hacia arriba. Lo validado contra los datos reales —coste, realizado, efectivo y los dos ejercicios fiscales— no depende de precios de mercado y tiene que salir idéntico. Hay tarea de comprobarlo.
- **La heurística de mercado cerrado puede equivocarse** con muy pocos activos de una clase. → Queda escrito, y el caso se degrada a lo de ahora: el día sale incompleto.
- **Arrastrar un cierre muy antiguo** valoraría al precio de hace meses si un activo dejó de cotizar. → El día se sigue pudiendo distinguir porque la valoración dice de cuándo es el precio.

## Migration Plan

Nada que migrar: se calcula distinto sobre los mismos datos. La serie de precios no se toca.

Revertir es dejar de arrastrar y volver a marcar esos días incompletos.

## Open Questions

Ninguna.
