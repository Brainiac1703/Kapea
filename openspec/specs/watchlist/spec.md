# watchlist Specification

## Purpose

Guarda qué activos vigila el usuario, tenga o no posición en ellos, para que los sistemas y las ideas puedan hablar de algo antes de comprarlo.

## Requirements

### Requirement: Lo que se tiene está en seguimiento

Un activo con posición abierta DEBE estar en seguimiento sin que el usuario lo añada, y NO DEBE poder quitarse mientras la tenga. El seguimiento NO DEBE mantenerse como una copia de la cartera que haya que sincronizar: tener posición es una de las razones por las que un activo está en la lista.

Vender por completo un activo NO DEBE sacarlo del seguimiento. Quien ha tenido algo suele querer saber cómo sigue.

#### Scenario: Primera compra de un activo

- **WHEN** se importa la primera compra de un activo que el usuario no seguía
- **THEN** pasa a estar en seguimiento sin que tenga que añadirlo

#### Scenario: Venta completa

- **WHEN** el usuario vende toda su posición en un activo
- **THEN** sigue en la lista, ahora sin posición

#### Scenario: Quitar algo que se tiene

- **WHEN** el usuario intenta quitar del seguimiento un activo en el que tiene posición
- **THEN** el sistema lo rechaza y explica que primero tendría que dejar de tenerlo

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

### Requirement: La lista dice de cada activo en qué situación está

La lista DEBE mostrar, para cada activo, si se tiene o sólo se vigila, su último precio con la fecha a la que corresponde, y la posición cuando la haya. Un activo sin precio DEBE decirlo, en lugar de aparecer con valor cero.

#### Scenario: Activo en cartera

- **WHEN** el usuario mira un activo del que tiene posición
- **THEN** la lista lo identifica como tal y muestra su cantidad y su valor

#### Scenario: Activo sólo vigilado

- **WHEN** el usuario mira un activo que no tiene
- **THEN** la lista lo identifica como vigilado y muestra su precio sin hablar de posición

#### Scenario: Activo sin precio

- **WHEN** un activo en seguimiento no tiene ningún precio disponible
- **THEN** la lista lo dice, y no muestra un cero que se leería como que no vale nada

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
