## Context

Ver proposal.md para el porqué. Lo que condiciona el diseño es lo que ya existe:

- `Bit2MeImportAdapter` ya resuelve la permuta: emite dos `ImportRecord`, una venta y una compra, con `settledInCash: false` para que no muevan la caja, y `CashEffect` ya respeta esa marca. Kraken es el que no lo hace.
- `KrakenImportAdapter.InstantPurchases` agrupa los apuntes por referencia y exige que una de las dos patas sea dinero (`KrakenSymbols.IsFiat`). Sin esa pata se rinde y cada apunte cae en `MapLedgerType`, donde `SPEND` y `RECEIVE` son `Transfer`.
- El libro de Kraken no valora nada que no sea dinero. El adaptador ya lo sabe y por eso pone importe cero, con un comentario que explica el destrozo que causó lo contrario.
- `IPriceHistory` guarda el precio de cierre diario en euros por activo y fecha, y `IPriceHistoryProvider` lo descarga. Se usa para las gráficas.
- El cálculo ya produce `CalculationInconsistency`, el contrato `PortfolioResponse` ya tiene el campo `Inconsistencies` y `IsComplete` ya lo mira. Lo único que falta es que alguien lo rellene: hoy `PortfolioQueries` pasa `[]`.
- Las proyecciones se reemplazan por activo en cada recálculo (`IPortfolioProjectionStore.ReplaceAsync`), que recibe el `AssetCalculationResult` entero, incoherencias incluidas, y guarda todo menos esas.
- La lista de movimientos ya filtra por procedencia y por estado, y ya traduce el tipo en la tabla con `TransactionType_*`.

## Goals / Non-Goals

**Goals:**

- Que una permuta hecha en Kraken cuente como lo que es: sale un activo, entra otro, no se mueve dinero.
- Que las permutas ya guardadas queden corregidas sin que el usuario rehaga nada a mano.
- Que ninguna cifra de la cartera se presente como completa cuando el cálculo ha descartado un movimiento.
- Que una cifra estimada por Kapea se distinga de una tomada del origen.

**Non-Goals:**

- Adivinar el precio del instante exacto de la permuta. El cierre del día es la aproximación acordada, y por eso queda marcada como estimación corregible.
- Rehacer la valoración de los movimientos que el origen sí valora.
- Un sistema general de avisos de calidad de datos. Se muestran las incoherencias que el cálculo ya produce, nada más.

## Decisions

### El adaptador reconoce la permuta; la valoración la hace el motor

`InstantPurchases` pasa a tratar dos casos: con pata en dinero, compra o venta como ahora; sin ella y con las dos patas siendo activos, las dos patas de una permuta. El adaptador no valora, porque no puede: no tiene acceso al histórico de precios ni debe tenerlo. Marca el registro como pendiente de valorar y el `ImportPipeline`, que ya tiene los puertos de aplicación, lo valora.

El motivo es que la valoración por precio de cierre no es un asunto de Kraken. Cualquier adaptador que reciba un movimiento sin importe se beneficia, y el requisito quedó escrito en esos términos.

*Alternativa descartada:* inyectar el histórico de precios en el adaptador. Ataría la regla a una plataforma y obligaría a repetirla en la siguiente.

### Cada pata conserva su propio identificador de origen

Bit2Me tiene un identificador para el movimiento entero y por eso inventa los sufijos `:out` e `:in`. El libro de Kraken ya da un apunte por pata, cada uno con su identificador. Se conserva, que es lo que mantiene funcionando la deduplicación: releer el histórico reconoce las patas ya importadas y no las duplica.

*Consecuencia:* la marca de permuta no puede deducirse del identificador, como hizo la migración `SwapSettlement`. Va en el dato, que es donde debería haber estado.

### El importe estimado es una marca del movimiento, no un tipo aparte

Una columna booleana en `Transactions` con su migración, igual que `SettledInCash`. El movimiento sigue siendo una compra o una venta normal y entra en el cálculo como cualquier otra; lo único que cambia es que la aplicación dice de dónde salió la cifra y ofrece corregirla.

La corrección se apoya en lo que ya existe: corregir un movimiento importado en un paso, que ya guarda el motivo y recalcula. Al corregir el importe, la marca de estimado se retira.

*Alternativa descartada:* dejar la permuta pendiente de revisión hasta que el usuario la valore. Es lo que se hace cuando no hay precio, pero aplicarlo siempre convertiría cada cambio de cripto en una tarea manual, que es justo lo que el usuario descartó.

### Sin precio para la fecha, revisión; nunca una cifra inventada

Si el histórico no tiene el día y el proveedor tampoco lo da, el movimiento se importa pendiente de revisión. Es la misma regla que el resto del importador: el sistema prefiere admitir que no sabe algo a rellenarlo.

### Las incoherencias se guardan con la proyección

Una tabla de proyección más, reemplazada por activo en cada recálculo igual que los lotes y los resultados, y leída por `PortfolioQueries` para rellenar el campo que hoy va vacío. El texto que ve el usuario se compone en el cliente a partir del tipo, el activo, la fecha y la cantidad, con recursos localizados; el mensaje en español que hoy lleva el dominio no viaja a la pantalla.

