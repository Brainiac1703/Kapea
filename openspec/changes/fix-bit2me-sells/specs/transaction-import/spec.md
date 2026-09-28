## MODIFIED Requirements

### Requirement: Registros rechazados sin bloquear la importación

Un registro que no se puede normalizar NO DEBE abortar la ejecución. El sistema DEBE conservarlo con su contenido original y el motivo del rechazo, y DEBE permitir al usuario revisarlo y reprocesarlo tras corregir el problema.

El recuento de rechazados DEBE llegar hasta quien lanzó la importación, también cuando la lanza una sincronización automática. Guardarlo sólo en la ejecución no basta: una sincronización que rechaza registros y se declara terminada sin fallo esconde una pérdida de datos que nadie va a buscar. Un rechazo que no se ve tarda meses en descubrirse, y para entonces las cifras llevan meses mal.

#### Scenario: Fila ilegible en un fichero

- **WHEN** una fila del fichero de origen no se puede interpretar
- **THEN** el sistema la registra como rechazada con su motivo, continúa con el resto y refleja el recuento en la ejecución

#### Scenario: Reproceso de rechazados

- **WHEN** el usuario reprocesa los registros rechazados de una ejecución tras corregir la causa
- **THEN** los que ya se pueden normalizar se importan y los que siguen fallando conservan su estado de rechazo con el motivo actualizado

#### Scenario: Rechazos en una sincronización automática

- **WHEN** una sincronización programada o lanzada a mano deja registros rechazados
- **THEN** su recuento aparece en lo que la sincronización informa, en lugar de declararse terminada sin más

#### Scenario: Sincronización limpia

- **WHEN** una sincronización no rechaza ningún registro
- **THEN** no se avisa de rechazos, para que el aviso signifique algo cuando aparezca
