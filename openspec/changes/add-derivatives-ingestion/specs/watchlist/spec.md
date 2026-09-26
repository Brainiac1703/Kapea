## MODIFIED Requirements

### Requirement: Añadir y quitar activos

El usuario DEBE poder añadir al seguimiento un activo que nunca ha tenido, indicando su símbolo y su clase, y quitar del seguimiento el que no tenga posición. Quitarlo NO DEBE borrar el activo del catálogo ni su histórico de precios: deja de vigilarse, no deja de existir.

Al añadir un activo, el sistema DEBE decir si hay precios disponibles para él. Un activo que ningún proveedor cubre DEBE poder añadirse igualmente, pero advirtiendo de que no tendrá ni precio ni señales hasta que los haya.

El sistema DEBE decir además si el activo cotiza como contrato perpetuo. Un activo sin perpetuo DEBE seguirse igual, sabiendo que no tendrá señales de derivados.

#### Scenario: Añadir un activo con precios

- **WHEN** el usuario añade un activo que los proveedores cubren
- **THEN** queda en seguimiento y su histórico empieza a descargarse

#### Scenario: Añadir un activo sin cobertura

- **WHEN** ningún proveedor da precios del activo añadido
- **THEN** el sistema lo añade y advierte de que no tendrá precio ni señales mientras siga así

#### Scenario: Añadir algo que ya se sigue

- **WHEN** el usuario añade un activo que ya está en la lista
- **THEN** el sistema no lo duplica y lo dice

#### Scenario: Quitar algo que sólo se vigilaba

- **WHEN** el usuario quita un activo sin posición
- **THEN** desaparece de la lista, y sus precios y su histórico se conservan

#### Scenario: Añadir un activo con contrato perpetuo

- **WHEN** el usuario añade un activo que cotiza como perpetuo
- **THEN** queda en seguimiento y su dato de derivados empieza a recogerse

#### Scenario: Añadir un activo sin contrato perpetuo

- **WHEN** el activo añadido no cotiza como perpetuo en ninguna fuente
- **THEN** el sistema lo añade y dice que no tendrá señales de derivados
