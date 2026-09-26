## ADDED Requirements

### Requirement: Seguir un activo eligiéndolo de una búsqueda

El usuario DEBE poder añadir al seguimiento un activo buscándolo por nombre o por símbolo y eligiéndolo de los resultados, sin tener que declarar de qué clase es.

Cuando el activo elegido corresponde a uno que ya está en el catálogo, el sistema NO DEBE crear uno nuevo: DEBE reconocerlo y usar el que hay. Crear un segundo activo para lo mismo partiría en dos su historial y su cola de lotes.

#### Scenario: Activo que no se tenía

- **WHEN** el usuario elige de la búsqueda un activo que no está en el catálogo
- **THEN** se da de alta con el identificador de su proveedor y queda en seguimiento

#### Scenario: Activo que ya existe con otro nombre de símbolo

- **WHEN** el usuario elige un activo cuyo símbolo en el proveedor no es el mismo con el que Kapea lo tiene guardado
- **THEN** el sistema reconoce que es el mismo y no crea un activo nuevo

#### Scenario: Seguir por símbolo

- **WHEN** el usuario escribe directamente un símbolo y su clase, sin buscar
- **THEN** el activo se añade igual que antes
