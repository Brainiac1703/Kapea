## Context

Repositorio vacío: este change levanta la solución entera además de implementar el núcleo. Ver `proposal.md — Why` para la motivación y los ficheros bajo `specs/` para los requisitos.

Restricciones que condicionan el diseño:

- **El cliente es Blazor WebAssembly**, decidido en la propuesta inicial. Corre en el navegador y no puede custodiar secretos, así que la Web API ASP.NET Core no es opcional: es la única pieza que ve las API keys de los brokers.
- **Convenciones del equipo** (heredadas de legacy-lens): .NET 10, solución `.slnx`, versiones centralizadas, arquitectura por capas con las dependencias apuntando hacia dentro, CQRS con MediatR y FluentValidation en Application, EF Core en el adaptador de persistencia.
- **Ritmo real: ~1 día/semana.** El diseño prioriza que cada tarea deje algo verificable y que el núcleo determinista esté cubierto por tests antes de tocar la UI.
- **El cálculo fiscal es crítico.** La decisión de que el informe sea presentable a una gestoría convierte la trazabilidad en requisito estructural, no en una mejora posterior: hay que poder recorrer cualquier cifra hasta la fila del fichero o el registro de la API que la originó.
- **Los orígenes son inestables.** El formato de exportación de XTB cambia sin aviso y las APIs de Kraken y Bit2Me están fuera de nuestro control.

## Goals / Non-Goals

**Goals:**

- Un núcleo de cálculo puro, sin dependencias de infraestructura y cubierto por tests, que sea la referencia de corrección del proyecto.
- Un límite de extensión claro para añadir plataformas: un adaptador nuevo no toca el núcleo.
- Reproducibilidad: mismos movimientos, mismo resultado, hoy y dentro de tres ejercicios.
- Estructura de solución y esquema de datos que aguanten las fases 2 a 5 sin migración de datos.

**Non-Goals:**

- Rendimiento a escala: un usuario con miles —no millones— de movimientos. Se elige la claridad sobre la optimización mientras no haya una medición que obligue a lo contrario.
- Multiusuario real: el esquema lleva `UserId` y la autorización filtra por él, pero no hay gestión de usuarios, roles ni facturación.
- Infraestructura como código y pipeline de despliegue.
- Cualquier uso de IA: el núcleo es determinista por diseño.

## Decisions

### Estructura de la solución

```
Kapea.slnx
  src/Kapea.Domain              — entidades, invariantes, cálculo FIFO. Sin dependencias.
  src/Kapea.Application         — CQRS (MediatR), validación, puertos (interfaces) de importación,
                                  tipos de cambio y almacén de secretos.
  src/Kapea.Infrastructure      — EF Core + SQL Server, adaptadores de broker, Key Vault, tipos de cambio.
  src/Kapea.Api                 — Web API: endpoints, autenticación, composición.
  src/Kapea.Client              — Blazor WebAssembly.
  src/Kapea.Shared              — DTOs del contrato API, compartidos entre Api y Client.
  src/Kapea.Sync                — proceso programado de sincronización (Azure Functions, timer trigger).
  tests/…                       — un proyecto de test por capa.
```

`Kapea.Sync` se separa de `Kapea.Api` porque su ciclo de vida es distinto (ejecución programada, sin tráfico entrante) y porque conviene que la sincronización pueda fallar sin arrastrar a la API. Comparte Application e Infrastructure.

**Alternativa descartada** — un solo proyecto de servidor con un `BackgroundService`: más simple de arrancar, pero acopla el escalado de la API al de la sincronización y complica aislar fallos de la parte que depende de terceros. La propuesta ya elige Azure Functions.

### El motor FIFO vive en Domain y es una función pura

El cálculo recibe la lista ordenada de movimientos de un activo y devuelve lotes y resultados realizados. No conoce EF Core, ni fechas del sistema, ni proveedores de tipos de cambio: los tipos de cambio le llegan ya resueltos junto a cada movimiento.

Esto es lo que hace posible el requisito de recálculo determinista: se puede probar exhaustivamente con tablas de casos, incluidos los escenarios fiscales incómodos (permutas cripto-cripto, splits retroactivos, ventas que barren varios lotes con comisión repartida), sin base de datos ni red.

