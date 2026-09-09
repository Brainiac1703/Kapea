## Why

La pantalla de cartera actual enseña las posiciones abiertas con su coste, y poco más. Los ficheros con los que el usuario lleva su seguimiento hoy —una hoja de acciones y otra de cripto— muestran lo que de verdad se mira, y hay tres cosas que Kapea todavía no sabe responder:

- **Cuánto vale la cartera en total.** Su hoja lo calcula como posiciones más efectivo. Kapea no tiene el concepto de efectivo: sabe qué activos hay, no cuánto dinero queda sin invertir. Sin eso, la cifra que más se mira está incompleta.
- **Cuánto pesa cada activo.** Es la columna que le dice si está sobreexpuesto, y la base de cualquier decisión de rebalanceo.
- **Cuánto se ha ganado en total.** Su hoja suma el resultado realizado acumulado y el latente. Kapea tiene el realizado por ejercicio fiscal, que es otra pregunta.

Además, las acciones siguen sin precio de mercado porque nunca se eligió proveedor. Su script usa Yahoo Finance con una regla de conversión de símbolos que ya está probada contra sus datos.

Este change convierte la pantalla en la que se abre por costumbre: qué tengo, cuánto vale, cuánto he ganado y cómo está repartido.

## What Changes

- **Saldo de efectivo por cuenta**, calculado desde los movimientos igual que el resto de la cartera: ingresos, retiradas, compraventas, dividendos, intereses y comisiones. No se introduce a mano.
- **Una sola página de cartera**, agrupada por clase de activo, con subtotales por grupo y el total del patrimonio arriba. Añadir una clase nueva —fondos, planes— será un grupo más, no una pantalla más.
- **Peso de cada activo** sobre el total de la cartera, y peso de cada clase.
- **Resultado total**: el realizado acumulado de toda la vida de la cartera, el latente de las posiciones abiertas, y la suma.
- **Comisiones acumuladas** por activo y en total.
- **Rendimientos cobrados**: dividendos, intereses y recompensas, con su retención.
- **Precios de acciones** desde Yahoo Finance, con la conversión de símbolos del bróker al proveedor. Detrás de un puerto, como CoinGecko, para poder cambiarlo sin tocar nada más.
- **Actualización de precios** al abrir la página y con un botón, con la hora del precio siempre a la vista.
- **Corrección**: la tabla de identificadores de CoinGecko no cubre TAO, B2M ni PAXG, así que tres criptos del usuario se quedaban sin precio.

## Capabilities

### New Capabilities

- `cash-balance`: saldo de efectivo de cada cuenta, derivado de los movimientos.
- `market-prices`: obtención de precios de mercado por clase de activo, conversión de símbolos y comportamiento cuando no hay precio.

### Modified Capabilities

- `pnl-engine`: la posición abierta incorpora el peso en la cartera, las comisiones acumuladas y el resultado realizado acumulado del activo.

## Impact

- **Código**: cálculo de efectivo en el motor, proveedor de precios de renta variable, ampliación de las consultas de cartera y una pantalla nueva que sustituye a la actual.
- **Infraestructura**: ninguna. Yahoo Finance no exige clave.
- **Riesgo asumido**: Yahoo Finance no es una API oficial y puede dejar de funcionar sin aviso. Las especificaciones ya exigen que una posición sin precio se muestre igual, indicando que el valor no está disponible, así que un corte degrada la pantalla sin romperla.
- **Fuera de alcance**: gráficas de evolución, velas e indicadores técnicos, que son la fase 4 del roadmap. Y el planificador de aportaciones con porcentaje objetivo y rebalanceo, que merece su propio change.
