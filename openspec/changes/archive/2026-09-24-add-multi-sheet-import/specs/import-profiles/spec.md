## ADDED Requirements

### Requirement: La tabla se localiza dentro del fichero

El sistema DEBE encontrar la tabla de datos aunque no empiece en la primera fila y aunque no esté en la primera hoja. Un informe puede traer delante sus metadatos —número de cuenta, título, periodo— y repartir su contenido en varias hojas, y eso NO DEBE impedir reconocerlo.

La fila de cabeceras es la primera que reconoce un perfil de la plataforma. Si ninguna fila de ninguna hoja la reconoce, el sistema DEBE decir qué encontró, para que el usuario pueda crear un perfil.

#### Scenario: Metadatos antes de la tabla

- **WHEN** el usuario sube un fichero cuyas primeras filas son datos del informe y la tabla empieza más abajo
- **THEN** el sistema encuentra la fila de cabeceras y trata como movimientos sólo las filas que vienen después

#### Scenario: La tabla no está en la primera hoja

- **WHEN** el libro tiene varias hojas y la que reconoce un perfil no es la primera
- **THEN** el sistema la usa igualmente

#### Scenario: Ninguna hoja se reconoce

- **WHEN** ninguna fila de ninguna hoja coincide con un perfil de la plataforma
- **THEN** el sistema propone crear un perfil indicando las cabeceras que ha encontrado y en qué hoja

### Requirement: Un perfil de serie que cambia alcanza a quien ya lo tenía

Cuando la definición de un perfil que viene de serie cambia, el sistema DEBE llevar ese cambio a las instalaciones que ya lo tenían, como una versión nueva del mismo perfil. Las versiones anteriores DEBEN conservarse, para que un movimiento importado con una de ellas siga explicando con qué reglas entró.

Un perfil que el usuario haya hecho suyo NO DEBE tocarse: manda su versión.

#### Scenario: Se corrige cómo se lee una plataforma

- **WHEN** cambia la definición de serie de un perfil y el usuario ya lo tenía sin modificar
- **THEN** el perfil gana una versión nueva con la definición corregida, y la siguiente importación la usa

#### Scenario: El perfil que el usuario editó

- **WHEN** el usuario ha modificado un perfil y después cambia la definición de serie
- **THEN** el suyo se queda como está

#### Scenario: Nada que actualizar

- **WHEN** la definición de serie no ha cambiado
- **THEN** no se añade ninguna versión, por muchas veces que se arranque

### Requirement: Perfil atado a una hoja

Un perfil PUEDE declarar el nombre de la hoja que reconoce. Cuando lo declara, sólo se aplica a esa hoja; cuando no, se aplica a cualquiera cuyas cabeceras reconozca.

#### Scenario: Dos hojas de cabeceras parecidas

- **WHEN** un libro trae dos hojas cuyas cabeceras podrían encajar con el mismo perfil y uno de los perfiles declara su hoja
- **THEN** cada hoja se lee con el perfil que le corresponde, y no con el del otro

#### Scenario: Perfil sin hoja declarada

- **WHEN** un perfil no declara hoja
- **THEN** se aplica a la hoja cuyas cabeceras reconozca, como hasta ahora

## MODIFIED Requirements

### Requirement: Emparejamiento de un fichero con su perfil

Al subir un fichero, el sistema DEBE buscar entre los perfiles de la plataforma de la cuenta destino los que reconozcan sus cabeceras, en cualquiera de sus hojas. Si ninguno encaja, DEBE ofrecer crear uno en lugar de rechazar el fichero sin más.

Un mismo fichero PUEDE emparejarse con varios perfiles, uno por hoja, y entonces la importación DEBE alimentarse de todas ellas con una sola subida. El usuario DEBE ver de qué hojas se ha leído y con qué perfil cada una.

#### Scenario: Fichero de un formato ya conocido

- **WHEN** el usuario sube un fichero cuyas cabeceras coinciden con un perfil existente
- **THEN** el sistema lo usa directamente y no propone ningún mapeo nuevo

#### Scenario: Formato desconocido

- **WHEN** ningún perfil reconoce las cabeceras del fichero
- **THEN** el sistema propone crear un perfil, indicando las cabeceras encontradas

#### Scenario: Varios perfiles posibles

- **WHEN** más de un perfil reconoce las cabeceras de la misma hoja
- **THEN** el sistema usa el más reciente y deja constancia de cuál ha aplicado

#### Scenario: Un libro con dos hojas útiles

- **WHEN** el usuario sube un libro en el que dos hojas distintas son reconocidas por dos perfiles
- **THEN** la importación incluye los movimientos de las dos, en una sola ejecución, y dice de qué hoja sale cada uno
