## MODIFIED Requirements

### Requirement: El identificador del proveedor se guarda, no se adivina

Cuando un activo se da de alta eligiendo un resultado de búsqueda, el sistema DEBE guardar el identificador con el que ese proveedor lo conoce y usarlo después para pedir sus precios.

Dos activos distintos pueden compartir símbolo, así que el sistema NO DEBE deducir el identificador a partir del símbolo cuando ya tiene uno guardado: hacerlo podría traer el precio de otro activo sin que nada fallara.

Esto vale para **todas** las peticiones de precio, la de la serie histórica y la de la cotización de ahora. Que una lo use y la otra no deja al activo a medias, con gráfica y sin precio, sin que el usuario pueda entender por qué.

Un activo con identificador guardado NO DEBE depender de que su símbolo figure en ninguna lista escrita a mano. Esa lista PUEDE seguir existiendo para lo que no tiene identificador, pero NO DEBE ser la única vía.

#### Scenario: Activo elegido en una búsqueda

- **WHEN** el usuario añade un activo eligiéndolo de los resultados
- **THEN** sus precios se piden con el identificador que traía ese resultado

#### Scenario: Dos activos con el mismo símbolo

- **WHEN** existen dos activos distintos que comparten símbolo y el usuario elige uno
- **THEN** los precios que se piden son los del que eligió

#### Scenario: Activo sin identificador guardado

- **WHEN** un activo entró por una importación y no tiene identificador de proveedor
- **THEN** el sistema lo resuelve como hasta ahora, a partir de su símbolo

#### Scenario: Cotización de un activo fuera de la lista escrita a mano

- **WHEN** se pide la cotización de un activo con identificador guardado cuyo símbolo no está en la lista del proveedor
- **THEN** el proveedor lo resuelve por su identificador y devuelve su precio

#### Scenario: Las dos vías en la misma petición

- **WHEN** se piden a la vez activos con identificador guardado y activos sin él
- **THEN** cada uno se resuelve por su vía y ninguno impide el precio de los demás
