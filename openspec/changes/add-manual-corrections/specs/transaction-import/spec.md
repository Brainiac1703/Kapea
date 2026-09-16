## ADDED Requirements

### Requirement: Un movimiento anulado no vuelve al reimportar

La huella de un movimiento anulado DEBE seguir contando como existente en la deduplicación. Reimportar un fichero, sincronizar una cuenta o releer su histórico completo NO DEBE volver a crear un movimiento que el usuario ha anulado.

#### Scenario: Releer el histórico tras anular

- **WHEN** el usuario anula un movimiento de una cuenta con API y después relee todo su histórico
- **THEN** ese movimiento se cuenta como duplicado, no se crea de nuevo y sigue anulado

#### Scenario: Reimportar un fichero tras anular

- **WHEN** el usuario anula un movimiento importado por fichero y vuelve a subir el mismo fichero
- **THEN** la vista previa lo cuenta como duplicado y no lo importaría

### Requirement: Coincidencias con movimientos manuales al importar

Un movimiento manual no tiene huella de origen, así que la deduplicación no puede reconocerlo. La vista previa de una importación DEBE señalar cada registro que coincide con un movimiento manual vigente de la misma cuenta en tipo, activo, cantidad y día, sin descartarlo por sí sola, para que el usuario decida si borra el manual o descarta la importación. Como una sincronización por API no tiene vista previa, *Por revisar* DEBE listar además cada movimiento manual vigente que coincide así con uno importado, hasta que el usuario borre uno de los dos o marque la coincidencia como correcta.

#### Scenario: Importar lo que ya se apuntó a mano

- **WHEN** el usuario apuntó a mano una compra y después importa el fichero de la plataforma que la incluye
- **THEN** la vista previa marca ese registro como posible duplicado de un movimiento manual y enseña cuál

#### Scenario: Sin coincidencias

- **WHEN** ningún registro del fichero coincide con un movimiento manual de la cuenta
- **THEN** la vista previa no muestra ninguna marca de coincidencia

#### Scenario: Llega por API lo que se apuntó a mano

- **WHEN** el usuario apuntó a mano una compra en su cuenta de Kraken y la siguiente sincronización importa esa misma compra
- **THEN** *Por revisar* muestra la pareja como posible duplicado y cuenta como pendiente hasta que el usuario borre el manual o marque que son distintos
