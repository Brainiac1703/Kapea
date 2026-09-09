## MODIFIED Requirements

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
