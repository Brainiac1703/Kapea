# portfolio-domain Specification

## Purpose

Define el modelo de cartera de Kapea —activos, divisas, cuentas por plataforma, movimientos normalizados y lotes FIFO— y las invariantes que lo mantienen coherente y auditable, con independencia de la plataforma de la que provengan los datos.

## Requirements

### Requirement: Aislamiento por usuario

Toda entidad de cartera DEBE estar asociada a un `UserId`, y toda lectura o escritura DEBE quedar restringida al usuario autenticado.

El `UserId` DEBE corresponder a un usuario del registro y DEBE deducirse siempre de la sesión, nunca de un dato de entrada de la petición.

#### Scenario: Lectura restringida al propietario

- **WHEN** un usuario autenticado consulta cuentas, movimientos, lotes o posiciones
- **THEN** el sistema devuelve exclusivamente las entidades cuyo `UserId` coincide con el suyo

#### Scenario: Acceso a una entidad ajena

- **WHEN** un usuario solicita por identificador una entidad que pertenece a otro `UserId`
- **THEN** el sistema responde como si la entidad no existiera, sin revelar su existencia

#### Scenario: Datos creados por otro usuario en la misma instalación

- **WHEN** dos usuarios distintos usan la misma instalación de Kapea
- **THEN** ninguno de los dos ve rastro de las cuentas, movimientos ni resultados del otro

### Requirement: Catálogo de activos

El sistema DEBE mantener un catálogo de activos identificados de forma estable e independiente del nombre que use cada plataforma. Un activo DEBE declarar su clase (`Equity` o `Crypto`), su símbolo canónico y, cuando exista, un identificador externo estándar (ISIN para renta variable). Dos importaciones del mismo activo desde plataformas distintas DEBEN resolver al mismo activo del catálogo.

#### Scenario: Mismo activo desde dos plataformas

- **WHEN** se importa un movimiento de `BTC` desde Kraken y otro de `BTC` desde Bit2Me
- **THEN** ambos movimientos referencian el mismo activo del catálogo

#### Scenario: Símbolo desconocido

- **WHEN** una importación aporta un símbolo que no se puede resolver contra el catálogo
- **THEN** el sistema crea el activo como no verificado y marca el movimiento para revisión del usuario, sin bloquear el resto de la importación

#### Scenario: Precisión por clase de activo

- **WHEN** se registra una cantidad de un activo de clase `Crypto`
- **THEN** el sistema conserva al menos 8 decimales sin redondeo intermedio

### Requirement: Cuentas por plataforma

El usuario DEBE poder registrar una o varias cuentas, cada una asociada a una plataforma y a una divisa base. Todo movimiento DEBE pertenecer exactamente a una cuenta.

Las plataformas DEBEN ser datos y no un conjunto cerrado en el código. Cada plataforma DEBE declarar cómo se importa —por fichero o por API—, y el sistema DEBE usar esa declaración para decidir qué cuentas admiten credencial y cuáles admiten subida de fichero.

#### Scenario: Alta de cuenta

- **WHEN** el usuario registra una cuenta indicando plataforma, alias y divisa base
- **THEN** el sistema la crea y queda disponible como destino de importación

#### Scenario: Borrado de cuenta con movimientos

- **WHEN** el usuario intenta borrar una cuenta que tiene movimientos importados
- **THEN** el sistema rechaza la operación y explica que primero deben eliminarse o reasignarse sus movimientos

#### Scenario: Alta de una plataforma nueva

- **WHEN** se da de alta una plataforma que se importa por fichero
- **THEN** queda disponible al crear una cuenta sin haber modificado el código

#### Scenario: Credencial solo donde tiene sentido

- **WHEN** el usuario va a dar de alta una credencial
- **THEN** solo se ofrecen las cuentas cuya plataforma declara que se importa por API

### Requirement: Movimiento normalizado

Todo dato importado DEBE convertirse en un movimiento normalizado con, como mínimo: cuenta, tipo, activo (cuando aplique), cantidad, precio unitario, divisa de la operación, comisiones, fecha y hora con zona horaria, y una referencia inmutable al origen del que procede.

