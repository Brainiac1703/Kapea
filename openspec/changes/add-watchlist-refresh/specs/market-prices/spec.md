## MODIFIED Requirements

### Requirement: Actualización bajo demanda

Los precios se DEBEN pedir al abrir la página y cuando el usuario los refresque explícitamente. El sistema NO DEBE refrescarlos por su cuenta de forma periódica.

Esto vale para **cualquier** pantalla que muestre precios, no sólo para la cartera. Una pantalla que los enseñe y no deje volver a pedirlos obliga a recargar el navegador entero para saber si han cambiado.

Las capas gratuitas de los proveedores tienen límites, y una cartera se consulta de vez en cuando: refrescar sola consumiría cuota sin cambiar ninguna decisión.

#### Scenario: Apertura de la página

- **WHEN** el usuario abre la cartera
- **THEN** el sistema obtiene los precios y los muestra con su instante

#### Scenario: Refresco explícito

- **WHEN** el usuario pulsa refrescar
- **THEN** el sistema vuelve a pedir los precios y actualiza los valores y el instante

#### Scenario: Refresco en la lista de seguimiento

- **WHEN** el usuario está en la lista de seguimiento
- **THEN** puede volver a pedir los precios sin recargar la página

#### Scenario: Recargas seguidas

- **WHEN** el usuario recarga la página varias veces en poco tiempo
- **THEN** el sistema reutiliza el precio ya obtenido dentro de una ventana breve, en lugar de repetir la consulta al proveedor