*Alternativa descartada:* recalcular el FIFO en cada consulta de cartera para obtener las incoherencias al vuelo. Duplicaría el cálculo entero en la petición más frecuente de la aplicación, para un dato que casi siempre está vacío.

### Lo ya importado se borra en su propia migración

Los pasos a Earn ya guardados se borran con las mismas salvaguardas que las patas de
permuta —importados, sin tocar por el usuario y sin nada calculado colgando— pero en una
migración aparte.

La razón es práctica y no de estilo: la migración de las permutas ya está aplicada en la
base de desarrollo, y editar una migración ya aplicada deja el historial diciendo una
cosa y la base otra. Una migración por propósito también se lee mejor cuando haya que
entender, dentro de un año, por qué desaparecieron noventa y dos movimientos.

Aquí no hace falta releer nada después: a diferencia de las permutas, estos movimientos
no tienen que volver en otra forma. Simplemente dejan de existir.

### Las permutas mal importadas se borran y se vuelven a leer

Una migración localiza las patas afectadas —movimientos de tipo `Transfer` cuyo contenido original es un `spend` o un `receive` de Kraken y cuya referencia tiene la otra pata— y las borra. La siguiente relectura completa del histórico las reimporta ya como permutas y valoradas.

Es más seguro que reconstruirlas en SQL: la conversión necesita el precio del día, que una migración no puede pedir, y el camino de importación nuevo va a ejecutarse de todas formas. Borrarlas es seguro porque hoy no participan en nada: no crean lote, no consumen lote y no afectan a la caja.

Lo que esto obliga a vigilar: la migración no puede borrar un movimiento que el usuario haya anulado, corregido o emparejado a mano con uno manual. Esos se dejan como están y se cuentan en el registro de la migración.

### Los pasos a Earn se descartan al leer, no al calcular

Meter un activo en Earn y recuperarlo no llega a ser movimiento: el adaptador lo cuenta
como registro sin efecto financiero y no lo entrega. Ni el motor de importación ni el
cálculo tienen que saber que Earn existe.

Lo que obliga a descartar las dos caras, y no a emparejarlas, es de dónde salen: Bit2Me
expone el mismo paso en dos sitios —las transacciones del monedero y los movimientos de
Earn—, cada uno con su propio identificador. Emparejarlos exigiría adivinar que dos
identificadores distintos son el mismo hecho, por importe y por instante, que es
precisamente la clase de coincidencia que un día junta dos movimientos que no eran el
mismo. Descartando ambas caras por lo que son, el duplicado no puede aparecer.

En Kraken el paso son dos anotaciones del mismo activo canónico bajo una misma
referencia, una que entra y otra que sale, y se reconocen por ahí y no por el nombre del
subtipo: la plataforma ha usado varios a lo largo del tiempo y no hay motivo para
perseguirlos.

*Alternativa descartada:* importarlos y esconderlos con un filtro. Deja la lista llena de
apuntes que no significan nada y obliga a explicar en la pantalla algo que sobra en el
dato.

*Lo que esto no da:* cuánto hay dentro de Earn. Kapea no modela bolsillos dentro de una
cuenta, y añadirlos es otro trabajo; queda apuntado, no entra aquí.

### El filtro por tipo pasa a ser una lista

`TransactionQuery` cambia el tipo suelto por una colección, el endpoint acepta el parámetro repetido y la consulta filtra por pertenencia al conjunto; una colección vacía significa todos los tipos, como hoy el valor vacío. El desplegable pasa a selección múltiple y cada opción se nombra con la misma clave de recurso que la tabla, de modo que no hay dos traducciones del mismo tipo.

## Risks / Trade-offs

- **El cierre del día no es el precio del cambio** → La cifra queda marcada como estimada y el usuario puede corregirla. En las permutas del usuario hablamos de unos trece euros en total.
- **Descargar precios durante una importación la hace más lenta y dependiente de un proveedor** → Sólo se piden los días que faltan, y si el proveedor no responde el movimiento cae en revisión en lugar de fallar la importación entera.
- **Borrar movimientos en una migración** → Se acota a las patas de permuta de Kraken que ninguna otra cosa referencia, se excluyen las tocadas por el usuario y se deja constancia de cuántas se borraron. En producción se aplica con el mismo paso de migraciones que ya detiene el despliegue si falla.
- **Un histórico releído que no vuelva a traer esas permutas** dejaría al usuario sin esos movimientos → La relectura completa forma parte de las tareas y se comprueba activo por activo antes de dar el trabajo por bueno.
- **Cambiar la firma de la búsqueda de movimientos** afecta al cliente y a las pruebas de API → El parámetro repetido es compatible con una sola aparición, así que una llamada con un tipo sigue funcionando.

## Migration Plan

1. Se despliega el código nuevo y la migración, que añade la marca de estimado y borra las patas de permuta mal importadas.
2. Se relee el histórico completo de Kraken, que las reimporta como permutas valoradas.
3. Se recalcula la cartera: PAXG queda cerrado con su resultado realizado, y los activos de origen pierden las unidades que ya no se tienen.
4. Si algo saliera mal, revertir es restaurar la copia de la base y volver a la revisión anterior; el código nuevo no cambia ningún movimiento que ya estuviera bien.

## Open Questions

Ninguna. La valoración quedó decidida con el usuario: automática con el precio del día y corregible.