Los tipos de movimiento soportados DEBEN incluir `Buy`, `Sell`, `Deposit`, `Withdrawal`, `Transfer`, `Dividend`, `Fee`, `Interest`, `Reward`, `Split` y `Unknown`.

#### Scenario: Movimiento con todos los campos

- **WHEN** un adaptador entrega una compra con cuenta, activo, cantidad, precio, divisa, comisión y fecha
- **THEN** el sistema persiste el movimiento normalizado con la referencia a su origen

#### Scenario: Tipo no reconocido

- **WHEN** el origen aporta un concepto de operación que no encaja en ningún tipo soportado
- **THEN** el sistema registra el movimiento con tipo `Unknown`, lo excluye del cálculo de P&L y lo señala como pendiente de clasificar

#### Scenario: Fecha sin zona horaria

- **WHEN** el origen aporta una fecha sin zona horaria explícita
- **THEN** el sistema la interpreta en la zona declarada por el adaptador y almacena el instante en UTC junto con la zona de origen

### Requirement: Inmutabilidad del movimiento importado

Un movimiento importado NO DEBE modificarse en sus datos financieros (cantidad, precio, divisa, comisión, fecha). Las correcciones DEBEN expresarse como un ajuste manual con su propia trazabilidad, o eliminando y reimportando la ejecución de importación completa.

#### Scenario: Intento de edición directa

- **WHEN** se intenta alterar la cantidad o el precio de un movimiento ya importado
- **THEN** el sistema rechaza el cambio

#### Scenario: Corrección mediante ajuste

- **WHEN** el usuario registra un ajuste manual sobre una posición
- **THEN** el sistema lo persiste como movimiento propio, identificado como ajuste manual y con el motivo aportado por el usuario

### Requirement: Lote FIFO como entidad de primer nivel

Cada adquisición de un activo DEBE generar un lote con su cantidad original, cantidad restante, coste de adquisición en EUR y fecha de adquisición. Los lotes DEBEN ser consultables de forma independiente del cálculo que los consume.

#### Scenario: Compra genera lote

- **WHEN** se importa una compra de 10 unidades de un activo
- **THEN** el sistema crea un lote de 10 unidades con cantidad restante 10 y su coste en EUR

#### Scenario: La cantidad restante nunca es negativa

- **WHEN** un cálculo intentaría consumir de un lote más cantidad de la que le resta
- **THEN** el sistema rechaza la operación y notifica una inconsistencia de datos en lugar de dejar la cantidad restante en negativo

### Requirement: Trazabilidad de extremo a extremo

Toda cifra derivada (lote, posición, resultado) DEBE ser reconducible hasta los movimientos que la originan, y todo movimiento hasta la ejecución de importación y el registro de origen del que salió.

#### Scenario: Descomposición de un resultado

- **WHEN** el usuario consulta un resultado realizado
- **THEN** el sistema enumera los lotes consumidos y, para cada uno, los movimientos de compra y de venta que lo originan

#### Scenario: Origen de un movimiento

- **WHEN** el usuario consulta un movimiento importado
- **THEN** el sistema indica la ejecución de importación, la plataforma y el identificador o la fila del registro original

### Requirement: Registro manual de movimientos

El usuario DEBE poder registrar a mano un movimiento en cualquiera de sus cuentas indicando tipo, activo cuando proceda, cantidad, precio, importe, divisa, comisión, fecha y una nota opcional. El movimiento manual DEBE cumplir las mismas reglas de coherencia que uno importado y, en una divisa distinta del euro, DEBE congelar el tipo de cambio de su fecha. DEBE participar en lotes, posiciones, efectivo, evolución y resultados igual que un importado, y tras registrarlo todo ello DEBE recalcularse.

#### Scenario: Registrar una compra a mano

- **WHEN** el usuario registra sin nota una compra de 2 unidades de un activo en una cuenta de XTB
- **THEN** el sistema la guarda identificada como movimiento manual y la posición del activo incluye esas 2 unidades con su coste

#### Scenario: Movimiento manual incoherente

- **WHEN** el usuario registra una compra sin activo o con cantidad cero
- **THEN** el sistema la rechaza con el mismo motivo con que rechazaría un movimiento importado así

#### Scenario: Movimiento manual en otra divisa

