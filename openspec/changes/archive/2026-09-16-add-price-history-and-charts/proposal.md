## Why

Kapea sabe lo que vale la cartera hoy y lo que costó, pero no guarda ningún precio de ayer. Sin serie temporal no hay evolución: no se puede decir si el patrimonio sube o baja, ni desde cuándo, ni qué activo arrastra al resto. Toda la fase de análisis —interpretación de gráficas, sentimiento y umbrales de compra y venta— se apoya en tener esa serie, así que es lo primero.

Además, las capas gratuitas de los proveedores limitan por minuto. Reconstruir la historia cada vez que alguien abre una pantalla agotaría la cuota y dejaría sin precio a quien esté mirando, de modo que el histórico hay que traerlo una vez y guardarlo.

## What Changes

- El sistema guarda el **precio diario de cierre** de cada activo con posición abierta, desde su primera adquisición hasta hoy, y lo completa cada día sin volver a pedir lo que ya tiene.
- Se reconstruye el **valor de la cartera día a día** combinando los movimientos con esos precios: cuántas unidades había cada día y cuánto valían.
- Se añaden **gráficas** a la aplicación: evolución del patrimonio, evolución de cada posición y reparto por clase de activo a lo largo del tiempo.
- Se calculan **indicadores técnicos** sobre la serie de un activo —media móvil simple y exponencial, y RSI— como base de las fases siguientes.
- Un día sin precio se dice, no se interpola: la gráfica muestra el hueco en lugar de inventar una línea recta.

## Capabilities

### New Capabilities

- `price-history`: la serie diaria de precios de un activo, cómo se trae, cómo se guarda y qué pasa con los días que faltan.
- `portfolio-history`: el valor de la cartera a lo largo del tiempo, reconstruido de los movimientos y los precios.
- `technical-indicators`: medias móviles y RSI sobre una serie de precios.

### Modified Capabilities

- `market-prices`: el proveedor de precios pasa a ofrecer también series históricas, no solo el precio de ahora.

## Impact

- **Domain**: el cálculo de la serie de cartera y los indicadores, como funciones puras sobre movimientos y precios.
- **Infrastructure**: una tabla de precios diarios por activo y fecha, la descarga del histórico contra CoinGecko y Yahoo, y un trabajo que completa lo que falte.
- **Api**: endpoints para la serie de cartera, la de un activo y sus indicadores.
- **Client**: una pantalla de evolución con sus gráficas, y la gráfica de un activo desde su posición.
- **Sin librerías de pago.** Las gráficas se dibujan con SVG propio o con una librería de licencia permisiva; la decisión se toma en el diseño.
- El volumen es pequeño: doce activos abiertos y algo más de año y medio de historia son unos pocos miles de filas.
