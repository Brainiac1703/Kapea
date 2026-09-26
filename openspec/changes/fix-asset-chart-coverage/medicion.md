# Lo que cuesta dibujar la serie entera

Medido el 26 de septiembre de 2026 contra los datos reales del usuario, con MSTR.US, que
es el activo más largo: 9.764 días de rango y 6.723 con cotización, desde el 3 de enero
de 2000.

## El servidor no era el problema

| Activo | Días | Con precio | Respuesta | Tamaño |
|---|---|---|---|---|
| ADA | 3.244 | 3.244 | 490 ms | 799 KB |
| NOW.US | 5.203 | 3.580 | 345 ms | 1.012 KB |
| MSTR.US | 9.764 | 6.723 | 360 ms | 1.893 KB |

Medido otra vez desde el navegador, la petición de MSTR tardó **172 ms** y trajo 1.893 KB.

## El navegador sí

Con la serie entera, la pantalla tardaba **más de veinte segundos** en responder desde que
llegaba el dato. Una medición con el depurador llegó a abortar por pasar de cuarenta y
cinco segundos sin que el renderizador contestara.

La causa no eran las bandas del cursor, que ya estaban limitadas a doscientas. Eran las
polilíneas: la línea se parte en cada día sin cotización, y veintiséis años de una acción
son **1.411 tramos** sólo del precio, más los de las dos medias y la fuerza relativa. Cada
tramo es un elemento con su cadena de coordenadas, y el navegador tiene que comparar todos
en cada redibujado.

## Lo que se hizo

Reducir lo que se dibuja, no lo que se guarda ni lo que se calcula. El dibujo mide mil
unidades de ancho, así que más de mil puntos se pisan entre sí: la serie se agrupa en
tramos consecutivos y de cada tramo sale un punto con valor, si lo hay.

Un tramo entero sin valor sigue siendo un hueco, de modo que una interrupción real —los
setenta días que AVAX no tiene en 2020— se sigue viendo cortada, mientras que un fin de
semana suelto deja de partir la línea cuando se miran años.

| | Antes | Después |
|---|---|---|
| Polilíneas con «Todo» en MSTR.US | 1.411 | **4** |
| Tiempo hasta que responde | más de 20 s | inmediato |

Los indicadores se siguen calculando en el servidor sobre todos los días, y lo que se lee
al pasar el ratón sale de la serie entera. Reducir para pintar no cambia una media.
