# Comprobación con los datos reales

Hecha el 27 de septiembre de 2026 sobre los 24 activos del usuario y 54.000 precios.

## Los nueve periodos por los tres intervalos

Sobre MSTR.US, que es el más largo: 6.723 cotizaciones desde el 3 de enero de 2000.
Tiempo de respuesta del servidor y tamaño de lo que viaja.

| Periodo | Días | Semanas | Meses |
|---|---|---|---|
| 1 semana | 8 pts · 130 ms | 1 · 101 ms | 1 · 98 ms |
| 1 mes | 31 · 100 ms | 5 · 108 ms | 2 · 97 ms |
| 3 meses | 91 · 99 ms | 13 · 100 ms | 4 · 102 ms |
| 6 meses | 181 · 100 ms | 26 · 112 ms | 7 · 110 ms |
| Año en curso | 271 · 113 ms | 39 · 99 ms | 10 · 118 ms |
| 1 año | 366 · 101 ms | 52 · 109 ms | 13 · 112 ms |
| 2 años | 731 · 113 ms | 105 · 123 ms | 25 · 111 ms |
| 5 años | 1.826 · 142 ms | 261 · 145 ms | 61 · 142 ms |
| **Todo** | **9.765 · 254 ms** | 1.395 · 255 ms | 321 · 220 ms |

Las 27 combinaciones responden. El caso más pesado —todo en días— son **4 MB** de
respuesta, y se dibuja: la gráfica reduce a mil puntos lo que pinta, así que el navegador
no tiene que vérselas con nueve mil.

Las tres formas aparecen donde deben. Un año en semanas son 52 tramos y salen **velas**;
un año en días son 366 y sale la **banda** con la línea del cierre, porque por encima de
ciento ochenta la vela se queda en una raya. Todo en días, también banda.

## Los que no tienen recorrido

| Activo | Días | Recorrido | Apertura y extremos del tramo | Cierre |
|---|---|---|---|---|
| POL | 55 | no | no se enseñan | 0,105578 € |
| PEPE | 53 | no | no se enseñan | 0,00000388 € |
| TAO | 53 | no | no se enseñan | 289,29 € |
| HYPE | 53 | no | no se enseñan | 81,67 € |

Los cuatro los cubre sólo CoinGecko, cuya capa gratuita no da apertura, máximo ni mínimo.
La pantalla lo dice: «Este activo no tiene máximos ni mínimos: su proveedor sólo da el
cierre de cada día», dibuja la línea sola y omite las cifras del tramo, que saldrían de
agregar cierres y contradirían el aviso. El cierre y las cifras del periodo sí salen,
porque de ésas sí hay dato.

El eje de PEPE, cuyo precio es 0,00000382 €, se lee: **0,00000835 · 0,00000626 ·
0,00000418 · 0,00000209**. Antes decía «0» cinco veces.

## Cuántos activos tienen recorrido

**20 de 24**, y **51.664 de 54.000 precios**.

Los cuatro que no son los de arriba. De los otros veinte, a algunos les faltan unas
decenas de días sueltos en que Yahoo no dio los extremos o los dio incoherentes: esos
días conservan su cierre, que es lo previsto.

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

Este cambio sólo añade tres columnas que nadie usa para valorar y cambia cómo se enseña
lo que ya había, así que tenían que salir idénticas y salen.
