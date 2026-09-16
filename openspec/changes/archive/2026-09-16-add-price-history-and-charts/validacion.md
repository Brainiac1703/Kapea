# Contraste de las gráficas con los datos reales

Hecho el 12 de septiembre de 2026 sobre la cartera del usuario: 12 activos abiertos,
4 vendidos por completo y unos 2.400 movimientos.

## El valor de hoy coincide con la pantalla de cartera

| | Importe |
|---|---|
| Cartera, al precio de ahora | 3.757,78 € |
| Gráfica, al cierre de ayer | 3.752,62 € |

Los cinco euros de diferencia son lo que ha variado el mercado desde el último cierre
guardado. La gráfica trabaja con cierres diarios y la cartera con el precio del momento,
así que coincidir al céntimo significaría que una de las dos miente.

## Lo aportado cuadra con lo que el usuario tenía anotado

La línea de aportaciones termina en **4.850,00 €**, exactamente la cifra que él daba
como dinero invertido. Sale de sumar ingresos menos retiradas de los movimientos
importados, sin que nadie la escriba a mano.

## Días incompletos

| Periodo | Días | Incompletos |
|---|---|---|
| Último año | 366 | 1 |
| Desde el primer movimiento | 496 | 102 |

El único día incompleto del último año es el primero del periodo, y los 102 del histórico
completo están entre mayo y septiembre de 2025. Todos se deben a `POL`, que se compró en
junio de 2025 y se vendió en octubre: CoinGecko no sirve fechas anteriores a un año y
Yahoo no lo cotiza con ningún símbolo conocido, así que su tramo inicial se queda sin
precio.

Esos días la gráfica enseña el valor de lo demás y avisa arriba de cuántos son. Como POL
fueron 50 €, el efecto es pequeño, pero se dice en lugar de disimularse.

## Lo que hizo falta corregir para llegar aquí

- Los activos vendidos por completo no pedían precios. Se tuvieron durante meses, así que
  su tramo aparecía vacío: los días incompletos bajaron de 45 a 1 al incluirlos.
- El relleno solo avanzaba hacia delante, de modo que una serie que empezó tarde nunca
  recuperaba su principio.
