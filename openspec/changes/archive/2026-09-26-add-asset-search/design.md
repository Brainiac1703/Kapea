## Context

Ver proposal.md para el porqué. Lo que condiciona el diseño es lo que ya existe:

- `CoinGeckoMarketPriceProvider.CoinIds` traduce símbolo a identificador de CoinGecko con un diccionario escrito a mano. `CoinGeckoPriceHistoryProvider` devuelve una serie vacía si el símbolo no está ahí, así que una moneda fuera de la lista no tiene precios ni señales.
- `YahooSymbols.ToYahoo` traduce en un solo sentido, de canónico a Yahoo: quita el sufijo `.US`, que Yahoo no usa, y declara excepciones una a una. Su comentario avisa de por qué no hay una regla más lista: «una regla equivocada devuelve el precio de otro valor, y eso no falla, solo miente».
- El símbolo canónico del catálogo es el de XTB (`NOW.US`, `CSPX.UK`), el catálogo es global y su índice único es símbolo más clase.
- `WatchlistService.AddAsync` resuelve por símbolo y clase, crea si no existe, y comprueba la cobertura con el proveedor de histórico para avisar al usuario.
- Los dos proveedores son clientes HTTP configurados con su URL base, y los dos ofrecen búsqueda gratuita.

## Goals / Non-Goals

**Goals:**

- Que se pueda añadir un activo sabiendo su nombre y no su símbolo.
- Que una cripto fuera de la lista escrita a mano pueda tener precios.
- Que elegir un resultado no duplique un activo que ya existe.
- Que un proveedor caído no deje la pantalla sin funcionar.

**Non-Goals:**

- Cambiar cómo se resuelven los símbolos al importar movimientos. Ahí manda el bróker y así sigue.
- Añadir proveedores nuevos.
- Reescribir la lista de identificadores que ya existe. Se queda como respaldo para lo que entró importando.

## Decisions

### El identificador del proveedor pasa a ser un dato del activo

Un campo más en el catálogo, junto al símbolo canónico. Cuando está, manda sobre cualquier traducción; cuando no, se resuelve como hasta ahora.

Esto es lo que convierte la búsqueda en algo más que comodidad: el identificador deja de adivinarse y pasa a venir de lo que el usuario eligió viendo el nombre completo y el mercado. Una moneda que hoy no tiene precios porque no está en la lista escrita a mano, los tendrá.

*Lo que no cambia:* la lista sigue ahí para los activos que entraron importando, que no tienen identificador. Quitarla dejaría sin precios lo que hoy funciona.

*Alternativa descartada:* sustituir la lista por una descarga del catálogo entero del proveedor. Son miles de monedas, muchas con el mismo símbolo, y elegir entre ellas sin que nadie mire es exactamente lo que la lista escrita a mano evita.

### Un resultado se compara con el catálogo por la traducción que ya existe

Antes de crear nada, cada activo del catálogo de esa clase se traduce al símbolo del proveedor con la misma función que se usa para pedir precios, y se compara con el del resultado. Si coincide, es el mismo activo y se usa el que hay.

Así, buscar «ServiceNow» y elegir `NOW` encuentra el `NOW.US` que entró importando XTB, en lugar de crear un segundo activo con su propia cola de lotes.

El catálogo tiene decenas de activos, no miles, así que la comparación se hace en memoria y no necesita índice ni columna nueva.

*Lo que esto deja abierto:* dos criptomonedas distintas que compartan símbolo se reconocerían como la misma. Es el caso que la lista escrita a mano ya avisaba, y no se resuelve mirando símbolos. Cuando ocurra, el identificador guardado permitirá distinguirlas; hasta entonces, el símbolo es lo único que hay.

### Buscar es preguntar a los dos proveedores a la vez

Una sola caja, sin elegir tipo. Se pregunta a los dos y se juntan los resultados, cada uno diciendo qué es y dónde cotiza.

Un proveedor que falla no vacía la pantalla: se devuelve lo del otro y se dice que la búsqueda está incompleta. Es la misma regla que ya sigue la descarga de precios, donde un proveedor caído no impide usar los demás.

### Escribir el símbolo sigue funcionando

Quien ya sabe que quiere ADA no tiene por qué buscar. La vía de añadir por símbolo y clase se queda tal cual, y la búsqueda se suma a ella.

Además es la salida cuando la búsqueda no encuentra nada: el activo se puede seguir igualmente, que es lo que hoy ya se hace con los que ningún proveedor cubre.

## Risks / Trade-offs

- **Dos peticiones externas por búsqueda**, con su latencia y su cuota. → Se hacen en paralelo, sólo cuando el usuario busca, y el resultado no se guarda: es una lista que se mira una vez.
- **Los proveedores de búsqueda pueden limitar peticiones.** → Un rechazo se trata como un proveedor caído: se enseña lo que haya y se dice que falta.
- **Un símbolo elegido mal trae precios de otro activo.** → Por eso el resultado enseña el nombre completo y el mercado antes de elegir, y por eso se guarda el identificador en lugar de volver a deducirlo cada vez.
- **El identificador puede quedar obsoleto** si el proveedor lo cambia. → Cuando deje de dar precios, el activo aparecerá sin precio en la lista de seguimiento, que es donde el usuario puede volver a buscarlo.

## Migration Plan

El identificador es una columna nueva y opcional: los activos existentes se quedan sin ella y siguen resolviéndose por su símbolo. Nada que convertir.

Revertir es dejar de mirar la columna.

## Open Questions

Ninguna.
