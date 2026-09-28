## MODIFIED Requirements

### Requirement: Añadir y quitar activos

El usuario DEBE poder añadir al seguimiento un activo que nunca ha tenido, indicando su símbolo y su clase, y quitar del seguimiento el que no tenga posición. Quitarlo NO DEBE borrar el activo del catálogo ni su histórico de precios: deja de vigilarse, no deja de existir.

Al añadir un activo, el sistema DEBE decir si hay precios disponibles para él. Un activo que ningún proveedor cubre DEBE poder añadirse igualmente, pero advirtiendo de que no tendrá ni precio ni señales hasta que los haya.

La comprobación DEBE cubrir las dos cosas que el usuario va a ver: la serie histórica, que es lo que dibuja la gráfica y alimenta los indicadores, y la cotización del momento, que es lo que aparece en la columna de precio. Son proveedores distintos y su cobertura no coincide, así que preguntar sólo por una puede dar por bueno un activo que después aparece sin precio.

Cuando falte una de las dos, el aviso DEBE decir cuál. El remedio no es el mismo: un activo sin cotización suele resolverse volviéndolo a añadir desde la búsqueda, que es lo que le da el identificador con el que su proveedor lo conoce; uno sin serie no tiene arreglo desde la aplicación.

#### Scenario: Añadir un activo con precios

- **WHEN** el usuario añade un activo que los proveedores cubren
- **THEN** queda en seguimiento y su histórico empieza a descargarse

#### Scenario: Añadir un activo sin cobertura

- **WHEN** ningún proveedor da precios del activo añadido
- **THEN** el sistema lo añade y advierte de que no tendrá precio ni señales mientras siga así

#### Scenario: Activo con serie pero sin cotización

- **WHEN** el activo añadido tiene histórico pero ningún proveedor da su precio de ahora
- **THEN** el sistema lo añade y advierte de que aparecerá sin precio, diciendo que elegirlo de una búsqueda puede resolverlo

#### Scenario: Activo con cotización pero sin serie

- **WHEN** el activo añadido tiene precio de ahora pero ningún proveedor da su histórico
- **THEN** el sistema lo añade y advierte de que no tendrá gráfica ni indicadores

#### Scenario: Añadir algo que ya se sigue

- **WHEN** el usuario añade un activo que ya está en la lista
- **THEN** el sistema no lo duplica y lo dice

#### Scenario: Quitar algo que sólo se vigilaba

- **WHEN** el usuario quita un activo sin posición
- **THEN** desaparece de la lista, y sus precios y su histórico se conservan
