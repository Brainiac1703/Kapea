# Validación con los datos reales del usuario

Contraste hecho el 11 y el 12 de septiembre de 2026 sobre su histórico de Bit2Me y
Kraken. La renta variable queda pendiente: no hay ningún movimiento real de XTB
importado todavía.

## Coste de las posiciones abiertas

Su anotación daba 4.850 € invertidos y Kapea 4.838,17 €. Las cinco diferencias
tienen causa y ninguna es un error de cálculo.

| Activo | Suyo | Kapea | Diferencia | Causa |
|---|---|---|---|---|
| ETH | 1.150 | 1.124,73 | −25,27 | Vendió POL y ATOM por 75,54 € y compró ETH por 74,72 €. Su nota arrastra los 100 € originales |
| B2M | 50 | 72,13 | +22,13 | Recompensas entregadas en especie, valoradas a lo que valían al cobrarlas |
| XRP | 1.050 | 1.045,61 | −4,39 | Vendió XDC por 45,84 € de los 50 € que puso, y lo reinvirtió en XRP |
| PAXG | 600 | 597,60 | −2,40 | Pagó 591,68 de compras más 5,92 de comisión |
| SOL | 350 | 348,10 | −1,90 | Vendió EURC por 98,11 € de los 100 € que puso, y lo reinvirtió en SOL |

Su cifra es el dinero que asignó a cada moneda; la de Kapea es lo que costó lo que
todavía tiene, con las pérdidas de las permutas ya descontadas.

## Efectivo

Salía a −120,75 €, imposible. Dos causas, las dos corregidas:

- 121 € de compras pagadas con Apple Pay. Bit2Me manda un solo registro para el
  cobro y la compra, mientras que un ingreso por transferencia llega como dos. La
  compra gastaba un efectivo que nunca entró en la cuenta.
- Permutas y comisiones de red contadas como movimientos de dinero.

Tras corregirlo el efectivo queda en 0,00 € en Kraken y 0,01 € en Bit2Me, que es
el redondeo de la venta de EURC.

## Comisiones

Solo aparecían en PAXG. Bit2Me no manda ninguna comisión en sus movimientos de
monedero: la cobra como diferencial, dando peor cambio que el publicado. Se deduce
comparando lo pagado con ese cambio, y salen 52,20 € en total, alrededor del 0,95 %
de cada compra, que es lo que dice la columna del fichero exportado.

## Pendiente

- El histórico de XTB, cuando el usuario lo exporte.
- Contrastar el valor de mercado y los pesos contra lo que muestran las propias
  plataformas.
