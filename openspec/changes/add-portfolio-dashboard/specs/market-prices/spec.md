## Purpose

Obtiene el precio de mercado de los activos de la cartera, traduciendo el símbolo de cada bróker al del proveedor, y define qué se enseña cuando un precio no está disponible.

## ADDED Requirements

### Requirement: Precio por clase de activo

El sistema DEBE obtener precios de criptomonedas y de renta variable, cada uno de su proveedor, y DEBE poder incorporar clases de activo nuevas sin cambiar el resto de la cartera.

#### Scenario: Precios de las dos clases

- **WHEN** la cartera tiene posiciones abiertas en acciones y en criptomonedas
- **THEN** el sistema obtiene el precio de unas y otras y las muestra en la misma página

#### Scenario: Clase sin proveedor

- **WHEN** la cartera tiene una posición de una clase para la que no hay proveedor de precios
- **THEN** la posición se muestra con su cantidad y su coste medio, indicando que el valor de mercado no está disponible

### Requirement: Conversión del símbolo del bróker al del proveedor

El sistema DEBE traducir el símbolo tal y como lo entrega el bróker al que espera el proveedor de precios, y DEBE permitir corregir esa traducción para un activo concreto sin cambiar la regla general.

#### Scenario: Sufijo de mercado estadounidense

- **WHEN** el bróker entrega un símbolo con sufijo de mercado estadounidense
- **THEN** el sistema consulta el precio con el símbolo sin ese sufijo

#### Scenario: Sufijo de otro mercado

- **WHEN** el bróker entrega un símbolo con sufijo de un mercado europeo
- **THEN** el sistema lo consulta tal cual, porque el proveedor usa la misma nomenclatura

#### Scenario: Excepción para un activo concreto

- **WHEN** un activo necesita un símbolo distinto del que produce la regla general
- **THEN** el sistema admite una correspondencia explícita para ese activo y la usa en su lugar

#### Scenario: Símbolo que el proveedor no reconoce

- **WHEN** el proveedor no encuentra el símbolo consultado
- **THEN** la posición se muestra sin valor de mercado y el sistema deja constancia de qué símbolo intentó

### Requirement: Ausencia de precio dicha explícitamente

Cuando falte el precio de un activo, el sistema NO DEBE mostrar cero ni omitir la posición. DEBE mostrarla con su cantidad y su coste, e indicar que el valor de mercado no está disponible.

Un cero se confunde con una posición sin valor, y una posición omitida se confunde con una posición que no existe. Las dos cosas falsean el total.

#### Scenario: Un activo sin precio

- **WHEN** no se obtiene el precio de uno de los activos de la cartera
- **THEN** su fila aparece con cantidad y coste, y su valor y su resultado latente se muestran como no disponibles

#### Scenario: Total con precios incompletos

- **WHEN** falta el precio de al menos un activo
- **THEN** el valor total de la cartera se presenta como incompleto, indicando cuántas posiciones no se han podido valorar

#### Scenario: El proveedor no responde

- **WHEN** el proveedor de precios falla o agota su cuota
- **THEN** la página se muestra igual con los datos que no dependen del precio, y se avisa de que los precios no se han podido actualizar

### Requirement: Antigüedad del precio visible

Cada precio DEBE mostrarse junto al instante al que corresponde, y la página DEBE indicar cuándo se actualizaron los precios por última vez.

#### Scenario: Instante de cada precio

- **WHEN** el usuario consulta una posición valorada
- **THEN** ve a qué momento corresponde el precio aplicado

#### Scenario: Precio de una sesión anterior

- **WHEN** el precio disponible es del cierre de una sesión anterior
- **THEN** el sistema lo muestra igualmente, indicando su antigüedad en lugar de ocultarlo

### Requirement: Actualización bajo demanda

Los precios se DEBEN pedir al abrir la página y cuando el usuario los refresque explícitamente. El sistema NO DEBE refrescarlos por su cuenta de forma periódica.

Las capas gratuitas de los proveedores tienen límites, y una cartera se consulta de vez en cuando: refrescar sola consumiría cuota sin cambiar ninguna decisión.

#### Scenario: Apertura de la página

- **WHEN** el usuario abre la cartera
- **THEN** el sistema obtiene los precios y los muestra con su instante

#### Scenario: Refresco explícito

- **WHEN** el usuario pulsa refrescar
- **THEN** el sistema vuelve a pedir los precios y actualiza los valores y el instante

#### Scenario: Recargas seguidas

- **WHEN** el usuario recarga la página varias veces en poco tiempo
- **THEN** el sistema reutiliza el precio ya obtenido dentro de una ventana breve, en lugar de repetir la consulta al proveedor
