## ADDED Requirements

### Requirement: Lo que una hoja ya aporta no se cuenta dos veces

Cuando un mismo hecho aparece en dos hojas del mismo fichero, el sistema DEBE importarlo una sola vez, por la hoja que lo describe mejor, y descartarlo en la otra. La hoja que describe mejor una compra o una venta es la que trae cantidad y precio, porque es la única con la que se pueden construir lotes.

Lo descartado por esta razón, y las filas de totales o resúmenes que un informe añade al final, DEBEN contarse entre los registros sin efecto financiero, de modo que el usuario vea cuántos fueron.

#### Scenario: La operación está en dos hojas

- **WHEN** una compra aparece en la hoja de operaciones con su cantidad y su precio, y en la hoja de efectivo sólo como un importe
- **THEN** se importa una sola vez, con su cantidad y su precio, y el dinero no se cuenta dos veces

#### Scenario: Fila de totales

- **WHEN** una hoja termina con una fila de totales del informe
- **THEN** no se importa como movimiento y se cuenta entre los registros sin efecto financiero

#### Scenario: Lo que sólo está en la hoja de efectivo

- **WHEN** la hoja de efectivo trae ingresos, intereses, retenciones o comisiones que no aparecen en ninguna otra hoja
- **THEN** se importan todos, porque nadie más los aporta
