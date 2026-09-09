## MODIFIED Requirements

### Requirement: Detección de formato y fallo explícito

El sistema DEBE reconocer el formato del fichero por sus cabeceras antes de procesar filas, emparejándolo con un perfil de importación. Si ninguna cabecera se corresponde con un perfil conocido, la importación NO DEBE procesar filas a ciegas: DEBE ofrecer crear un perfil, indicando qué columnas se han encontrado.

#### Scenario: Cabeceras desconocidas

- **WHEN** el fichero no se corresponde con ningún perfil conocido
- **THEN** el sistema no importa nada y ofrece crear un perfil, enumerando las columnas encontradas

#### Scenario: Columna opcional ausente

- **WHEN** falta una columna que el perfil declara opcional
- **THEN** la importación continúa y los movimientos afectados quedan sin ese dato, señalados para revisión

#### Scenario: Columnas adicionales

- **WHEN** el fichero contiene columnas que el perfil no mapea
- **THEN** el sistema las ignora y procesa el fichero con normalidad

### Requirement: Interpretación de las convenciones de XTB

El perfil DEBE declarar las convenciones locales del informe —separador decimal, separador de columnas y formato de fecha— y el sistema DEBE aplicarlas al leer cada fila. La traducción de los conceptos de operación a tipos de movimiento DEBE venir del perfil, no del código.

#### Scenario: Convención decimal europea

- **WHEN** el perfil declara coma decimal y punto de millares
- **THEN** las cantidades y los importes se interpretan sin pérdida de precisión

#### Scenario: Concepto sin equivalencia

- **WHEN** una fila declara un concepto que el perfil no traduce
- **THEN** el movimiento se importa con tipo `Unknown` y queda pendiente de clasificar

#### Scenario: Fila con importe cero y sin activo

- **WHEN** una fila corresponde a un apunte informativo sin efecto financiero
- **THEN** el sistema la descarta y la contabiliza como registro no financiero en la ejecución

## ADDED Requirements

### Requirement: Perfiles de XTB distribuidos de serie

Kapea DEBE traer perfiles preparados para los informes conocidos de xStation5 —operaciones de efectivo, posiciones cerradas y posiciones abiertas— de modo que un usuario de XTB pueda importar sin definir ningún mapeo.

Esos perfiles DEBEN ser editables como cualquier otro: cuando XTB cambie su exportación, corregirlos NO DEBE requerir una versión nueva de la aplicación.

#### Scenario: Importación sin configurar nada

- **WHEN** un usuario sube una exportación de xStation5 de un formato conocido
- **THEN** el sistema la reconoce con el perfil de serie y no pide ningún mapeo

#### Scenario: XTB cambia su informe

- **WHEN** la exportación deja de coincidir con el perfil de serie
- **THEN** el usuario puede corregir el perfil desde la aplicación, sin esperar a una versión nueva

#### Scenario: Equivalencia con el comportamiento anterior

- **WHEN** se reimporta un fichero ya importado con la versión anterior de Kapea
- **THEN** todos sus registros se descartan como duplicados, porque los perfiles de serie reproducen la misma interpretación
