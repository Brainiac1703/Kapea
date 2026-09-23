## ADDED Requirements

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

## MODIFIED Requirements

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
