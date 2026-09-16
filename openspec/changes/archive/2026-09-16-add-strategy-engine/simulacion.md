# Simulación del sistema de partida sobre los datos reales

Ejecutada el 12 de septiembre de 2026 sobre los doce activos con posición abierta, con
mil euros por operación y el coste real del usuario: un 0,95 % dentro del precio en cada
dirección, o sea casi un 1,9 % por operación completa.

El sistema simulado es el de partida: entra cuando el precio cruza al alza su media de
cincuenta días **estando por encima de la de doscientos**, y sale al perder la de
cincuenta, con salida a dos veces el recorrido diario y objetivo al doble de esa
distancia.

## Lo que salió

| Activo | Operaciones | Acertadas | Resultado | Comprar y no tocar | Comisiones |
|---|---|---|---|---|---|
| PAXG | 2 | 0 | −51,34 € | +54,37 € | 39,71 € |
| TAO | 2 | 0 | −168,87 € | −425,09 € | 38,52 € |
| USDG | 2 | 0 | −22,92 € | −13,86 € | 39,99 € |
| Los otros nueve | 0 | — | 0 € | entre −64 € y −658 € | 0 € |

## Lo que eso significa

**Ninguna de las seis operaciones acertó.** Perdieron 243,13 € en total, y **118,22 € de
esa pérdida son comisiones**, casi la mitad. Con mil euros por operación, cada entrada y
salida se lleva unos veinte euros antes de que el precio haga nada.

**El filtro de tendencia hizo su trabajo.** En nueve de los doce activos no hubo ni una
entrada, porque el precio nunca estuvo por encima de su media de doscientos días. Eso
evitó pérdidas de entre 64 € y 658 € por activo, que es lo que habría dado comprar y
aguantar. En un mercado que cae, no operar es una decisión, y el sistema la tomó.

**Donde sí operó, perdió.** Los tres activos con señal son justamente los que subieron lo
bastante para cruzar sus medias, y en los tres el sistema entró tarde y salió con la
siguiente sacudida. PAXG es el caso más claro: aguantarlo habría dado +54 € y operarlo dio
−51 €, con 40 € de comisiones por medio.

## Conclusión

Sobre este histórico y con estas comisiones, el sistema de partida **no aporta valor**. No
significa que la idea del cruce de medias sea mala; significa que a esta escala de capital
y con un 1,9 % por operación, un sistema necesita acertar mucho y operar poco. Esa es la
cifra que había que conocer antes de operar con dinero, y es exactamente para lo que está
el simulador.

## Pendiente

- Probar el mismo sistema con menos operaciones, subiendo la ventana de la media rápida.
- Probar con capital mayor por operación, para que la comisión pese menos en proporción.
- Contrastar con un sistema descrito por un analista, cuando se traduzca a reglas.
