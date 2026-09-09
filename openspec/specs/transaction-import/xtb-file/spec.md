# transaction-import/xtb-file Specification

## Purpose

Importa el histórico de operaciones de XTB a partir de la exportación de fichero de xStation5, asumiendo desde el diseño que ese formato cambia sin aviso y que el fallo debe ser visible y diagnosticable en lugar de silencioso.

## Requirements

### Requirement: Importación desde fichero subido

El sistema DEBE permitir al usuario subir un fichero de exportación de XTB en formato Excel o CSV y asociarlo a una cuenta de plataforma `XTB`, produciendo una ejecución de importación con el mismo ciclo de vida que cualquier otro adaptador.

#### Scenario: Fichero válido

- **WHEN** el usuario sube una exportación de xStation5 y elige una cuenta XTB
- **THEN** el sistema normaliza sus filas a movimientos y devuelve la ejecución con sus recuentos

#### Scenario: Cuenta de otra plataforma

- **WHEN** el usuario intenta asociar el fichero a una cuenta que no es de plataforma `XTB`
- **THEN** el sistema rechaza la importación indicando la incompatibilidad

#### Scenario: Fichero de tipo no admitido

- **WHEN** el usuario sube un fichero que no es Excel ni CSV
- **THEN** el sistema lo rechaza antes de procesarlo, indicando los formatos admitidos

### Requirement: Detección de formato y fallo explícito

El adaptador DEBE reconocer el formato del fichero por sus cabeceras antes de procesar filas. Si las cabeceras no se corresponden con ningún formato conocido, la importación DEBE fallar por completo indicando qué columnas se esperaban y cuáles se encontraron.

#### Scenario: Cabeceras desconocidas

- **WHEN** el fichero no contiene las columnas de ningún formato de exportación conocido
- **THEN** el sistema rechaza la importación entera e informa de las columnas esperadas y las encontradas

#### Scenario: Columna opcional ausente

- **WHEN** falta una columna que el formato reconocido declara opcional
- **THEN** la importación continúa y los movimientos afectados quedan sin ese dato, señalados para revisión

#### Scenario: Columnas adicionales

- **WHEN** el fichero contiene columnas que el formato conocido no declara
- **THEN** el sistema las ignora y procesa el fichero con normalidad

### Requirement: Vista previa antes de confirmar

Antes de persistir, el sistema DEBE ofrecer una vista previa con los movimientos que se importarían, los que se descartarían por duplicado y los que se rechazarían con su motivo. La persistencia solo DEBE ocurrir tras la confirmación del usuario.

#### Scenario: Vista previa y confirmación

- **WHEN** el usuario sube el fichero
- **THEN** el sistema muestra la vista previa sin persistir nada, y solo importa cuando el usuario confirma

#### Scenario: Vista previa descartada

- **WHEN** el usuario abandona la vista previa sin confirmar
- **THEN** no queda ningún movimiento persistido ni ninguna ejecución completada

### Requirement: Interpretación de las convenciones de XTB

El adaptador DEBE interpretar correctamente las convenciones locales de la exportación —separador decimal, separador de columnas, formato de fecha y divisa de la operación— y DEBE traducir los conceptos de operación de XTB a los tipos de movimiento normalizados.

#### Scenario: Convención decimal europea

- **WHEN** el fichero usa coma como separador decimal y punto como separador de millares
- **THEN** las cantidades y los importes se interpretan sin pérdida de precisión

#### Scenario: Concepto sin equivalencia

- **WHEN** una fila declara un concepto de operación que no tiene equivalencia en los tipos normalizados
- **THEN** el movimiento se importa con tipo `Unknown` y queda pendiente de clasificar

#### Scenario: Fila con importe cero y sin activo

- **WHEN** una fila corresponde a un apunte informativo sin efecto financiero
- **THEN** el sistema la descarta y la contabiliza como registro no financiero en la ejecución