- **WHEN** el usuario registra a mano una compra en dólares
- **THEN** el sistema congela el tipo de cambio de su fecha y la valora en euros con él en todos los cálculos

#### Scenario: Divisa sin tipo de cambio para la fecha

- **WHEN** el usuario registra a mano un movimiento en una divisa sin tipo de cambio disponible para su fecha
- **THEN** el sistema lo rechaza indicando que falta el tipo de cambio

#### Scenario: Cuenta ajena

- **WHEN** el usuario intenta registrar un movimiento en una cuenta que no es suya
- **THEN** el sistema responde como si la cuenta no existiera

### Requirement: Edición y borrado de un movimiento manual

El usuario DEBE poder editar cualquier dato de un movimiento manual y borrarlo. Al editar, si cambian la fecha o la divisa, el tipo de cambio DEBE volver a resolverse. Tras editar o borrar, lotes, posiciones y resultados DEBEN recalcularse. Editar y borrar de esta forma NO DEBE estar permitido sobre movimientos importados.

#### Scenario: Editar la cantidad

- **WHEN** el usuario cambia la cantidad de un movimiento manual
- **THEN** el sistema guarda el cambio y las cifras reflejan la cantidad nueva

#### Scenario: Borrar un movimiento manual

- **WHEN** el usuario borra un movimiento manual
- **THEN** el movimiento desaparece y las cifras vuelven a ser las que eran sin él

#### Scenario: Intento sobre un importado

- **WHEN** el usuario intenta editar o borrar un movimiento importado como si fuera manual
- **THEN** el sistema lo rechaza e indica que un importado se corrige o se anula

### Requirement: Anulación de un movimiento importado

El usuario DEBE poder anular un movimiento importado aportando un motivo. Un movimiento anulado NO DEBE participar en lotes, posiciones, efectivo, histórico de patrimonio, rendimiento ni resultados. La anulación NO DEBE borrar el movimiento ni alterar sus datos financieros, su registro de origen ni su huella. El sistema NO DEBE permitir anular un movimiento que participa en un traspaso confirmado, ni anular un movimiento manual o un ajuste, que se borran.

#### Scenario: Anular una venta duplicada

- **WHEN** el usuario anula con motivo una venta importada
- **THEN** la venta deja de consumir lotes y de generar resultado, y sigue en la lista de movimientos marcada como anulada con su motivo y su registro de origen

#### Scenario: Anulación sin motivo

- **WHEN** el usuario intenta anular un movimiento sin motivo
- **THEN** el sistema lo rechaza y dice que el motivo es obligatorio

#### Scenario: Movimiento en un traspaso confirmado

- **WHEN** el usuario intenta anular un movimiento que forma parte de un traspaso confirmado
- **THEN** el sistema lo rechaza e indica el traspaso que lo impide

#### Scenario: Anular un movimiento manual

- **WHEN** el usuario intenta anular un movimiento manual o un ajuste
- **THEN** el sistema lo rechaza e indica que ese movimiento se borra en lugar de anularse

#### Scenario: Movimiento sin clasificar

- **WHEN** el usuario anula un movimiento que estaba pendiente de clasificar
- **THEN** deja de contar como pendiente de revisar y las cifras dejan de declararse incompletas por su causa

### Requirement: Deshacer una anulación

El usuario DEBE poder deshacer la anulación de un movimiento. El movimiento DEBE volver a participar en el cálculo exactamente con los datos que tenía, y el sistema DEBE recalcular lotes, posiciones y resultados.

#### Scenario: Deshacer

- **WHEN** el usuario deshace la anulación de una venta
- **THEN** la venta vuelve a consumir lotes y a generar el mismo resultado que generaba antes de anularse

### Requirement: Corrección de un movimiento importado en un paso

El usuario DEBE poder corregir un movimiento importado partiendo de sus datos. Al confirmar, el sistema DEBE anular el importado con el motivo aportado y registrar un ajuste manual con los datos corregidos y ese mismo motivo, como una sola operación: o se hacen las dos cosas o ninguna. El ajuste NO DEBE poder editarse; DEBE poder borrarse, y borrarlo NO DEBE deshacer por sí solo la anulación del importado.

#### Scenario: Corregir la cantidad de una compra

