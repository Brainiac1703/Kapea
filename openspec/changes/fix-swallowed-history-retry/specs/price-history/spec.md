## MODIFIED Requirements

### Requirement: Descarga incremental

El sistema DEBE pedir a los proveedores solo los días que le faltan. Una segunda ejecución sobre una serie ya completa no PUEDE generar ninguna petición de datos ya guardados.

El sistema DEBE recordar hasta qué fecha hacia atrás ha pedido la serie de cada activo, y NO DEBE volver a pedir por debajo de ella. Recordar sólo lo guardado no basta: un tramo pedido que no devolvió nada volvería a pedirse en cada ejecución, indefinidamente y sin que nada lo delatara.

Un tramo sólo DEBE contar como pedido cuando el proveedor haya podido contestar, diga lo que diga. Un tramo que el proveedor no pudo contestar NO DEBE contar como pedido: dar por preguntado lo que nunca se llegó a preguntar convierte un fallo pasajero en un hueco permanente, y lo hace sin dejar rastro. Que no venga ningún precio y que no se pueda preguntar son cosas distintas, y el sistema DEBE distinguirlas.

Un tramo DEBE volver a pedirse cuando cambie lo que se busca: si el suelo baja por debajo de lo ya pedido, o si se resuelve el activo con un identificador de proveedor distinto del usado entonces.

#### Scenario: Puesta al día

- **WHEN** la serie llega hasta anteayer y se ejecuta la actualización
- **THEN** el sistema pide únicamente los dos días que faltan

#### Scenario: Serie ya completa

- **WHEN** se ejecuta la actualización dos veces seguidas el mismo día
- **THEN** la segunda no pide ningún dato al proveedor

#### Scenario: Tramo pedido que no devolvió nada

- **WHEN** se pidió la historia anterior a la primera cotización de un activo y el proveedor no devolvió nada
- **THEN** las ejecuciones siguientes no vuelven a pedir ese tramo

#### Scenario: Tramo que el proveedor no pudo contestar

- **WHEN** se pide un tramo y el proveedor falla en lugar de contestar
- **THEN** ese tramo no cuenta como pedido y la ejecución siguiente vuelve a pedirlo

#### Scenario: Relleno hacia atrás que falla

- **WHEN** falla el tramo de historia antigua de un activo cuya serie ya llega hasta hoy
- **THEN** el hueco anterior sigue apareciendo como pendiente en las ejecuciones siguientes

#### Scenario: Activo cuya primera descarga falla entera

- **WHEN** un activo sin ningún precio guardado ve fallar su primera petición
- **THEN** conserva las dos direcciones pendientes, y la ejecución siguiente vuelve a pedir su serie desde el principio

#### Scenario: Suelo que baja

- **WHEN** se quiere historia anterior a la ya pedida
- **THEN** el sistema pide sólo el tramo nuevo, no el que ya había pedido

#### Scenario: Activo que aprende su identificador de proveedor

- **WHEN** un activo pasa a resolverse con un identificador de proveedor distinto del usado en la petición anterior
- **THEN** el sistema puede volver a pedir su historia, porque ahora pregunta por otra cosa

### Requirement: Degradación ante un proveedor caído

Un fallo del proveedor NO PUEDE dejar la serie a medias de forma silenciosa ni impedir que se guarde lo ya descargado. El sistema DEBE conservar lo obtenido y reintentar los días que falten en la siguiente ejecución.

Un proveedor DEBE poder declarar que no ha podido contestar, y el sistema NO DEBE confundir esa declaración con la de un proveedor que contesta que no tiene nada. Cuando varios proveedores se reparten un rango, el rango sólo DEBE darse por contestado si ninguno de los que hicieron falta falló, aunque entre todos hayan traído días.

El reintento DEBE esperar a la siguiente ejecución. Insistir dentro de la misma pasada castiga precisamente el caso más común —haber pedido demasiado— y no aporta nada que la siguiente vuelta no dé.

Una ejecución DEBE dejar constancia de cuántos activos se quedaron a medias por un fallo, y no sólo de cuántos días escribió. Sin esa cifra, un relleno que falla entero y uno que va perfecto se leen igual.

#### Scenario: Corte a mitad de la descarga

- **WHEN** el proveedor falla después de entregar parte del rango pedido
- **THEN** el sistema guarda lo entregado y vuelve a pedir el resto en la siguiente ejecución

#### Scenario: Límite de peticiones tras una descarga masiva

- **WHEN** el proveedor empieza a rechazar peticiones por exceso de uso durante un relleno largo
- **THEN** lo descargado hasta ese momento se conserva, los tramos rechazados siguen pendientes y no se vuelve a insistir en la misma pasada

#### Scenario: Un proveedor falla y otro responde

- **WHEN** el primero de dos proveedores falla y el segundo entrega parte del rango
- **THEN** se guardan los días entregados y el rango no se da por contestado

#### Scenario: Proveedor que contesta que no tiene nada

- **WHEN** el proveedor responde correctamente y su respuesta no incluye ningún día
- **THEN** el tramo se da por contestado y no se vuelve a pedir

#### Scenario: Lo que la ejecución deja dicho

- **WHEN** termina una ejecución en la que algún activo falló
- **THEN** queda constancia de cuántos activos no pudieron completarse por un fallo del proveedor
