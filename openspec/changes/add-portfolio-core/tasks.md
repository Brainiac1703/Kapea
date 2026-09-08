## 1. Andamiaje de la solución

- [x] 1.1 Crear `global.json` (SDK 10.0.300, rollForward latestFeature), `Directory.Build.props` y `Directory.Packages.props` con `ManagePackageVersionsCentrally` y pinning transitivo; verificar que `dotnet --version` resuelve el SDK fijado
- [x] 1.2 Crear la solución `Kapea.slnx` con los proyectos `Domain`, `Application`, `Infrastructure`, `Api`, `Client`, `Shared` y `Sync` según design.md; verificar que `dotnet build` compila en limpio
- [x] 1.3 Crear un proyecto de test por capa (`Domain.Tests`, `Application.Tests`, `Infrastructure.Tests`, `Api.Tests`) con xUnit; verificar que `dotnet test` se ejecuta sin tests fallidos
- [x] 1.4 Añadir una prueba de arquitectura que falle si `Domain` referencia cualquier otro proyecto o si `Application` referencia `Infrastructure`; verificar que la prueba falla al introducir una referencia prohibida y pasa al quitarla
- [x] 1.5 Configurar los ficheros de recursos de localización (`es-ES` por omisión, `en` alternativo) y añadir un analizador o test que detecte literales de UI en el código; verificar con un literal de prueba

## 2. Modelo de dominio

- [x] 2.1 Implementar los tipos de valor `Money` (importe + divisa) y `Quantity` con aritmética `decimal`; verificar con tests que sumar dos `Money` de divisas distintas falla y que no hay pérdida de precisión en cripto (8+ decimales)
- [x] 2.2 Implementar `Asset` con clase (`Equity`/`Crypto`), símbolo canónico, ISIN opcional y marca de no verificado; verificar con tests de las invariantes del catálogo
- [x] 2.3 Implementar `PlatformAccount` (plataforma, alias, divisa base, `UserId`); verificar con test que rechaza el borrado cuando tiene movimientos
- [x] 2.4 Implementar `Transaction` con todos los campos de la spec, los tipos de movimiento soportados y la inmutabilidad de sus datos financieros; verificar con tests que un intento de mutación falla y que una fecha sin zona se almacena en UTC junto con la zona de origen
- [x] 2.5 Implementar `Lot` con cantidad original, cantidad restante, coste en EUR y fecha de adquisición; verificar con test que la cantidad restante nunca queda negativa
- [x] 2.6 Implementar `ManualAdjustment` como movimiento propio con motivo obligatorio; verificar con test que aparece en la trazabilidad diferenciado de los importados

## 3. Motor FIFO

- [x] 3.1 Implementar la función pura de cálculo (movimientos ordenados de un activo → lotes + resultados realizados) con desglose por lote consumido; verificar con tests de consumo parcial, consumo de varios lotes y agotamiento exacto
- [x] 3.2 Implementar el orden global por activo con desempate estable entre lotes de igual instante; verificar con test que dos ejecuciones sobre los mismos datos producen idéntico resultado y que lotes en cuentas distintas se consumen por fecha global
- [x] 3.3 Implementar el reparto proporcional de comisiones: las de adquisición al coste del lote, las de transmisión al importe repartidas entre lotes consumidos; verificar con tests de ambos casos
- [x] 3.4 Implementar los splits (ajuste de cantidad y coste unitario de lotes anteriores a la fecha efectiva, coste total y fecha de adquisición intactos); verificar con tests, incluido el split posterior a una venta ya calculada
- [x] 3.5 Implementar el tratamiento de dividendos como rendimiento del capital sin tocar lotes, con bruto y retención separados; verificar con tests
- [x] 3.6 Implementar la detección de venta sin lotes suficientes como inconsistencia explícita (activo, fecha, cantidad faltante); verificar con test que no se produce ningún resultado parcial silencioso
- [x] 3.7 Implementar la exclusión de movimientos `Unknown` y de traspasos pendientes de confirmar, con el recuento de pendientes en el resultado; verificar con test
- [x] 3.8 Añadir una batería de casos fiscales de referencia como tabla de datos (permuta cripto-cripto, compra en USD, venta que barre tres lotes con comisión, split retroactivo, dividendo con retención), con cifras esperadas calculadas a mano; verificar que todos pasan

## 4. Tipos de cambio

- [x] 4.1 Definir el puerto `IExchangeRateProvider` en `Application` y su tabla de tipos diarios en `Infrastructure`; verificar con test de integración sobre base de datos que la consulta por fecha y divisa devuelve el tipo esperado
- [x] 4.2 Implementar la descarga e ingesta de los tipos de referencia del BCE con reintentos; verificar contra una respuesta grabada que un rango de fechas se ingesta completo y que reingestar no duplica
- [x] 4.3 Implementar la sustitución por el último tipo publicado anterior cuando no hay publicación, dejando constancia en el movimiento; verificar con test sobre un fin de semana y un festivo
- [x] 4.4 Implementar la congelación del tipo aplicado, su fecha y su fuente junto al movimiento, y que el recálculo use el tipo guardado; verificar con test que alterar la tabla de tipos no cambia un resultado ya calculado

