## Context

Ver proposal.md para el porqué. Lo que condiciona el diseño es lo que ya existe:

- `TabularReader.ReadExcel` abre el libro con ClosedXML, toma `Worksheets.First()` y usa `rows[0]` como cabecera. No contempla ni varias hojas ni preámbulo.
- `FileInspector` lee el fichero una vez, pide a `ProfileMatching.Match` **un** perfil para esas cabeceras y devuelve una muestra para la vista previa. `ProfileFileImportAdapter` repite esa lectura al importar.
- Un perfil ya declara sus cabeceras reconocidas, el delimitador, las convenciones de número y fecha, el mapeo de columnas y la traducción de conceptos, y se versiona: cada movimiento importado guarda con qué perfil y con qué versión entró.
- `BuiltInProfiles` siembra los perfiles de serie y el sembrador sólo da de alta los que faltan; nunca pisa los que ya están, como dejó escrito la migración `AmountNetOfFee`.
- `ImportReadResult` ya distingue registros normalizados, rechazados y descartados por no tener efecto financiero, y el recuento de los últimos se enseña al terminar.
- Los conceptos que un perfil no traduce entran como `Unknown` y quedan en revisión, sin bloquear el resto.

El fichero que motiva el cambio: tres hojas, cuatro filas de metadatos delante de cada tabla, y en la hoja de efectivo una fila final `Total` sin fecha ni identificador.

## Goals / Non-Goals

**Goals:**

- Que un informe con pestañas y cabecera administrativa se importe sin preparar el fichero a mano.
- Que la solución sirva para el siguiente bróker que exporte así, no sólo para XTB.
- Que el dinero no se cuente dos veces cuando el mismo hecho aparece en dos hojas.
- Que el usuario vea de qué hoja sale cada cosa.

**Non-Goals:**

- Adivinar la estructura de un fichero sin perfil. La tabla se localiza porque un perfil reconoce sus cabeceras; sin perfil, se sigue ofreciendo crear uno.
- Importar la hoja de posiciones abiertas. Es un resumen de dos cifras, no movimientos.
- Reconstruir las posiciones que XTB dejó abiertas en el momento del informe. En este caso no hay ninguna.

## Decisions

### La tabla se busca, no se supone

El lector deja de devolver una tabla y pasa a devolver las hojas del fichero. Para cada hoja, la fila de cabeceras es la primera que un perfil de la plataforma reconoce; las anteriores son preámbulo y se descartan.

Localizar la cabecera con los perfiles, y no con una heurística de «la primera fila con muchas celdas no vacías», tiene una ventaja que se nota el día que falla: el criterio es un dato que el usuario puede ver y corregir, no una adivinanza enterrada en el código.

*Consecuencia:* el lector necesita saber qué cabeceras busca, así que la búsqueda vive donde ya está el emparejamiento y no dentro del lector. El lector entrega hojas y filas; quien empareja decide dónde empieza cada tabla.

*Alternativa descartada:* que el perfil declare cuántas filas saltar. Un número fijo se rompe en cuanto el informe añade una línea, y obliga al usuario a contar filas de un fichero que no ha abierto.

### Una subida, varias hojas, varios perfiles

`FileInspector` pasa a devolver una lista de hojas reconocidas, cada una con su perfil, y la importación las recorre todas. Es el cambio que permite subir el fichero una sola vez, que es lo que el usuario pidió.

Una hoja que ningún perfil reconoce no es un error: se ignora y se dice. Un libro entero sin ninguna hoja reconocida sí lo es, y entonces se ofrece crear un perfil como hasta ahora.

### Una subida es una importación, aunque lea dos hojas

Una sola vista previa, una confirmación y una línea en el historial. El perfil deja de
ser sólo del conjunto y pasa a viajar con cada registro, de modo que un movimiento sabe
con qué perfil y de qué hoja entró aunque su ejecución haya leído dos.

*Alternativa descartada:* una ejecución por hoja. Encajaba mejor con el modelo actual
—cada ejecución con su perfil y su recuento— pero obligaba al usuario a revisar dos
vistas previas y confirmar dos veces un fichero que subió una vez, que es justo lo que
se quería evitar.

### El perfil puede atarse a una hoja

Un campo más en la versión del perfil, con su migración. Sirve para desempatar cuando dos hojas del mismo libro tienen cabeceras parecidas, y para dejar dicho en el dato —no en el código— que «operaciones de efectivo» se lee de la hoja de efectivo.

Es opcional: un CSV no tiene hojas y un perfil sin hoja declarada sigue funcionando igual.

### Las compras y ventas salen de las posiciones cerradas