- **WHEN** el usuario corrige la cantidad de una compra importada de 1,5 a 1,05 con un motivo
- **THEN** la compra importada queda anulada con ese motivo, existe un ajuste de compra de 1,05 con el mismo motivo, y la posición refleja 1,05

#### Scenario: Corrección sin motivo

- **WHEN** el usuario intenta corregir un importado sin motivo
- **THEN** el sistema lo rechaza y dice que el motivo es obligatorio

#### Scenario: Corrección con datos incoherentes

- **WHEN** el usuario corrige una compra importada dejando la cantidad a cero
- **THEN** el sistema rechaza la corrección y el importado no queda anulado

#### Scenario: Un ajuste no se edita

- **WHEN** el usuario intenta cambiar los datos de un ajuste
- **THEN** el sistema lo rechaza e indica que un ajuste se borra y se corrige de nuevo

### Requirement: Aviso de ejercicios afectados

Antes de confirmar el alta, la edición o el borrado de un movimiento manual, una corrección, una anulación, deshacerla o el borrado de un ajuste, el sistema DEBE avisar cuando alguna de las fechas implicadas pertenece a un ejercicio anterior al actual, nombrando el más antiguo e indicando que sus resultados y los de los ejercicios siguientes pueden cambiar.

#### Scenario: Movimiento manual en un ejercicio pasado

- **WHEN** el usuario va a registrar a mano una venta con fecha de un ejercicio anterior al actual
- **THEN** el sistema avisa, nombrando el ejercicio, y espera confirmación

#### Scenario: Edición que cruza de ejercicio

- **WHEN** el usuario mueve la fecha de un movimiento manual del ejercicio actual a uno anterior
- **THEN** el sistema avisa nombrando ese ejercicio anterior

#### Scenario: Ejercicio actual

- **WHEN** todas las fechas implicadas son del ejercicio actual
- **THEN** el sistema no muestra ese aviso

### Requirement: Procedencia visible de cada movimiento

Todo movimiento DEBE declarar su procedencia: importado por API, importado por fichero, registrado a mano o ajuste de corrección. Un movimiento registrado a mano DEBE conservar el instante en que se registró y el de su última edición, y NO DEBE poder hacerse pasar por importado. La procedencia DEBE mostrarse, con un texto comprensible y no con un código interno, en la lista de movimientos, en los últimos movimientos del inicio y en el detalle fiscal de cada transmisión y de cada lote que consume. La lista DEBE mostrar además la nota de los manuales, el motivo de ajustes y anulaciones, y distinguir los anulados de los vigentes; y DEBE permitir filtrar por procedencia, por estado y por tipo de movimiento.

El filtro por tipo DEBE nombrar cada tipo en el idioma de la aplicación, con el mismo texto con el que se muestra en la lista, y DEBE admitir varios tipos a la vez, devolviendo los movimientos de cualquiera de los seleccionados.

#### Scenario: Movimiento registrado a mano en la lista

- **WHEN** el usuario consulta la lista de movimientos
- **THEN** cada movimiento registrado a mano se identifica como tal, con su nota si la tiene, el instante en que se registró y, si se editó, el de su última edición

#### Scenario: Movimiento importado en la lista

- **WHEN** el usuario consulta un movimiento importado
- **THEN** se identifica como importado por API o por fichero, con la importación de la que procede

#### Scenario: Procedencia en el detalle fiscal

- **WHEN** el usuario consulta una transmisión de un ejercicio cuya venta o alguno de cuyos lotes procede de un movimiento registrado a mano
- **THEN** la venta y cada lote consumido indican su procedencia, de modo que se ve qué parte de la cifra se apoya en datos apuntados a mano

#### Scenario: Filtrar manuales

- **WHEN** el usuario filtra la lista por movimientos registrados a mano
- **THEN** ve sólo esos, cada uno con su nota si la tiene

#### Scenario: Filtrar anulados

- **WHEN** el usuario filtra la lista por movimientos anulados
- **THEN** ve sólo los anulados, cada uno con su motivo y el instante en que se anuló

#### Scenario: Tipos nombrados en el idioma de la aplicación

- **WHEN** el usuario abre el filtro por tipo de movimiento
- **THEN** cada tipo aparece con el mismo texto con el que se muestra en la lista, en el idioma de la aplicación