**Alternativa descartada** — calcular incrementalmente en la capa de persistencia según entran los movimientos: más rápido, pero un movimiento retroactivo obliga a deshacer estado ya escrito, que es exactamente donde aparecen los errores caros.

### Recálculo completo por activo, no incremental

Cuando entran movimientos nuevos, se recalculan desde cero todos los lotes y resultados de los activos afectados y se reemplazan en un único cambio transaccional. Con volúmenes de un usuario particular el coste es despreciable y elimina de raíz toda una clase de bugs de estado desincronizado. Los lotes y resultados persistidos son, por tanto, una proyección: la fuente de verdad son los movimientos.

**Alternativa descartada** — recálculo incremental desde la fecha del movimiento retroactivo: correcto pero con más aristas, y sin ganancia medible a esta escala. Si algún día hace falta, la frontera ya está en el sitio adecuado.

### Los movimientos son inmutables; las correcciones son movimientos nuevos

Editar un movimiento importado destruiría la trazabilidad hasta el origen. Un ajuste manual es un movimiento propio, identificado como tal y con motivo. Deshacer una importación es eliminar su ejecución completa.

### Huella de deduplicación en dos niveles

Se prefiere el identificador natural del origen (`txid` de Kraken, identificador de operación de Bit2Me). Cuando el origen no lo aporta —el caso de los ficheros de XTB— la huella se calcula sobre cuenta, tipo, activo, cantidad, precio, divisa, comisión e instante.

Esto tiene una consecuencia aceptada: dos operaciones legítimamente idénticas en el mismo instante y misma cuenta colapsarían en una sola. La mitigación es incluir el número de fila dentro del fichero en la huella, lo que resuelve el caso normal a cambio de que reimportar el mismo fichero reordenado sí duplique. Se opta por la variante con número de fila y se avisa en la vista previa, porque perder una operación real es peor que un duplicado visible.

### Tipos de cambio: BCE, cacheados y congelados en el movimiento

Fuente: los tipos de referencia diarios del Banco Central Europeo, que es lo que la AEAT admite y lo que un asesor fiscal espera ver. Se descargan a una tabla propia y se sirven desde ahí; la red no interviene en el cálculo.

**El tipo aplicado se persiste junto al movimiento**, con su fecha y su fuente. Un recálculo posterior usa el tipo guardado, nunca vuelve a consultar la fuente. Sin esto, recalcular un ejercicio ya presentado podría dar cifras distintas a las declaradas: inaceptable.

Días sin publicación (festivos y fines de semana): se aplica el último tipo publicado anterior y se deja constancia en el movimiento.

### Los tipos monetarios son `decimal`, y la divisa viaja con el importe

Nada de `double` en ninguna parte del recorrido. Cantidades de activo con 18 decimales de escala (holgado para cripto), importes monetarios con 8, y redondeo a dos decimales solo al presentar. Un tipo `Money` que empareja importe y divisa evita el error clásico de sumar euros con dólares.

### Adaptadores: dos puertos, no uno

`IFileImportAdapter` (recibe un flujo y devuelve registros normalizados) e `IApiImportAdapter` (recibe una credencial y un rango temporal). Forzar un único puerto obligaría a inventar parámetros vacíos en ambos lados. Los dos entregan el mismo tipo de registro normalizado al motor de importación, que a partir de ahí no distingue el origen.

El adaptador se selecciona por la plataforma de la cuenta destino mediante registro con clave en el contenedor de dependencias.

### Staging antes de persistir

Toda importación pasa por una fase intermedia donde los registros ya están normalizados y clasificados —importable, duplicado, rechazado— pero nada se ha escrito en las tablas de dominio. XTB expone esa fase como vista previa; los adaptadores de API la confirman automáticamente. Un solo camino, dos formas de confirmarlo.

El registro de origen se conserva íntegro (fila cruda o respuesta JSON) junto al movimiento importado. Ocupa poco a esta escala y es lo que sostiene la trazabilidad frente a una gestoría.

