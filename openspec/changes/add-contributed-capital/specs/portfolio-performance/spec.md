## ADDED Requirements

### Requirement: Dinero aportado

El sistema DEBE decir cuánto dinero ha entrado en las plataformas y cuánto ha salido de ellas desde el principio, y la diferencia entre ambos: lo aportado neto. Sólo cuenta el dinero, no los activos. El movimiento que forma parte de un traspaso entre cuentas del propio usuario NO DEBE contar, porque ese dinero ya estaba dentro, y un movimiento anulado tampoco.

Las cantidades en una divisa distinta del euro DEBEN valorarse con el tipo de cambio congelado en el movimiento, como el resto del cálculo.

#### Scenario: Aportado neto

- **WHEN** el usuario ha ingresado cuatro mil ochocientos cincuenta euros y retirado seiscientos quince
- **THEN** el sistema dice que ha aportado cuatro mil doscientos treinta y cinco, y muestra por separado lo ingresado y lo retirado

#### Scenario: Traspaso entre cuentas propias

- **WHEN** el usuario mueve dinero de una de sus cuentas a otra
- **THEN** ni la salida ni la entrada alteran lo aportado

#### Scenario: Aportación en otra divisa

- **WHEN** se ingresa dinero en una divisa distinta del euro
- **THEN** cuenta por su valor en euros al tipo congelado en el movimiento, y recalcular no cambia la cifra

#### Scenario: Movimiento anulado

- **WHEN** el usuario anula un ingreso
- **THEN** deja de contar en lo aportado

### Requirement: Lo aportado frente a lo que hay

El sistema DEBE comparar el patrimonio actual con lo aportado neto y dar la diferencia en euros y en porcentaje sobre lo aportado. Esta comparación NO ES la rentabilidad ponderada por dinero: no pondera cuándo entró cada euro, dice cuánto se puso y cuánto hay.

Cuando la comparación no puede ser fiel, el sistema DEBE decirlo en lugar de dar una cifra que engaña. NO DEBE darse un porcentaje si no se ha aportado nada.

#### Scenario: Diferencia con lo aportado

- **WHEN** el usuario ha aportado cuatro mil doscientos treinta y cinco euros y su patrimonio es de tres mil cuatrocientos setenta y tres
- **THEN** el sistema dice que pierde setecientos sesenta y dos euros, un dieciocho por ciento de lo aportado

#### Scenario: Ha entrado un activo desde fuera

- **WHEN** un activo entra en una cuenta sin que ese dinero haya pasado por la plataforma
- **THEN** el sistema advierte de que lo aportado se queda corto, porque esa entrada no es una aportación en dinero

#### Scenario: Sin ninguna aportación

- **WHEN** no consta ningún ingreso
- **THEN** el sistema no da porcentaje, y dice que no hay nada aportado con lo que comparar

#### Scenario: Patrimonio incompleto

- **WHEN** faltan precios o el efectivo está incompleto
- **THEN** la comparación se presenta con la misma advertencia que ya acompaña al patrimonio
