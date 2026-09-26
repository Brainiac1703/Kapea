## MODIFIED Requirements

### Requirement: Alcance del histórico

La serie de un activo en seguimiento DEBE cubrir desde el día más antiguo que necesite para ser útil hasta hoy: como mínimo, desde su primera adquisición cuando la haya, y en todo caso lo bastante atrás para que los sistemas declarados puedan evaluarse sobre él con su ventana completa. Un activo que no está en seguimiento no necesita serie.

#### Scenario: Primera descarga de un activo

- **WHEN** un activo entra en la cartera con una adquisición de hace dos años
- **THEN** el sistema descarga su precio diario desde esa fecha

#### Scenario: Activo comprado hoy

- **WHEN** un activo se adquiere por primera vez hoy
- **THEN** el sistema descarga lo que sus sistemas necesiten para poder evaluarlo, y no sólo el precio de hoy

#### Scenario: Activo que nunca se ha tenido

- **WHEN** el usuario añade al seguimiento un activo que nunca ha comprado
- **THEN** el sistema descarga su serie hacia atrás lo suficiente para evaluar sobre él los sistemas declarados

#### Scenario: Activo que deja de seguirse

- **WHEN** el usuario quita un activo del seguimiento
- **THEN** su serie deja de actualizarse, y la ya descargada se conserva
