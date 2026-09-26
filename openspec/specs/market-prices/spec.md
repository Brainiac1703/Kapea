# market-prices Specification

## Purpose

Define de dónde salen los precios de mercado de los activos, tanto el actual como las series históricas, y cómo se comporta Kapea cuando un proveedor no tiene el dato.

## Requirements

### Requirement: Series históricas del proveedor

Un proveedor de precios DEBE poder entregar, además del precio de ahora, el cierre diario de un activo entre dos fechas. Un proveedor que no cubra un activo o un rango DEBE devolver lo que tenga, sin impedir que se pidan los demás.

#### Scenario: Rango completo

- **WHEN** se pide a un proveedor el histórico de un activo que cubre
- **THEN** devuelve un precio por cada día del rango en que hubo cotización

#### Scenario: Activo no cubierto

- **WHEN** se pide el histórico de un activo que el proveedor no conoce
- **THEN** devuelve una serie vacía y los activos restantes se piden igual

#### Scenario: Cuota agotada

- **WHEN** el proveedor rechaza la petición por exceso de peticiones
- **THEN** el sistema conserva lo ya obtenido y deja el resto para la siguiente ejecución, sin perder lo descargado

### Requirement: Búsqueda de activos en el proveedor

Un proveedor de precios DEBE poder buscar activos por nombre o por símbolo y devolver, de cada uno, el nombre con el que se le conoce, su símbolo, la clase de activo que es, dónde cotiza cuando eso lo distinga de otro con el mismo símbolo, y el identificador con el que ese proveedor lo conoce.

Un proveedor que no encuentre nada DEBE devolver una lista vacía, y uno que falle NO DEBE impedir que se vean los resultados de los demás.

#### Scenario: Búsqueda por nombre

- **WHEN** el usuario busca un activo por su nombre
- **THEN** el sistema devuelve los activos que coinciden, cada uno con su símbolo, su clase y dónde cotiza

#### Scenario: Búsqueda por símbolo

- **WHEN** el usuario busca escribiendo un símbolo
- **THEN** el sistema devuelve los activos que lo usan, que pueden ser varios

#### Scenario: Un proveedor caído

- **WHEN** uno de los proveedores falla durante una búsqueda
- **THEN** se devuelven los resultados de los demás y se dice que la búsqueda está incompleta

#### Scenario: Sin resultados

- **WHEN** ningún proveedor encuentra nada con ese texto
- **THEN** el sistema lo dice, y ofrece seguir el activo por su símbolo igualmente

### Requirement: El identificador del proveedor se guarda, no se adivina

Cuando un activo se da de alta eligiendo un resultado de búsqueda, el sistema DEBE guardar el identificador con el que ese proveedor lo conoce y usarlo después para pedir sus precios.

Dos activos distintos pueden compartir símbolo, así que el sistema NO DEBE deducir el identificador a partir del símbolo cuando ya tiene uno guardado: hacerlo podría traer el precio de otro activo sin que nada fallara.

#### Scenario: Activo elegido en una búsqueda

- **WHEN** el usuario añade un activo eligiéndolo de los resultados
- **THEN** sus precios se piden con el identificador que traía ese resultado

#### Scenario: Dos activos con el mismo símbolo

- **WHEN** existen dos activos distintos que comparten símbolo y el usuario elige uno
- **THEN** los precios que se piden son los del que eligió

#### Scenario: Activo sin identificador guardado

- **WHEN** un activo entró por una importación y no tiene identificador de proveedor
- **THEN** el sistema lo resuelve como hasta ahora, a partir de su símbolo

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
