# Cobertura del histórico para la cartera del usuario

Comprobado contra los proveedores el 12 de septiembre de 2026, para los doce activos
con posición abierta. Su primer movimiento es del 4 de mayo de 2025.

## Cubiertos del todo por Yahoo, en euros

`BTC`, `ETH`, `XRP`, `SOL`, `AVAX`, `LINK` y `DOGE`. Yahoo los cotiza como `BTC-EUR` y
devuelve cierres diarios desde antes del primer movimiento.

## Cubiertos solo el último año

`PAXG`, `B2M`, `PEPE`, `TAO` y `USDG`. Yahoo no los cotiza contra el euro, solo contra
el dólar (`PAXG-USD`, `PEPE24478-USD`, `TAO22974-USD`, `B2M-USD`, `USDG-USD`). CoinGecko
sí los da en euros, pero su capa gratuita solo llega 365 días atrás, o sea hasta el 13 de
septiembre de 2025.

Queda por tanto un hueco de unos cuatro meses, del 4 de mayo al 12 de septiembre de 2025.

## Lo que eso significa en la gráfica

`PAXG` pesa el 16 % de la cartera, así que durante esos cuatro meses el patrimonio
aparecería corto en esa proporción, y el día saldría marcado como incompleto. Los otros
cuatro juntos no llegan al 1 %.

## Resultado de la primera carga real

Ejecutada el 12 de septiembre de 2026. Los doce activos quedan cubiertos desde su
primera adquisición, con 5.024 precios guardados.

| Activo | Días | Desde | Origen |
|---|---|---|---|
| XRP | 496 | 2025-05-04 | Yahoo |
| B2M | 495 | 2025-05-05 | Yahoo+BCE hasta septiembre, CoinGecko después |
| SOL | 494 | 2025-05-06 | Yahoo |
| ETH | 490 | 2025-05-10 | Yahoo |
| BTC | 489 | 2025-05-11 | Yahoo |
| AVAX, LINK | 466 | 2025-06-03 | Yahoo |
| PAXG, USDG | 334 | 2025-10-13 | CoinGecko |
| DOGE | 332 | 2025-10-15 | Yahoo |
| PEPE | 317 | 2025-10-30 | CoinGecko |
| TAO | 311 | 2025-11-05 | CoinGecko |

El hueco que se temía no llegó a existir. Cuatro de los cinco tokens pequeños se
compraron en octubre o después, dentro de la ventana gratuita de CoinGecko, y el
único anterior, `B2M`, se rellenó desde el dólar con el tipo del BCE.
