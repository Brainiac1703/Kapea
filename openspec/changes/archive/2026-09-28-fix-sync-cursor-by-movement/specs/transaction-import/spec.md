## MODIFIED Requirements

### Requirement: Sincronización periódica de los adaptadores de API

Los adaptadores de origen API DEBEN sincronizarse de forma programada desde el servidor, importando desde la fecha del último movimiento que la cuenta ya tiene. Una sincronización solapada de la misma cuenta NO DEBE ejecutarse en paralelo.

El punto de partida NO DEBE ser el instante en que corrió la ejecución anterior. Una plataforma puede publicar un movimiento después de que ocurriera, o con el reloj desfasado: su fecha queda entonces por debajo de ese instante y aparece más tarde, así que partir de ahí lo deja fuera para siempre y sin rastro. Partir del último movimiento conocido hace que lo que llega tarde entre igual; lo que se relea de más se descarta por duplicado, que es un coste acotado frente a una pérdida definitiva.

Una cuenta sin ningún movimiento DEBE pedirse desde el principio del histórico.

#### Scenario: Sincronización incremental

- **WHEN** se dispara la sincronización programada de una cuenta ya sincronizada antes
- **THEN** el sistema solicita a la plataforma los movimientos posteriores al último que ya tiene

#### Scenario: Movimiento publicado con retraso

- **WHEN** la plataforma entrega un movimiento cuya fecha es anterior a la hora en que corrió la ejecución previa
- **THEN** la siguiente sincronización lo importa igualmente, en lugar de dejarlo fuera para siempre

#### Scenario: Cuenta sin movimientos

- **WHEN** se sincroniza una cuenta que no tiene ningún movimiento importado
- **THEN** se pide desde el principio del histórico

#### Scenario: Ejecución solapada

- **WHEN** se dispara una sincronización de una cuenta que ya tiene otra en curso
- **THEN** la nueva no se ejecuta y queda constancia de que se omitió por solape

#### Scenario: Plataforma no disponible

- **WHEN** la plataforma no responde o devuelve un error temporal
- **THEN** el sistema reintenta con espera creciente y, si agota los reintentos, deja la ejecución en estado fallido sin alterar el punto desde el que parte la siguiente
