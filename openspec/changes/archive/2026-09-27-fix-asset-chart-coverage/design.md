## Context

Ver proposal.md para el porqué y las cifras medidas. Lo que condiciona el diseño es cómo se construye hoy la serie.

- `PortfolioQueries.DaysAsync` reconstruye la cartera entera para un rango: lee los movimientos, saca de ellos la lista de activos, pide los precios **de esos activos** y llama a `PortfolioHistory.Build`.
- `PortfolioHistory.Build` devuelve un `PortfolioDay` por fecha, con un `AssetDay` por activo **con posición** ese día. El precio vive dentro del `AssetDay`.
- `PortfolioHistory.ForAsset` saca la serie de un activo buscándolo en la lista de posiciones de cada día: `day.Assets.FirstOrDefault(...)`. Si no hay posición no hay `AssetDay`, y por tanto tampoco precio.
- `PortfolioQueries.GetAssetHistoryAsync` calcula los indicadores sobre los días que tienen precio, a propósito: rellenar daría la media del relleno y no la del mercado.
- `LineChart` parte la línea en cada punto cuyo valor es nulo, así que un día sin precio se ve como un corte.
- `AssetEvolution.razor` tiene botones para la ventana del indicador (7, 20, 50) pero ninguno para el rango, y pide 365 días escritos a mano. `Evolution.razor`, la de la cartera, sí tiene selector de periodo.

## Goals / Non-Goals

**Goals:**

- Que la cotización de un activo se pueda ver aunque no se tenga ni se haya tenido nunca.
- Que siga distinguiéndose un día sin cotización de un día sin posición.
- Que se pueda ver toda la historia que hay, no un año.

**Non-Goals:**

- Tocar cómo se calcula la evolución de la cartera entera. Ahí el precio de un activo que no se tiene no pinta nada, y meterlo cambiaría cifras que están validadas contra los datos reales.
- Rellenar los días sin cotización.
- Rehacer la gráfica. Corta la línea en los huecos y así debe seguir.

## Decisions

### La cotización sale de la serie de precios, no de la posición

Es el fondo del asunto. Hoy el precio viaja dentro del `AssetDay`, que es un dato de la posición, y por eso desaparece con ella. Son dos cosas distintas:

| | Depende de tener el activo | Qué significa un hueco |
|---|---|---|
| Unidades y valor | Sí | No había posición |
| Cotización | No | El mercado no dio precio ese día |

La serie de un activo se compone por tanto de dos fuentes: la cotización, de la serie de precios; las unidades y el valor, de la posición reconstruida. Coinciden donde hay posición, y donde no la hay sigue habiendo cotización.

*Alternativa descartada:* meter en `PortfolioDay` un `AssetDay` con cantidad cero para los activos que no se tienen. Cambiaría la forma de la serie de la cartera —que recorre `day.Assets` para sumar y para decidir si el día está completo— y obligaría a distinguir por todas partes entre «lo tengo a cero» y «no lo tengo». Justo lo contrario de lo que este cambio busca.

### El activo consultado se pide siempre, tenga movimientos o no

`DaysAsync` saca los activos de los movimientos porque reconstruye la cartera, y ahí eso es correcto. Para la serie de uno solo hace falta además su cotización, exista movimiento o no.

Se resuelve pidiendo la serie de precios del activo consultado por separado, en lugar de ampliar la lista que alimenta la reconstrucción de la cartera. Así el valor de la cartera no cambia por consultar una gráfica, que es un efecto que nadie espera y que movería cifras ya validadas.

### Un activo que no se tiene no hace incompleto un día

`IsComplete` significa que faltó el precio de algo con posición, y eso no cambia. Conviene dejarlo escrito porque, al entrar cotizaciones de activos sin posición, es la confusión natural: un festivo de una acción que sólo se vigila no rompe nada, porque esa acción no entra en el valor de la cartera.

### El rango se elige, y «todo» significa todo

La pantalla de un activo gana el mismo selector que ya tiene la de la cartera. La diferencia es qué significa «todo»: allí son 3.650 días escritos a mano, que bastan porque la cartera empieza con el primer movimiento. Aquí no bastan: MSTR.US tiene 6.723 días.

«Todo» pasa a ser una petición sin fecha de inicio, que el servidor resuelve como la primera cotización guardada de ese activo. Pedir un rango que empieza antes no es un error: se devuelve lo que hay.

*Por qué no subir el número:* elegir 10.000 en lugar de 3.650 sólo mueve el problema, y obliga a acertar un número que depende de qué activo se mire.

### Cuántos puntos aguanta la gráfica se mide antes de ofrecerlo

`LineChart` dibuja un `polyline` por tramo con un punto por día. Seis mil puntos en un SVG es mucho más de lo que se ha probado, y nadie distingue seis mil días en una pantalla.

Se mide primero con el activo más largo que hay. Si no va fino, se reduce la resolución de lo que se dibuja —no de lo que se guarda ni de lo que se calcula— y se deja escrito el criterio. Los indicadores se siguen calculando sobre todos los días: reducir para pintar no puede cambiar una media.

## Risks / Trade-offs

- **La gráfica puede ir lenta con miles de puntos.** → Se mide antes de ofrecer «todo», y si hace falta se reduce sólo lo que se pinta.
- **Confundir las dos series al arreglarlas.** → Lo que protege es que las cifras de la cartera están validadas contra los datos reales del usuario: coste, realizado, efectivo y los dos ejercicios fiscales tienen que salir idénticos después.
- **Más días con cotización hacen aparecer más huecos reales**, como los fines de semana de las acciones o los setenta días que AVAX no tiene en 2020. → Son ciertos y se ven como cortes. Rellenarlos sería mentir.

## Migration Plan

Nada que migrar: sólo se devuelven días que hasta ahora se descartaban. Ningún dato guardado cambia.

Revertir es volver a componer la serie desde la posición.

## Open Questions

Ninguna.