#### Scenario: Filtrar por varios tipos

- **WHEN** el usuario selecciona compras y ventas en el filtro por tipo
- **THEN** ve los movimientos de ambos tipos y ninguno de los demás

#### Scenario: Filtro por tipo vacío

- **WHEN** el usuario no selecciona ningún tipo
- **THEN** ve los movimientos de todos los tipos

### Requirement: Importe estimado distinguible y corregible

Un importe que el sistema ha estimado, en lugar de tomarlo del origen, DEBE distinguirse de los demás allí donde se muestre el movimiento, con un texto comprensible que diga que es una estimación y de cuándo procede el precio usado. El usuario DEBE poder sustituirlo por el importe real; al hacerlo, el movimiento deja de estar marcado como estimado y el cálculo se rehace. Un importe estimado NO DEBE impedir que el movimiento participe en el cálculo.

#### Scenario: Movimiento con importe estimado en la lista

- **WHEN** el usuario consulta un movimiento cuyo importe se estimó al importarlo
- **THEN** ve que ese importe es una estimación y a qué fecha corresponde el precio con el que se calculó

#### Scenario: Corrección del importe estimado

- **WHEN** el usuario sustituye el importe estimado por el real
- **THEN** el movimiento deja de estar marcado como estimado, conserva la corrección como tal y las posiciones y los resultados se recalculan con el nuevo importe

#### Scenario: El estimado sí cuenta

- **WHEN** existen movimientos con importe estimado
- **THEN** participan en el cálculo de posiciones y resultados como cualquier otro

### Requirement: Lo que suma y lo que resta en la lista de movimientos

La lista de movimientos DEBE decir, en cada uno, si suma a la cartera o resta de ella. El signo DEBE ir delante del importe y el color sólo DEBE reforzarlo, de modo que se lea igual sin distinguir colores.

Suma lo que entra sin pagarlo o devuelve dinero: una venta, un ingreso, un dividendo, un interés y un rendimiento cobrado, aunque llegue en unidades del propio activo y no mueva ningún euro. Resta lo que cuesta dinero: una compra, una retirada y una comisión.

Un intercambio NO DEBE mostrarse con signo ni con color: una permuta cambia una cosa por otra y un traspaso lleva algo de una cuenta propia a otra, así que ni suman ni restan. Fingir una dirección que no tienen es peor que no decir nada.

La comisión DEBE mostrarse con el mismo criterio, porque siempre resta.

#### Scenario: Una venta suma

- **WHEN** el usuario mira una venta en la lista
- **THEN** su importe aparece con signo positivo y el color que el resto de la aplicación usa para lo que suma

#### Scenario: Una compra resta

- **WHEN** el usuario mira una compra
- **THEN** su importe aparece con signo negativo y el color de lo que resta

#### Scenario: La comisión

- **WHEN** un movimiento tiene comisión
- **THEN** se muestra con signo negativo y el color de lo que resta

#### Scenario: Una recompensa cobrada en unidades

- **WHEN** el usuario mira una recompensa que le pagaron en unidades del propio activo
- **THEN** su importe aparece como lo que suma, aunque ningún euro se haya movido: se la regalaron y su cartera vale más

#### Scenario: Un intercambio

- **WHEN** el usuario mira una permuta o un traspaso entre sus cuentas
- **THEN** su importe se muestra sin signo ni color, porque ni sumó ni restó

### Requirement: Un activo recuerda cómo lo conoce su proveedor

Un activo DEBE poder guardar el identificador con el que lo conoce el proveedor que da sus precios. Ese identificador NO DEBE confundirse con el símbolo canónico: el símbolo identifica el activo dentro de Kapea, y el identificador sólo sirve para pedirle datos a ese proveedor.

Un activo sin identificador guardado DEBE seguir funcionando: es el caso de todo lo que entró importando movimientos.

#### Scenario: Activo dado de alta desde una búsqueda

- **WHEN** el usuario añade un activo eligiéndolo de una búsqueda
- **THEN** el activo guarda el identificador de ese proveedor junto a su símbolo canónico

#### Scenario: Activo importado

- **WHEN** un activo entra por una importación de movimientos
- **THEN** se guarda sin identificador de proveedor y sus precios se resuelven por su símbolo