## 5. Persistencia

- [x] 5.1 Crear el `DbContext` con las configuraciones de EF Core: precisión decimal por columna (18 para cantidades, 8 para importes), `UserId` en toda entidad de cartera e índices por cuenta, activo y fecha; verificar con test que ninguna columna monetaria o de cantidad usa coma flotante
- [x] 5.2 Implementar el filtro global de consulta por `UserId` tomado del token; verificar con test que una consulta olvidando el filtro explícito no devuelve datos de otro usuario y que la búsqueda por identificador ajeno se comporta como inexistente
- [x] 5.3 Generar la migración inicial y aplicarla; verificar que la base de datos se crea desde cero y que el esquema coincide con el modelo
- [x] 5.4 Implementar la persistencia de lotes y resultados como proyección reemplazable por activo dentro de una transacción; verificar con test de integración que un recálculo sustituye la proyección completa sin dejar restos

## 6. Credenciales de broker

- [x] 6.1 Definir el puerto `ISecretStore` y sus implementaciones para Key Vault y para desarrollo local con User Secrets; verificar con tests de integración de alta, lectura y borrado
- [x] 6.2 Implementar la entidad de credencial con metadatos y referencia al secreto, y los DTOs del contrato API sin ningún campo capaz de transportar el secreto; verificar con test que la serialización de la respuesta no contiene el secreto
- [x] 6.3 Implementar el alta con verificación contra la plataforma y el rechazo de credenciales con permisos de trading o retirada; verificar con tests sobre respuestas grabadas de credencial válida, rechazada y con permisos excesivos
- [x] 6.4 Implementar rotación y revocación conservando el histórico y los movimientos importados; verificar con tests que una credencial revocada queda excluida de las sincronizaciones
- [x] 6.5 Implementar el marcado automático como inválida ante un rechazo durante la sincronización, sin afectar a otras plataformas; verificar con test que las demás cuentas siguen sincronizando
- [x] 6.6 Añadir un test que recorra los mensajes de log emitidos en los flujos de credenciales y falle si alguno contiene el secreto, entero o truncado

## 7. Motor de importación

- [x] 7.1 Definir los puertos `IFileImportAdapter` e `IApiImportAdapter`, el tipo de registro normalizado común y el registro con clave por plataforma en el contenedor; verificar con test que una plataforma sin adaptador registrado se rechaza indicando las soportadas
- [x] 7.2 Implementar `ImportRun` con su ciclo de vida, recuentos (leídos, importados, duplicados, rechazados) y consulta de historial y detalle; verificar con tests de integración
- [x] 7.3 Implementar la fase de staging: normalización y clasificación en importable, duplicado y rechazado, sin escribir en las tablas de dominio; verificar con test que abandonar el staging no deja nada persistido
- [x] 7.4 Implementar la huella de deduplicación en dos niveles (identificador natural del origen, o datos financieros más número de fila) y su índice único por cuenta; verificar con tests de reimportación del mismo fichero, periodos solapados y dos operaciones legítimamente idénticas
- [x] 7.5 Implementar la atomicidad de la confirmación y el registro de rechazados con su contenido original y motivo; verificar con test que un fallo a mitad no deja movimientos persistidos y que los rechazados no abortan la ejecución
- [x] 7.6 Implementar el reproceso de registros rechazados de una ejecución; verificar con test que los normalizables se importan y los que siguen fallando conservan el rechazo con motivo actualizado
- [x] 7.7 Implementar la conservación íntegra del registro de origen junto al movimiento y la consulta de trazabilidad movimiento → ejecución → registro original; verificar con test de integración
- [x] 7.8 Implementar la eliminación de una ejecución con recálculo posterior y su bloqueo cuando hay dependencias; verificar con tests de ambos casos

## 8. Detección de traspasos internos

- [x] 8.1 Implementar el emparejamiento candidato entre salida y entrada del mismo activo en cuentas del usuario, con ventana temporal y tolerancia configurables (72 h y 2 % por defecto); verificar con tests dentro y fuera de ventana y de tolerancia
- [x] 8.2 Implementar la confirmación: traslado de lotes a la cuenta de destino conservando coste y fecha de adquisición, sin generar resultado realizado; verificar con test que el P&L no cambia tras confirmar un traspaso
- [x] 8.3 Implementar el rechazo persistente de una propuesta para que no se vuelva a ofrecer; verificar con test
- [x] 8.4 Implementar el registro de la diferencia por comisión de red como comisión del traspaso; verificar con test de cantidad recibida inferior a la enviada

## 9. Adaptador XTB (fichero)

