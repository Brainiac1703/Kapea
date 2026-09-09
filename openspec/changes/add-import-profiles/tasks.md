## 1. Las plataformas pasan a ser datos

- [ ] 1.1 Sustituir el enumerado Platform por una entidad con nombre y forma de importación (fichero o API), con las tres actuales sembradas; verificar con test que dar de alta una plataforma de fichero la hace elegible al crear una cuenta sin tocar código
- [ ] 1.2 Migrar el esquema y los datos existentes traduciendo los tres valores actuales a filas; verificar sobre la base de datos que las cuentas y los movimientos ya importados conservan su plataforma
- [ ] 1.3 Adaptar el registro de adaptadores para que resuelva por plataforma sin depender del enumerado; verificar con test que una plataforma de API sin adaptador se rechaza enumerando las soportadas
- [ ] 1.4 Actualizar los contratos y las pantallas de cuentas y credenciales para que la lista de plataformas y su forma de importación vengan del servidor; verificar manualmente que credenciales solo ofrece cuentas de plataformas de API e importar solo las de fichero

## 2. Perfil de importación

- [ ] 2.1 Implementar la entidad de perfil con sus cabeceras reconocidas, delimitador, convenciones de número y fecha, correspondencia de columnas y traducción de conceptos; verificar con tests que un perfil sin fecha o sin importe se rechaza indicando el campo que falta
- [ ] 2.2 Implementar el versionado: editar un perfil crea una versión nueva y conserva la anterior; verificar con test que la versión previa sigue siendo recuperable
- [ ] 2.3 Añadir al movimiento la referencia al perfil y la versión con los que se interpretó; verificar con test de integración que la consulta de un movimiento devuelve esa referencia junto a la ejecución y la fila original
- [ ] 2.4 Implementar el emparejamiento de un fichero con su perfil por las cabeceras; verificar con tests de formato conocido, formato desconocido y varios perfiles posibles, comprobando que en el último caso se aplica el más reciente y se deja constancia
- [ ] 2.5 Persistir perfiles y versiones con su configuración de EF Core; verificar con test de integración que un perfil sobrevive al viaje de ida y vuelta con todas sus reglas

## 3. Adaptador genérico de fichero

- [ ] 3.1 Implementar el adaptador genérico que lee un fichero aplicando un perfil y produce registros normalizados; verificar con tests de convención decimal europea e invariante, y de formatos de fecha distintos
- [ ] 3.2 Implementar la traducción de conceptos según el perfil, con `Unknown` para lo que no traduzca; verificar con test que un concepto sin traducción no bloquea el resto del fichero
- [ ] 3.3 Implementar el descarte contado de apuntes sin efecto financiero según lo que declare el perfil; verificar con test que se cuentan y no se rechazan
- [ ] 3.4 Comprobar que la huella de deduplicación no cambia de forma respecto al adaptador anterior; verificar con test que reimportar un fichero ya importado con la versión previa descarta todos sus registros como duplicados

## 4. Perfiles de XTB de serie

- [ ] 4.1 Convertir los tres formatos conocidos de xStation5 —efectivo, posiciones cerradas y posiciones abiertas— en perfiles sembrados; verificar que se dan de alta al crear la base de datos
- [ ] 4.2 Migrar los tests de extremo a extremo del adaptador de XTB al adaptador genérico con esos perfiles; verificar que producen exactamente los mismos movimientos que antes
- [ ] 4.3 Retirar el adaptador de XTB codificado y sus formatos; verificar que la batería completa sigue pasando y que no queda ninguna referencia al adaptador retirado
- [ ] 4.4 Comprobar que un perfil de serie se puede editar desde la aplicación; verificar manualmente que corregir una cabecera permite importar un fichero que antes no encajaba

## 5. Propuesta de mapeo por IA

- [ ] 5.1 Definir el puerto de propuesta de mapeo en la capa de aplicación, con su contrato de entrada (cabeceras y filas de ejemplo) y de salida (correspondencia, traducción y confianza por campo); verificar con un doble que el motor de perfiles funciona sin implementación real
- [ ] 5.2 Implementar el adaptador contra Azure OpenAI; verificar contra respuestas grabadas que una propuesta bien formada se interpreta y que una malformada se rechaza sin romper la importación
- [ ] 5.3 Garantizar que la petición lleva solo cabeceras y como mucho tres filas; verificar con test que inspecciona la petición emitida y falla si contiene más filas del fichero
- [ ] 5.4 Implementar la validación de la propuesta contra las filas de ejemplo: si la columna mapeada como fecha o como importe no se puede interpretar en ellas, la propuesta se marca como no concluyente; verificar con tests de columna mal mapeada
- [ ] 5.5 Implementar la regla de confirmación —campo obligatorio sin mapear, confianza por debajo del umbral o concepto sin traducir— con el umbral en configuración; verificar con tests de propuesta concluyente y de cada motivo de duda por separado
- [ ] 5.6 Implementar la degradación cuando el servicio no está configurado o falla; verificar con test que la importación sigue siendo posible por la vía manual

## 6. Interfaz

- [ ] 6.1 Ampliar la vista previa con las primeras filas ya interpretadas —fecha, tipo, activo, cantidad e importe—; verificar manualmente que un mapeo equivocado se aprecia antes de confirmar
- [ ] 6.2 Implementar la pantalla de mapeo: columnas del fichero a la izquierda, campos del movimiento a la derecha, con la propuesta rellenada cuando la haya; verificar manualmente el mapeo manual completo sin IA
- [ ] 6.3 Mostrar en esa pantalla qué se envía al servicio de IA antes de pedir una propuesta; verificar manualmente que el aviso aparece y es comprensible
- [ ] 6.4 Implementar la gestión de perfiles: listado, edición y creación de versión; verificar manualmente que editar un perfil no altera los movimientos ya importados
- [ ] 6.5 Mostrar el perfil y la versión aplicados en el detalle de una importación y en el de un movimiento; verificar manualmente la trazabilidad de una cifra hasta el perfil que la interpretó

## 7. Validación de extremo a extremo

- [ ] 7.1 Importar un CSV de una plataforma no soportada hasta ahora usando solo la propuesta de IA y la pantalla de mapeo; verificar que los movimientos entran correctamente sin haber escrito código
- [ ] 7.2 Repetir la importación anterior; verificar que no se llama al servicio de IA y que todo se descarta como duplicado
- [ ] 7.3 Corregir el perfil de esa plataforma y reimportar; verificar que solo entran los movimientos que la corrección altera y que los anteriores conservan su versión
