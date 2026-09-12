# Validación con los datos del usuario

Hecha el 12 de septiembre de 2026 sobre su cartera real: doce activos abiertos, 4.850 €
aportados y algo más de año y medio de histórico.

## Rendimiento del último año

| | Valor |
|---|---|
| Rentabilidad de las decisiones | −45,08 % |
| Rentabilidad de su dinero | −32,92 % |
| Volatilidad anualizada | 45,44 % |
| Mayor caída desde un máximo | −33,83 %, sin recuperar |
| La misma inversión solo en BTC | −30,52 % |

Las dos rentabilidades difieren en doce puntos, y eso es informativo: sus aportaciones
más grandes llegaron en momentos comparativamente buenos, así que lo que se ha llevado es
mejor que lo que dicen sus decisiones de reparto.

**Su mayor apuesta lo habría hecho mejor que su cartera.** Poner el mismo dinero, en las
mismas fechas, solo en bitcoin habría perdido un 30,5 % frente al 45,1 % de la cartera
repartida. Con una volatilidad del 45 % anual, una caída del 34 % es lo esperable y no una
anomalía.

### Contraste con las plataformas

No se ha podido contrastar contra una rentabilidad publicada por Bit2Me ni por Kraken,
porque ninguna de las dos la da en estos términos. Lo que sí cuadra es lo que alimenta el
cálculo: el valor de hoy coincide con la pantalla de cartera salvo el movimiento del
mercado desde el último cierre, y las aportaciones suman exactamente los 4.850 € que el
usuario tenía anotados.

## Concentración

Las tres mayores posiciones son el **71,8 %** de la cartera, por encima del umbral del
70 %. BTC y ETH superan además el tope del 25 % por posición, con un 27,2 % y un 27,1 %.

## Lo que cuesta operar mucho

Se declaró un sistema de operativa frecuente a propósito —cruces de la media de veinte
días, sin filtro de tendencia— y se simuló sobre los doce activos con mil euros por
operación.

| | Valor |
|---|---|
| Operaciones | 292 |
| Resultado | −6.322,21 € |
| De ello, comisiones | −5.798,65 € |

**El 92 % de la pérdida son comisiones.** Con doce mil euros desplegados, el sistema se
deja más de la mitad, y casi todo se lo lleva el 1,9 % por operación completa. Ningún
acierto razonable compensa eso: para que 292 operaciones dejaran algo, habría que ganar
casi un 2 % neto en cada una.

Esta es la conclusión más útil de toda la fase, y contradice la intuición de que operar
más da más oportunidades. A esta escala de capital y con estas comisiones, **la operativa
frecuente está descartada por aritmética**, no por opinión.

## Sistemas simulados y con qué supuestos

| Sistema | Operaciones | Resultado | Comisiones |
|---|---|---|---|
| Cruce de 50 con filtro de 200 | 6 | −243,13 € | −118,22 € |
| Cruce de 20 sin filtro | 292 | −6.322,21 € | −5.798,65 € |

Supuestos comunes: mil euros por operación, 0,95 % dentro del precio en cada dirección
—medido en sus propios movimientos—, ejecución al cierre del día siguiente a la señal,
tramos del ahorro de España, y las series diarias guardadas desde mayo de 2025.

El filtro de tendencia es lo que separa los dos resultados: con él hubo seis operaciones
y sin él doscientas noventa y dos. No hizo ganar dinero, pero evitó perderlo a paladas.

## Pendiente

- Contrastar contra un sistema descrito por un analista, cuando se traduzca a reglas con
  el servicio de IA configurado.
- No se puede borrar un sistema, solo corregirlo. El de prueba de comisiones queda a la
  vista, con su nombre diciendo para qué era.
