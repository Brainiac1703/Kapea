# import-profiles Specification

## Purpose

Describe cómo Kapea aprende el formato de un fichero de movimientos una sola vez y lo reutiliza después, de modo que añadir una plataforma nueva no exija escribir un adaptador y que la interpretación de cada importación quede registrada y sea reproducible.

## Requirements

### Requirement: Perfil de importación

Un perfil DEBE describir cómo se lee un fichero de una plataforma: el delimitador y las convenciones de número y fecha, la correspondencia entre columnas del fichero y campos del movimiento normalizado, y la traducción de cada concepto del origen a un tipo de movimiento.

Un perfil DEBE identificar el formato que reconoce por el conjunto de cabeceras del fichero, y DEBE declarar a qué plataforma pertenece.

#### Scenario: Perfil completo

- **WHEN** el usuario guarda un perfil con sus cabeceras, su correspondencia de columnas y su traducción de conceptos
- **THEN** el sistema lo persiste y queda disponible para importar ficheros de esa plataforma

#### Scenario: Perfil sin los campos mínimos

- **WHEN** un perfil no asigna columna a la fecha o al importe
- **THEN** el sistema rechaza guardarlo e indica qué campo obligatorio falta

#### Scenario: Concepto sin traducción

- **WHEN** el perfil no traduce un concepto que aparece en el fichero
- **THEN** los movimientos de ese concepto se importan con tipo `Unknown` y quedan pendientes de clasificar, sin bloquear el resto

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

### Requirement: Propuesta de mapeo asistida por IA

Cuando no hay perfil para un fichero, el sistema DEBE poder proponer uno a partir de sus cabeceras y de un número acotado de filas de ejemplo. La propuesta DEBE incluir, para cada campo mapeado, una medida de confianza.

El sistema NO DEBE enviar el fichero completo al servicio de IA. Solo DEBEN salir la fila de cabeceras y como mucho tres filas de ejemplo.

#### Scenario: Propuesta a partir de un fichero desconocido

- **WHEN** el usuario pide proponer un mapeo para un fichero cuyo formato no se reconoce
- **THEN** el sistema devuelve una correspondencia de columnas y una traducción de conceptos, cada una con su confianza

#### Scenario: Lo que sale de la máquina

- **WHEN** el sistema solicita una propuesta de mapeo
- **THEN** la petición contiene únicamente las cabeceras y como mucho tres filas del fichero, y el resto del contenido no abandona el servidor

#### Scenario: El servicio de IA no responde

- **WHEN** el servicio de IA falla o no está configurado
- **THEN** el sistema ofrece el mapeo manual con el mismo formulario, sin propuesta previa, y la importación puede continuar

### Requirement: Confirmación cuando la propuesta no es concluyente

El sistema DEBE pedir confirmación al usuario antes de usar una propuesta cuando falte algún campo obligatorio, cuando la confianza de algún campo quede por debajo del umbral configurado, o cuando algún concepto del fichero no se haya podido traducir.

Cuando ninguna de esas condiciones se cumpla, el sistema PUEDE aceptar la propuesta y continuar sin preguntar.

#### Scenario: Propuesta concluyente

- **WHEN** la propuesta mapea todos los campos obligatorios por encima del umbral y traduce todos los conceptos
- **THEN** el sistema guarda el perfil y sigue con la importación sin pedir confirmación

#### Scenario: Confianza insuficiente

- **WHEN** la confianza de algún campo obligatorio queda por debajo del umbral
- **THEN** el sistema muestra el mapeo propuesto para que el usuario lo confirme o lo corrija antes de continuar

#### Scenario: Concepto sin traducir en la propuesta

- **WHEN** la propuesta deja algún concepto del fichero sin traducir
- **THEN** el sistema pide confirmación indicando qué conceptos no ha sabido interpretar

#### Scenario: Corrección del usuario

- **WHEN** el usuario corrige un mapeo propuesto y lo confirma
- **THEN** el perfil se guarda con las correcciones, no con la propuesta original

### Requirement: Mapeo manual sin IA

El usuario DEBE poder crear y editar un perfil eligiendo a mano la columna de cada campo y el tipo de cada concepto, con independencia de que haya servicio de IA disponible.

#### Scenario: Creación manual

- **WHEN** el usuario crea un perfil asignando cada campo a una columna del fichero
- **THEN** el sistema lo guarda y lo aplica igual que a uno propuesto por IA

#### Scenario: Edición de un perfil existente

- **WHEN** el usuario corrige el mapeo de un perfil ya guardado
- **THEN** el sistema crea una versión nueva del perfil y conserva la anterior

### Requirement: Versionado y trazabilidad del perfil

Cada movimiento importado DEBE registrar el perfil y la versión con los que se interpretó. Editar un perfil NO DEBE alterar los movimientos ya importados con una versión anterior.

#### Scenario: Origen de la interpretación

- **WHEN** el usuario consulta un movimiento importado desde un fichero
- **THEN** el sistema indica el perfil y la versión que lo interpretaron, además de la ejecución de importación y la fila original

#### Scenario: Corrección posterior de un perfil

- **WHEN** el usuario corrige un perfil después de haber importado con él
- **THEN** los movimientos ya importados conservan su versión y sus cifras, y la corrección solo afecta a las importaciones siguientes

#### Scenario: Reimportación tras corregir el perfil

- **WHEN** el usuario reimporta un fichero con una versión corregida del perfil
- **THEN** los movimientos cuya huella no cambie se descartan como duplicados y solo entran los que la corrección haya alterado

### Requirement: La IA no interviene en las cifras

El servicio de IA solo DEBE producir la correspondencia entre columnas y campos y la traducción de conceptos. Los importes, las cantidades y las fechas DEBEN leerse del fichero aplicando el perfil, sin intervención del modelo.

#### Scenario: Importación con perfil existente

- **WHEN** se importa un fichero cuyo formato ya tiene perfil
- **THEN** no se realiza ninguna llamada al servicio de IA

#### Scenario: Reproducibilidad

- **WHEN** se vuelve a importar el mismo fichero con el mismo perfil
- **THEN** los movimientos resultantes son idénticos, con independencia de que el servicio de IA esté disponible

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