- [x] 9.1 Implementar la detección de formato por cabeceras con fallo total explícito que enumere columnas esperadas y encontradas; verificar con tests de cabeceras desconocidas, columna opcional ausente y columnas adicionales
- [x] 9.2 Implementar el parser de Excel y CSV con las convenciones locales (separador decimal, separador de columnas, formato de fecha, divisa); verificar con test que la convención decimal europea se interpreta sin pérdida de precisión
- [x] 9.3 Implementar el mapeo de conceptos de XTB a los tipos de movimiento normalizados, con `Unknown` para lo no reconocido y descarte de apuntes informativos sin efecto financiero; verificar con tests
- [ ] 9.4 Implementar el endpoint de subida y el flujo de vista previa y confirmación, incluida la validación de que la cuenta destino es de plataforma `XTB` y el rechazo de tipos de fichero no admitidos; verificar con tests de integración de la API
- [ ] 9.5 Incorporar ficheros de exportación reales como casos de test de extremo a extremo con cifras esperadas; verificar que la importación completa produce los movimientos esperados

## 10. Adaptadores de API (Kraken y Bit2Me)

- [x] 10.1 Implementar el cliente de Kraken con firma de peticiones, paginación y espera creciente ante límite de frecuencia; verificar contra respuestas grabadas que un histórico paginado se recorre completo y que el límite de frecuencia se reintenta
- [x] 10.2 Implementar la normalización de símbolos, alias históricos y pares de negociación de Kraken; verificar con tests, incluido el símbolo no traducible que crea el activo como no verificado
- [x] 10.3 Implementar el cliente de Bit2Me con paginación, espera creciente y cobertura de todos los productos legibles, dejando constancia de los omitidos; verificar contra respuestas grabadas
- [x] 10.4 Implementar las conversiones directas de Bit2Me como venta y compra enlazadas con valoración en EUR en la fecha; verificar con test que el FIFO trata la permuta como hecho imponible
- [x] 10.5 Implementar la normalización de rendimientos y recompensas a `Reward`/`Interest`; verificar con tests
- [x] 10.6 Implementar el fallo de la carga inicial ante interrupción de la paginación, sin persistir nada y sin avanzar el instante de última importación correcta; verificar con test para ambos adaptadores

## 11. Sincronización programada

- [x] 11.1 Implementar el proceso temporizado de `Kapea.Sync` que recorre las cuentas con credencial activa e importa desde el instante de la última importación correcta; verificar con test de integración que la segunda ejecución solicita solo el periodo posterior
- [x] 11.2 Implementar el bloqueo de ejecuciones solapadas por cuenta, dejando constancia de la omisión; verificar con test que una segunda ejecución concurrente no se ejecuta
- [x] 11.3 Implementar el aislamiento de fallos entre plataformas: una que falla no impide sincronizar el resto; verificar con test
- [x] 11.4 Configurar Application Insights y las trazas en español de inicio, fin y resultado de cada sincronización; verificar que una ejecución local emite las trazas esperadas

## 12. Precios de mercado y posiciones

- [x] 12.1 Definir el puerto `IMarketPriceProvider` e implementarlo contra CoinGecko para cripto, con caché y degradación ante indisponibilidad; verificar contra respuestas grabadas
- [x] 12.2 Implementar el cálculo de posiciones abiertas (cantidad y coste medio siempre; valor actual, resultado latente e instante del precio solo si hay precio); verificar con tests de posición con precio, sin precio y activo totalmente vendido

## 13. API y cliente

- [ ] 13.1 Configurar la autenticación con Entra External ID en la API y la extracción del `UserId` del token, sin aceptarlo nunca como parámetro de entrada; verificar con tests que una petición sin token o con token de otro usuario no accede a los datos
- [ ] 13.2 Implementar los endpoints de cuentas, credenciales, importaciones, movimientos, posiciones y resultados con sus comandos y consultas MediatR y su validación FluentValidation; verificar con tests de integración de la API
- [ ] 13.3 Implementar en el cliente Blazor WebAssembly las pantallas de cuentas y credenciales; verificar manualmente el alta, la rotación y la revocación contra la API
- [ ] 13.4 Implementar en el cliente la subida de fichero XTB con vista previa y confirmación, y el historial de importaciones con el detalle de rechazados; verificar manualmente el flujo completo con un fichero real
- [ ] 13.5 Implementar en el cliente la revisión de traspasos propuestos y de movimientos sin clasificar; verificar manualmente confirmación y rechazo
- [ ] 13.6 Implementar en el cliente las vistas de posiciones abiertas y de resultados realizados por ejercicio, con la advertencia visible cuando haya movimientos sin clasificar y el desglose de un resultado hasta sus lotes y movimientos; verificar manualmente la trazabilidad de una cifra hasta la fila de origen

## 14. Validación del núcleo

- [ ] 14.1 Importar el histórico real de las tres plataformas y contrastar los resultados de un ejercicio ya declarado contra la declaración presentada; documentar las diferencias y su causa en una nota junto al change
- [ ] 14.2 Corregir las discrepancias encontradas y añadir cada una como caso de test de referencia; verificar que la batería completa pasa
- [ ] 14.3 Ejecutar un recálculo completo desde cero sobre el histórico real y comprobar que reproduce exactamente los mismos lotes, posiciones y resultados
