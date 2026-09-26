## ADDED Requirements

### Requirement: Una idea puede hablar de algo que no se tiene

El usuario DEBE poder registrar una idea sobre cualquier activo, tenga posición o no. Registrarla DEBE poner ese activo en seguimiento, de modo que a partir de ese momento tenga precio y pueda evaluarse.

#### Scenario: Idea sobre un activo nuevo

- **WHEN** el usuario anota una idea sobre un activo que no seguía
- **THEN** la idea queda registrada y el activo pasa a estar en seguimiento

#### Scenario: Idea sobre algo que ya se sigue

- **WHEN** el usuario anota una idea sobre un activo que ya estaba en la lista
- **THEN** la idea queda registrada y la lista no se duplica
