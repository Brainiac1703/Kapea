# Validación con los datos reales del usuario

Cripto contrastada el 11 y el 12 de septiembre de 2026 sobre el histórico de Bit2Me
y Kraken; renta variable el 26 de septiembre, sobre el de XTB.

## Cripto

### Coste de las posiciones abiertas

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

### Efectivo

Salía a −120,75 €, imposible. Dos causas, las dos corregidas:

- 121 € de compras pagadas con Apple Pay. Bit2Me manda un solo registro para el
  cobro y la compra, mientras que un ingreso por transferencia llega como dos. La
  compra gastaba un efectivo que nunca entró en la cuenta.
- Permutas y comisiones de red contadas como movimientos de dinero.

Tras corregirlo el efectivo queda en 0,00 € en Kraken y 0,01 € en Bit2Me, que es
el redondeo de la venta de EURC.

### Comisiones

Solo aparecían en PAXG. Bit2Me no manda ninguna comisión en sus movimientos de
monedero: la cobra como diferencial, dando peor cambio que el publicado. Se deduce
comparando lo pagado con ese cambio, y salen 52,20 € en total, alrededor del 0,95 %
de cada compra, que es lo que dice la columna del fichero exportado.

## Renta variable

Contraste hecho el 26 de septiembre de 2026, ya con el histórico de XTB importado.
Las ocho posiciones están cerradas, así que no hay valor de mercado que contrastar:
lo que se compara es el coste, lo cobrado y el resultado.

| Activo | Vendido | Coste | Resultado |
|---|---|---|---|
| MSTR.US | 287,24 | 227,44 | +59,80 |
| NOW.US | 1.427,61 | 1.302,11 | +125,50 |
| VVSM.DE | 92,01 | 87,10 | +4,91 |
| MREO.US | 49,19 | 72,18 | −22,99 |
| JEDI.DE | 70,11 | 94,83 | −24,72 |
| DXYZ.US | 65,68 | 103,01 | −37,33 |
| **Total** | | | **+105,17** |

**Ninguna diferencia.** El propio informe de XTB da 105,17 € de resultado cerrado, la
misma cifra al céntimo. Es mejor referencia que una hoja anotada a mano, porque es la
que el bróker declara.

NOW.US se compró y se vendió cuatro veces; el resultado sale de las cuatro cerradas
por separado, no de restar el total vendido al total comprado, que daría lo mismo por
casualidad sólo porque la posición acabó a cero.

El efectivo de la cuenta queda en 603,76 €, que también cuadra: 498,31 ingresados más
0,47 de intereses, menos 0,19 de comisiones, menos 1.886,67 de compras, más 1.991,84
de ventas.

## Pendiente

- Contrastar el valor de mercado y los pesos contra lo que muestran las propias
  plataformas.
