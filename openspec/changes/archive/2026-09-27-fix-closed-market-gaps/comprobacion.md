# Comprobación con los datos reales

Hecha el 26 de septiembre de 2026.

## Los días que el usuario reportó

Del 2 al 6 de julio de 2026, antes y después:

| Día | | Antes | Después |
|---|---|---|---|
| jueves 2 | | 3.527,27 · completo | 3.527,27 · completo |
| viernes 3 | fiesta del 4 de julio en EE. UU. | **3.166,94 · incompleto** | **3.605,40 · arrastrado** |
| sábado 4 | | **3.115,38 · incompleto** | **3.641,72 · arrastrado** |
| domingo 5 | | **3.122,36 · incompleto** | **3.648,70 · arrastrado** |
| lunes 6 | | 3.660,96 · completo | 3.660,96 · completo |

Ni un día incompleto en ese tramo, y el desplome de 360 € desaparece.

## La clase no bastaba: hubo que afinar a mercado

La primera implementación agrupaba por clase de activo y arregló el sábado y el domingo,
pero **no el viernes 3**. Ese día cerró la bolsa estadounidense por la fiesta del 4 y la
alemana operó: mirando toda la renta variable junta, el día parecía abierto y a las
estadounidenses les faltaba el dato.

El diseño ya anticipaba que habría que afinar a mercado «si algún día conviven bolsas con
calendarios muy distintos». Conviven desde que hay acciones alemanas y estadounidenses.

El mercado se lee del sufijo del símbolo canónico, que es el del bróker: `NOW.US` cotiza
en Estados Unidos y `VVSM.DE` en Alemania. No es un dato nuevo, es leer el que ya había.

## Días incompletos en todo el histórico

| | Antes | Después |
|---|---|---|
| Incompletos | 167 | **101** |
| Con precio arrastrado | — | 66 |

**Los 101 que quedan son una laguna de verdad, no fines de semana.** Son consecutivos
desde el 3 de junio de 2025, que es el primer movimiento de POL, hasta el 12 de
septiembre, que es su primer precio. A POL sólo lo cubre CoinGecko, cuya capa gratuita da
un año, así que de antes no hay dato y no lo habrá. Es exactamente lo que «incompleto»
debe significar.

## El rango completo

| | Antes | Después |
|---|---|---|
| Días devueltos con «Todo» | 3.653 | **511** |
| Primer día | 2016-09-26 | **2025-05-04**, el primer movimiento |

## Lo que no se ha movido

| | Esperado | Obtenido |
|---|---|---|
| Coste | 4.233,55 | 4.233,55 |
| Resultado realizado | 85,26 | 85,26 |
| Efectivo | 603,77 | 603,77 |
| Fiscal 2025 | −24,5475969 | −24,5475969 |
| Fiscal 2026 | 109,81002382 | 109,81002382 |
| Incoherencias | 0 | 0 |
| Sin clasificar | 0 | 0 |

Ninguna depende de precios de mercado, así que tenían que salir idénticas y salen.

## Un defecto ajeno que se ve al comprobarlo

El texto de los avisos bajo el título apenas se lee en tema oscuro: sale claro sobre
fondo claro. Le pasa igual al aviso que ya existía, así que no lo introduce este cambio.
Queda anotado para la mejora de las gráficas.
