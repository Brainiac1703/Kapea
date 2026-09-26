## ADDED Requirements

### Requirement: Buscar el contrato perpetuo de un activo

Una fuente de derivados DEBE poder decir si un activo cotiza en ella como contrato perpetuo y, si lo hace, con qué identificador y con qué factor de multiplicación sobre la unidad del activo.

La correspondencia NO DEBE deducirse del símbolo cuando ya hay una guardada: dos activos distintos pueden compartir símbolo, y acertar por parecido traería el dato de otro sin que nada fallara. Es el mismo criterio que ya rige para el identificador del proveedor de precios.

#### Scenario: Activo que cotiza como perpetuo

- **WHEN** se pregunta a una fuente por un activo que cotiza en ella
- **THEN** devuelve el identificador del contrato y su factor de multiplicación

#### Scenario: Activo que no cotiza

- **WHEN** el activo no cotiza como perpetuo en esa fuente
- **THEN** la fuente lo dice, y las demás se consultan igual

#### Scenario: Correspondencia ya guardada

- **WHEN** un activo ya tiene contrato guardado para una fuente
- **THEN** el dato se pide con ese contrato y no se vuelve a deducir del símbolo