Es la única hoja con cantidad y precio, y sin cantidad no hay lote ni FIFO. En la hoja de efectivo, los conceptos de compra y de venta se declaran como sin efecto financiero: su dinero ya entra por la otra hoja.

Es el mismo criterio que se aplicó a Kraken y a Bit2Me, y por el mismo motivo: cuando una plataforma cuenta el mismo hecho dos veces, se elige la versión que más dice y se descarta la otra, contándola.

*Lo que esto deja fuera:* una posición que siguiera abierta no aparecería, porque no está en la hoja de cerradas. En este informe no hay ninguna; si un día la hay, la compra entrará como movimiento sin clasificar desde la hoja de efectivo y se verá en revisión, que es mejor que inventarla.

### La fila de totales se reconoce por lo que le falta

No tiene fecha ni identificador. Esa es la regla, y no su texto: un informe en otro idioma la llamaría de otra forma, pero seguirá sin fecha.

### Un perfil de serie que cambia llega a quien ya lo tenía

El sembrador daba de alta los perfiles que faltaban y no volvía a mirarlos. Con eso, una
base sembrada ayer se queda con la interpretación de ayer para siempre: se corrige cómo
se lee una plataforma, y el arreglo no llega nunca a quien más lo necesita, que es quien
ya la estaba importando mal.

Ahora compara la definición de serie con la guardada y, si cambió, añade una versión
nueva. Sólo sobre perfiles que siguen siendo de serie: si el usuario hizo suyo el suyo,
manda el suyo. Las versiones anteriores se conservan, así que un movimiento importado
hace meses sigue explicando con qué reglas entró.

Esto apareció importando de verdad: el perfil corregido no cambiaba nada porque la base
ya lo tenía en su forma anterior.

### El informe nuevo son perfiles nuevos, no una versión del antiguo

Un perfil reconoce un fichero sólo si están **todas** las cabeceras que declara. Cambiar
las del perfil de XTB, como se planteó al principio, dejaría ilegible la exportación
anterior que el usuario aún conserve: no es una migración de formato, son dos formatos
que conviven.

Así que el informe de varias hojas entra como dos perfiles nuevos, sembrados junto a los
de siempre. El emparejamiento ya sabe elegir entre varios, y cada fichero encuentra el
suyo sin que nadie tenga que decidir cuál usar.

Esto lo descubrió una prueba que ya existía: al cambiar las cabeceras, la lectura del
formato antiguo dejó de funcionar y lo dijo.

*Alternativa descartada:* que el sembrador añadiera una versión nueva al perfil
existente. Era la idea inicial, y habría roto lo que ya funcionaba a cambio de nada:
sembrar un perfil que falta es algo que el sembrador ya hace.

### La retención del interés entra como movimiento propio

XTB apunta el interés y su retención en dos filas, con identificadores distintos y sin más nexo que el periodo escrito en el comentario. Se importan como dos movimientos: el interés como rendimiento y la retención como gasto.

El efecto en el dinero es exacto y ninguna cifra se inventa. Lo que no se consigue así es presentar el rendimiento con su retención asociada, que es como se declara; emparejarlos exigiría leer el texto del comentario y confiar en él.

*Coste asumido, y por qué:* en este informe son cuarenta y siete céntimos de interés y doce de retención. Montar un emparejamiento por texto para eso sería un mecanismo frágil sosteniendo una cifra que no lo merece. Si algún día pesa, se empareja entonces.

## Risks / Trade-offs

- **Leer varias hojas multiplica lo que se importa de una subida** y un error de emparejamiento se nota más. → La vista previa sigue enseñando lo que entrará, ahora diciendo de qué hoja sale cada fila, y nada se confirma sin verla.
- **Un perfil que reconozca cabeceras demasiado genéricas** podría capturar una hoja que no le toca. → Para eso está la hoja declarada en el perfil, y el emparejamiento sigue prefiriendo el más reciente cuando hay empate.
- **Sembrar una versión nueva sobre un perfil de serie** cambia cómo se leerá el próximo fichero de esa plataforma. → Sólo se hace sobre perfiles que el usuario no ha tocado, y queda registrado como versión con su fecha.
- **Descartar las compras de la hoja de efectivo** deja fuera una posición que siguiera abierta. → Entra sin clasificar y se ve en revisión; no desaparece en silencio.

## Migration Plan

La hoja del perfil es una columna nueva, opcional y vacía para los existentes: nada que convertir. Los perfiles de XTB reciben una versión nueva al arrancar, y los movimientos ya importados siguen apuntando a la versión con la que entraron.

Revertir es volver a la revisión anterior: no se destruye ningún dato.

## Open Questions

Ninguna.
