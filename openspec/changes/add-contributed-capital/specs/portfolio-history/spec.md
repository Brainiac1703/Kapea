## MODIFIED Requirements

### Requirement: Aportaciones separadas del rendimiento

La serie DEBE distinguir lo que cambió por dinero aportado o retirado de lo que cambió por precio. Un ingreso seguido de una compra NO PUEDE leerse como una ganancia.

Lo que cuenta como aportación DEBE ser lo mismo aquí y en el resumen de la cartera: sólo el dinero que entra o sale de la plataforma. Un movimiento entre cuentas del propio usuario NO DEBE contar, porque ese dinero ya estaba dentro, ni tampoco la entrada de un activo, que no es dinero puesto.

#### Scenario: Ingreso y compra el mismo día

- **WHEN** el usuario ingresa mil euros y compra con ellos
- **THEN** el valor de la cartera sube mil euros y el rendimiento del día no se altera por esa operación

#### Scenario: Dinero movido entre cuentas propias

- **WHEN** el usuario traspasa dinero de una de sus cuentas a otra y el traspaso está confirmado
- **THEN** la serie no registra ninguna aportación ni ninguna retirada por ese movimiento

#### Scenario: Activo que llega de fuera

- **WHEN** entra un activo en una cuenta sin que su dinero haya pasado por la plataforma
- **THEN** no se registra como aportación, porque no es dinero puesto
