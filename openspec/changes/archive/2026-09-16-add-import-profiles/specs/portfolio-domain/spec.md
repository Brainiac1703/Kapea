## MODIFIED Requirements

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