### Detección de traspasos: propuesta, nunca automática

El emparejamiento se propone al usuario y no se aplica solo. Un falso positivo convertiría una venta real en un traspaso y borraría un hecho imponible del cálculo fiscal; un falso negativo solo genera ruido revisable. La asimetría de coste es clara.

Ventana temporal y tolerancia de cantidad son configurables, con valores por defecto de 72 horas y 2 % de diferencia por comisión de red. Las decisiones del usuario —confirmar o rechazar— se persisten para no volver a preguntar lo mismo.

### Secretos en Key Vault, referenciados desde la base de datos

La tabla de credenciales guarda metadatos y el nombre del secreto en Key Vault, nunca el secreto. El DTO del contrato API no tiene campo donde quepa un secreto, de modo que exponerlo por accidente exige un cambio de contrato deliberado. En desarrollo local, un almacén equivalente basado en User Secrets tras el mismo puerto.

### Precios de mercado: puerto definido, implementación mínima

El requisito de posiciones abiertas está redactado para funcionar sin precios (cantidad y coste medio siempre; valor y resultado latente solo si hay precio), así que la elección del proveedor —pregunta abierta nº 2 de la propuesta— no bloquea este change. Se define el puerto `IMarketPriceProvider` y se implementa contra CoinGecko para cripto, que tiene capa gratuita suficiente y no exige clave. La renta variable se queda sin precio en esta fase y las posiciones lo indican explícitamente; el proveedor definitivo se decide en la fase 3, cuando el dashboard lo exija de verdad.

### Autenticación desde el principio

Microsoft Entra External ID según la propuesta: la API valida el token y toda consulta filtra por el `UserId` extraído de él, con un filtro global en EF Core como red de seguridad frente al olvido en una consulta concreta. Ninguna operación acepta el `UserId` como parámetro de entrada.

## Risks / Trade-offs

- **El formato de XTB cambia sin aviso** → Detección de formato por cabeceras con fallo total y explícito, nunca importación parcial silenciosa. Ficheros de ejemplo reales como casos de test, para que un cambio de formato se manifieste como test roto y no como cifras mal calculadas.
- **Un error en el cálculo fiscal tiene consecuencias reales** → Motor puro con batería de tests por escenario; validación de los primeros resultados contra una declaración ya presentada antes de dar el módulo por bueno (tarea explícita en `tasks.md`).
- **Un falso positivo en la detección de traspasos borraría un hecho imponible** → Confirmación humana obligatoria; nunca aplicación automática.
- **Las APIs de terceros cambian o caen** → Los adaptadores devuelven registros normalizados, así que el impacto queda contenido en la capa de infraestructura; contract tests sobre respuestas grabadas detectan la deriva sin depender de la red.
- **Recálculo completo por activo** → Aceptado a esta escala. Si un día no basta, la frontera pura ya permite sustituirlo sin tocar el resto.
- **Colapso de operaciones idénticas al deduplicar ficheros sin identificador** → Mitigado con el número de fila en la huella; el recuento de duplicados se muestra en la vista previa para que un colapso inesperado sea visible antes de confirmar.
- **Blazor WebAssembly tiene mayor complejidad inicial** → Asumido y ya decidido en la propuesta. Se contiene manteniendo el cliente delgado: toda la lógica vive tras la API.

## Open Questions

- **Proveedor de precios para renta variable** (pregunta abierta nº 2 de la propuesta): se decide en la fase 3. El puerto `IMarketPriceProvider` aísla la elección y las specs ya contemplan la ausencia de precio.
- **Presupuesto mensual en Azure** (pregunta abierta nº 6): condiciona el dimensionamiento del hosting y de la base de datos, no el diseño del núcleo. Se puede resolver al desplegar.
- **Tratamiento fiscal fino de las recompensas de staking** (rendimiento del capital mobiliario frente a otras rentas): afecta a los informes de la fase 2, no al cálculo FIFO de este change. Se captura el movimiento con su tipo y su valoración en EUR, que es lo que cualquier criterio necesitará después. Conviene consultarlo con un asesor antes de la fase 2.
