## ADDED Requirements

### Requirement: Un activo recuerda cómo lo conoce su proveedor

Un activo DEBE poder guardar el identificador con el que lo conoce el proveedor que da sus precios. Ese identificador NO DEBE confundirse con el símbolo canónico: el símbolo identifica el activo dentro de Kapea, y el identificador sólo sirve para pedirle datos a ese proveedor.

Un activo sin identificador guardado DEBE seguir funcionando: es el caso de todo lo que entró importando movimientos.

#### Scenario: Activo dado de alta desde una búsqueda

- **WHEN** el usuario añade un activo eligiéndolo de una búsqueda
- **THEN** el activo guarda el identificador de ese proveedor junto a su símbolo canónico

#### Scenario: Activo importado

- **WHEN** un activo entra por una importación de movimientos
- **THEN** se guarda sin identificador de proveedor y sus precios se resuelven por su símbolo
