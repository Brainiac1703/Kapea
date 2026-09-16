## ADDED Requirements

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

Todo movimiento DEBE declarar su procedencia: importado por API, importado por fichero, registrado a mano o ajuste de corrección. Un movimiento registrado a mano DEBE conservar el instante en que se registró y el de su última edición, y NO DEBE poder hacerse pasar por importado. La procedencia DEBE mostrarse, con un texto comprensible y no con un código interno, en la lista de movimientos, en los últimos movimientos del inicio y en el detalle fiscal de cada transmisión y de cada lote que consume. La lista DEBE mostrar además la nota de los manuales, el motivo de ajustes y anulaciones, y distinguir los anulados de los vigentes; y DEBE permitir filtrar por procedencia y por estado.

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
