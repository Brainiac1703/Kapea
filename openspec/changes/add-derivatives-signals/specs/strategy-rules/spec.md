## MODIFIED Requirements

### Requirement: Reglas expresables y comprobables

Una regla DEBE expresarse como una condición sobre indicadores, precio, posición o magnitudes de derivados, combinable con otras por conjunción o disyunción. Un sistema con una regla que el motor no sepa evaluar NO PUEDE guardarse.

Una regla que use una magnitud de derivados NO PUEDE guardarse para un activo sin contrato perpetuo asociado: el sistema DEBE decirlo al guardarla, en lugar de aceptarla y no emitir nunca.

#### Scenario: Regla sobre un cruce de medias

- **WHEN** se declara que la entrada ocurre cuando la media de cincuenta días supera a la de doscientos
- **THEN** el sistema la acepta y la puede evaluar

#### Scenario: Regla con un indicador desconocido

- **WHEN** se declara una regla sobre un indicador que el motor no calcula
- **THEN** el sistema la rechaza al guardarla y dice cuál es

#### Scenario: Regla sobre el tipo de financiación

- **WHEN** se declara que la entrada ocurre cuando el tipo de financiación cae por debajo de un umbral
- **THEN** el sistema la acepta y la puede evaluar sobre los activos con contrato perpetuo

#### Scenario: Regla de derivados sobre un activo sin perpetuo

- **WHEN** se declara una regla de derivados para un activo que no cotiza como perpetuo
- **THEN** el sistema la rechaza al guardarla y dice por qué
