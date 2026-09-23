## Why

Al vender PAXG el 13/09/2026 la posición siguió apareciendo abierta y con resultado realizado cero. La causa: cinco permutas de cripto por cripto hechas en Kraken en noviembre y diciembre de 2025 (ETH, USDG, DOGE, PEPE y BTC cambiados por PAXG) se importaron como diez traspasos sueltos, sin restar del activo entregado ni crear lote del recibido. Al vender faltaban 0,00374501 PAXG, y el cálculo descartó la venta entera.

Lo grave no es el descuadre, es que nadie avisó: el motor sí anota esa incoherencia, pero la cartera la tira antes de mostrarla y presenta las cifras como si estuvieran completas. Las especificaciones ya exigen ambas cosas —valorar en euros las dos patas de una permuta entre criptomonedas y no producir un resultado parcial silencioso—, así que esto es incumplimiento, no funcionalidad nueva.

De paso se arregla el filtro de tipos de la pantalla de Movimientos, que el usuario reportó en el mismo mensaje.

## What Changes

- El adaptador de Kraken reconoce la permuta de un activo por otro: los apuntes `spend` y `receive` que comparten referencia y en los que ninguna pata es dinero dejan de importarse como dos traspasos y pasan a ser las dos patas de una permuta, una transmisión y una adquisición que no mueven la caja, igual que ya hace el adaptador de Bit2Me.
- Las permutas se valoran en euros con el precio de cierre del día, tomado del histórico de precios que ya se guarda para las gráficas. Esa valoración queda marcada como estimada: visible para el usuario y corregible desde la propia aplicación. Sin precio para ese día, el movimiento queda pendiente de revisión en lugar de inventar una cifra.
- Las incoherencias del cálculo llegan a la cartera y se muestran. Mientras haya una, las cifras se presentan como incompletas, como ya ocurre con los movimientos sin clasificar.
- Los movimientos ya guardados se corrigen. Releer el histórico no basta: las patas ya existen y se descartan por duplicado.
- Meter un activo en Earn o recuperarlo deja de importarse. No es una compra, ni una venta, ni un traspaso entre cuentas: es mover algo entre bolsillos de la misma cuenta. Se cuenta entre los registros descartados por no tener efecto, y las recompensas cobradas siguen entrando como renta.
- El filtro de tipos de la pantalla de Movimientos se muestra en el idioma de la aplicación y admite varios tipos a la vez.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `transaction-import`: un adaptador de API debe emitir las dos patas de una permuta entre activos cuando la plataforma la registra como dos apuntes con una misma referencia, debe poder valorarlas con el histórico de precios cuando la plataforma no las valora, y no debe importar el paso de un activo a un producto de rendimiento de la propia plataforma.
- `pnl-engine`: las incoherencias que detecta el cálculo deben llegar a quien consulta la cartera, no quedarse en el motor.
- `portfolio-domain`: un importe valorado por estimación debe distinguirse de uno tomado del origen y poder corregirse; la lista de movimientos debe permitir filtrar por varios tipos a la vez y mostrarlos en el idioma de la aplicación.

## Impact

- `src/Kapea.Infrastructure/Import/Kraken/KrakenImportAdapter.cs`: emparejamiento de permutas sin pata en dinero, y descarte de los pasos a rendimiento.
- `src/Kapea.Infrastructure/Import/Bit2Me/Bit2MeImportAdapter.cs`: descarte de las dos caras del paso a Earn, conservando las recompensas.
- `src/Kapea.Application/Import/`: `ImportRecord`, `StagedPayload` e `ImportPipeline`, para la valoración estimada.
- `src/Kapea.Domain/Transactions/Transaction.cs` y su configuración de EF Core: marca de importe estimado, con migración.
- `src/Kapea.Infrastructure/Persistence/Stores/PortfolioQueries.cs`: dejar de pasar la lista de incoherencias vacía.
- `src/Kapea.Application/Portfolio/PortfolioCalculationService.cs` y las proyecciones: conservar las incoherencias del recálculo.
- `src/Kapea.Client/Pages/Transactions.razor`, `src/Kapea.Client/Pages/Portfolio.razor` y los recursos de localización.
- `src/Kapea.Application/Portfolio/PortfolioQueries.cs` (`TransactionQuery`), `src/Kapea.Api/Endpoints/PortfolioEndpoints.cs` y `KapeaApiClient`: filtro de tipos multivalor.
- Una migración que borre las permutas mal importadas y los pasos a rendimiento ya guardados, en local y en producción.
