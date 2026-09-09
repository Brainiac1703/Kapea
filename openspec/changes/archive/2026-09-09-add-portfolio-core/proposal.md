## Why

Las inversiones (acciones y criptomonedas) están repartidas entre varias plataformas —XTB, Kraken y Bit2Me— y hoy no existe ninguna visión consolidada: calcular el beneficio o la pérdida real de una posición exige cruzar a mano exportaciones con formatos distintos, divisas distintas y criterios de coste distintos. Ese trabajo manual se repite cada ejercicio fiscal y es donde se cometen los errores más caros.

Este change construye el núcleo determinista y auditable de Kapea: un modelo de dominio de cartera, la importación de movimientos desde las tres plataformas y un motor de P&L con criterio FIFO en EUR. Es la base sobre la que se apoyan después los informes fiscales, el dashboard y el módulo de señales; sin ella ninguno de los tres puede existir. Corresponde a las fases 0 y 1 del roadmap de la propuesta inicial.

## What Changes

- **Repositorio greenfield**: se crea la solución `.slnx` con la arquitectura por capas del equipo (Domain, Application con CQRS, adaptador de persistencia EF Core, Web API) y el proyecto cliente Blazor WebAssembly. Todavía no existe código.
- **Modelo de dominio de cartera**: activos (acciones y cripto), divisas, cuentas por plataforma, movimientos normalizados y lotes FIFO como entidad de primer nivel. Esquema con `UserId` desde el inicio aunque solo haya un usuario.
- **Importación como adaptadores**: un contrato único de importación con dos formas de origen —fichero subido y API remota— de modo que añadir una plataforma futura no toque el núcleo.
- **Adaptador XTB (fichero)**: parser de la exportación Excel/CSV de xStation5, tolerante a cambios de formato y con rechazo explícito y diagnosticable cuando el fichero no encaja.
- **Adaptadores Kraken y Bit2Me (API)**: sincronización periódica server-side con API keys de solo lectura, ejecutada por un trigger temporizado, nunca desde el cliente WebAssembly.
- **Custodia de credenciales de broker**: las API keys se guardan cifradas fuera del cliente y no se devuelven nunca por la API; el cliente solo ve metadatos (plataforma, alias, estado, última sincronización).
- **Idempotencia y reconciliación**: reimportar el mismo periodo no duplica movimientos, y los traspasos entre cuentas propias se detectan y se marcan como no imponibles en lugar de contarse como venta y compra.
- **Motor de P&L FIFO**: consumo de lotes por orden de adquisición, conversión a EUR al tipo de la fecha de cada operación, tratamiento de splits y dividendos, y cálculo de posiciones abiertas y resultados realizados.
- **Trazabilidad**: toda cifra calculada es reconducible hasta los movimientos y lotes que la originan. Requisito derivado de la decisión de que el informe fiscal sea presentable a una gestoría.
- **Sin IA en el cálculo**: la capa de IA descrita en la propuesta (normalización de formatos, clasificación de movimientos ambiguos) queda fuera de este change. El núcleo financiero es determinista por diseño.

## Capabilities

### New Capabilities

- `portfolio-domain`: modelo de cartera —activos, divisas, cuentas por plataforma, movimientos normalizados, lotes FIFO— y las invariantes que los gobiernan.
- `broker-credentials`: alta, custodia cifrada, rotación y revocación de las credenciales de acceso a las plataformas, sin exposición al cliente.
- `transaction-import`: contrato de adaptador de importación, ciclo de vida de una ejecución de importación, idempotencia, deduplicación y detección de traspasos internos.
- `transaction-import/xtb-file`: importación desde la exportación de fichero de XTB (xStation5).
- `transaction-import/kraken`: importación e histórico vía API REST de Kraken.
- `transaction-import/bit2me`: importación e histórico vía API REST de Bit2Me.
- `pnl-engine`: cálculo FIFO de coste y resultado, conversión multidivisa a EUR, eventos corporativos, posiciones abiertas y resultados realizados por ejercicio.

### Modified Capabilities

Ninguna: `openspec/specs/` está vacío, este es el primer change con specs del proyecto.

## Impact

- **Código**: crea toda la estructura de la solución. No hay código existente que romper.
- **Infraestructura Azure**: base de datos relacional, Key Vault para las API keys, una función temporizada para la sincronización y Application Insights. El hosting y la autenticación (Entra External ID) se asumen provisionados; su configuración detallada no forma parte de este change.
- **Dependencias externas**: APIs de Kraken y Bit2Me (contratos fuera de nuestro control) y el formato de exportación de XTB, que la propuesta advierte que cambia sin aviso.
- **Fuera de alcance**: informes fiscales y Modelo 720/721 (fase 2), dashboard y gráficas (fase 3), indicadores técnicos y backtesting (fase 4), IA y multiusuario real (fase 5). Un proveedor de precios de mercado solo se necesita para valorar posiciones abiertas; ese punto queda abierto y se acota en el diseño.
