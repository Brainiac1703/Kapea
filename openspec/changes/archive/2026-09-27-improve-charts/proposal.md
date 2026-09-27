## Why

Las gráficas de Kapea enseñan una línea de cierres y poco más. El usuario lo resumió así: «la visualización es un desastre y la información mostrada también». Lo compara con Bit2Me, XTB y la aplicación de Bolsa de Apple, que usa para analizar el mercado y que es la que más le gusta.

Tres cosas le faltan, y son de naturaleza distinta:

**No se puede elegir el periodo.** Hoy se salta de un año a todo. Las aplicaciones que cita ofrecen una escala: Bit2Me va de una hora a doce meses y todo; la de Apple, de un día a diez años, pasando por año en curso y dos años. Sin escalonado, mirar los últimos tres meses o lo que va de año es imposible.

**No se ve el recorrido de cada día, sólo dónde cerró.** Kapea guarda un único número por activo y día. Sin apertura, máximo y mínimo no hay velas, no hay rango diario, y no se puede ver que un día se movió un ocho por ciento y acabó plano. El usuario pidió expresamente «dibujar líneas pintando los mínimos, máximos».

**No hay cifras junto a la gráfica.** La de Apple pone debajo un bloque con apertura, máximo, mínimo y el rango de 52 semanas. Kapea no da ninguna, así que la gráfica hay que interpretarla a ojo.

## What Changes

- **Una escala de periodos de verdad**: 1 semana, 1 mes, 3 meses, 6 meses, lo que va de año, 1 año, 2 años, 5 años y todo. Sustituye al salto de un año a todo.
- **Agregación por semanas y meses.** Cinco años de días son puntos que se pisan; en velas semanales o mensuales se ve la forma. El periodo elegido sugiere la agregación, y se puede cambiar.
- **Se guarda apertura, máximo y mínimo además del cierre.** Es la condición para todo lo demás. Yahoo los da gratis, así que la renta variable y las criptomonedas grandes los tendrán; lo que sólo cubre CoinGecko seguirá con el cierre, y se dirá.
- **Velas, o línea con la banda del recorrido**, según lo que el activo tenga y el periodo que se mire.
- **Un cursor que lee la serie**: al recorrer la gráfica se ve el valor de ese día y su fecha, como en la aplicación de Apple.
- **Un bloque de cifras bajo la gráfica**: apertura, máximo y mínimo del día, rango del periodo elegido, variación en ese periodo, y máximo y mínimo de 52 semanas.
- **Bandas de dispersión**, etiquetadas por lo que son. El usuario pidió «probabilidades de hacia dónde va el valor». Eso no existe y no se va a dibujar. Lo que sí se puede enseñar es cuánto se ha movido históricamente: las bandas de volatilidad que Kapea ya calcula, y el recorrido medio diario. Describen el pasado, y así se rotulan.
- **Fuera de alcance**: los datos intradía, que el usuario quiere y quedan anotados para más adelante. Y lo que arregla `fix-closed-market-gaps`, que va antes.

## Capabilities

### Modified Capabilities

- `price-history`: la serie diaria pasa a guardar apertura, máximo y mínimo además del cierre, cuando el proveedor los dé.
- `portfolio-history`: la serie de un activo entrega el recorrido del día y puede agregarse por semanas o meses.
- `technical-indicators`: las bandas de volatilidad y el recorrido medio se pueden pedir para dibujarlos, dejando claro que describen el pasado.

## Impact

- **Datos**: tres columnas más en 53.614 filas, y volver a descargar para rellenarlas donde el proveedor las tenga. Es una pasada larga como la del último cambio, y ya se sabe cómo hacerla sin retrasar el precio de hoy.
- **Cobertura desigual**: POL, PEPE y TAO sólo los cubre CoinGecko y se quedarán sin recorrido diario, igual que se quedan con un año de historia. La pantalla tiene que llevarlo bien en lugar de parecer rota.
- **Interfaz**: es donde está casi todo el trabajo. El componente actual dibuja líneas en SVG y habrá que enseñarle velas, bandas y un cursor.
- **Lo que no cambia**: ninguna cifra de la cartera ni ningún resultado fiscal. Esto es cómo se enseña lo que ya hay, más tres columnas nuevas que nadie usa para valorar.
- **Anotado para más adelante**: intradía. No es que no exista gratis, es que depende del activo. Binance publica velas de un minuto de criptomonedas desde 2017 en ficheros descargables, la misma fuente que ya planifica `add-derivatives-ingestion`; en renta variable, Yahoo da un minuto sólo de los últimos días y una hora de un par de años. Habrá que comprobarlo al abordarlo.
