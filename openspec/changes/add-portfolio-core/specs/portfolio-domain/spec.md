## Purpose

Define el modelo de cartera de Kapea —activos, divisas, cuentas por plataforma, movimientos normalizados y lotes FIFO— y las invariantes que lo mantienen coherente y auditable, con independencia de la plataforma de la que provengan los datos.

## ADDED Requirements

### Requirement: Aislamiento por usuario

Toda entidad de cartera DEBE estar asociada a un `UserId`, y toda lectura o escritura DEBE quedar restringida al usuario autenticado, aunque en la fase actual exista un único usuario.

#### Scenario: Lectura restringida al propietario

- **WHEN** un usuario autenticado consulta cuentas, movimientos, lotes o posiciones
- **THEN** el sistema devuelve exclusivamente las entidades cuyo `UserId` coincide con el suyo

#### Scenario: Acceso a una entidad ajena

- **WHEN** un usuario solicita por identificador una entidad que pertenece a otro `UserId`
- **THEN** el sistema responde como si la entidad no existiera, sin revelar su existencia

### Requirement: Catálogo de activos

El sistema DEBE mantener un catálogo de activos identificados de forma estable e independiente del nombre que use cada plataforma. Un activo DEBE declarar su clase (`Equity` o `Crypto`), su símbolo canónico y, cuando exista, un identificador externo estándar (ISIN para renta variable). Dos importaciones del mismo activo desde plataformas distintas DEBEN resolver al mismo activo del catálogo.

#### Scenario: Mismo activo desde dos plataformas

- **WHEN** se importa un movimiento de `BTC` desde Kraken y otro de `BTC` desde Bit2Me
- **THEN** ambos movimientos referencian el mismo activo del catálogo

#### Scenario: Símbolo desconocido

- **WHEN** una importación aporta un símbolo que no se puede resolver contra el catálogo
- **THEN** el sistema crea el activo como no verificado y marca el movimiento para revisión del usuario, sin bloquear el resto de la importación

#### Scenario: Precisión por clase de activo

- **WHEN** se registra una cantidad de un activo de clase `Crypto`
- **THEN** el sistema conserva al menos 8 decimales sin redondeo intermedio

### Requirement: Cuentas por plataforma

El usuario DEBE poder registrar una o varias cuentas, cada una asociada a una plataforma (`XTB`, `Kraken`, `Bit2Me`) y a una divisa base. Todo movimiento DEBE pertenecer exactamente a una cuenta.

#### Scenario: Alta de cuenta

- **WHEN** el usuario registra una cuenta indicando plataforma, alias y divisa base
- **THEN** el sistema la crea y queda disponible como destino de importación

#### Scenario: Borrado de cuenta con movimientos

- **WHEN** el usuario intenta borrar una cuenta que tiene movimientos importados
- **THEN** el sistema rechaza la operación y explica que primero deben eliminarse o reasignarse sus movimientos

### Requirement: Movimiento normalizado

Todo dato importado DEBE convertirse en un movimiento normalizado con, como mínimo: cuenta, tipo, activo (cuando aplique), cantidad, precio unitario, divisa de la operación, comisiones, fecha y hora con zona horaria, y una referencia inmutable al origen del que procede.

Los tipos de movimiento soportados DEBEN incluir `Buy`, `Sell`, `Deposit`, `Withdrawal`, `Transfer`, `Dividend`, `Fee`, `Interest`, `Reward`, `Split` y `Unknown`.

#### Scenario: Movimiento con todos los campos

- **WHEN** un adaptador entrega una compra con cuenta, activo, cantidad, precio, divisa, comisión y fecha
- **THEN** el sistema persiste el movimiento normalizado con la referencia a su origen

#### Scenario: Tipo no reconocido

- **WHEN** el origen aporta un concepto de operación que no encaja en ningún tipo soportado
- **THEN** el sistema registra el movimiento con tipo `Unknown`, lo excluye del cálculo de P&L y lo señala como pendiente de clasificar

#### Scenario: Fecha sin zona horaria

- **WHEN** el origen aporta una fecha sin zona horaria explícita
- **THEN** el sistema la interpreta en la zona declarada por el adaptador y almacena el instante en UTC junto con la zona de origen

### Requirement: Inmutabilidad del movimiento importado

Un movimiento importado NO DEBE modificarse en sus datos financieros (cantidad, precio, divisa, comisión, fecha). Las correcciones DEBEN expresarse como un ajuste manual con su propia trazabilidad, o eliminando y reimportando la ejecución de importación completa.

#### Scenario: Intento de edición directa

- **WHEN** se intenta alterar la cantidad o el precio de un movimiento ya importado
- **THEN** el sistema rechaza el cambio

#### Scenario: Corrección mediante ajuste

- **WHEN** el usuario registra un ajuste manual sobre una posición
- **THEN** el sistema lo persiste como movimiento propio, identificado como ajuste manual y con el motivo aportado por el usuario

### Requirement: Lote FIFO como entidad de primer nivel

Cada adquisición de un activo DEBE generar un lote con su cantidad original, cantidad restante, coste de adquisición en EUR y fecha de adquisición. Los lotes DEBEN ser consultables de forma independiente del cálculo que los consume.

#### Scenario: Compra genera lote

- **WHEN** se importa una compra de 10 unidades de un activo
- **THEN** el sistema crea un lote de 10 unidades con cantidad restante 10 y su coste en EUR

#### Scenario: La cantidad restante nunca es negativa

- **WHEN** un cálculo intentaría consumir de un lote más cantidad de la que le resta
- **THEN** el sistema rechaza la operación y notifica una inconsistencia de datos en lugar de dejar la cantidad restante en negativo

### Requirement: Trazabilidad de extremo a extremo

Toda cifra derivada (lote, posición, resultado) DEBE ser reconducible hasta los movimientos que la originan, y todo movimiento hasta la ejecución de importación y el registro de origen del que salió.

#### Scenario: Descomposición de un resultado

- **WHEN** el usuario consulta un resultado realizado
- **THEN** el sistema enumera los lotes consumidos y, para cada uno, los movimientos de compra y de venta que lo originan

#### Scenario: Origen de un movimiento

- **WHEN** el usuario consulta un movimiento importado
- **THEN** el sistema indica la ejecución de importación, la plataforma y el identificador o la fila del registro original
