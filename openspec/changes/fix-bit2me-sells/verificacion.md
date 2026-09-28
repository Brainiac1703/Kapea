# Comprobación con los datos reales

Relectura completa del histórico en local, 2026-09-28 18:05.

## Las dos ventas entran

```
Bit2Me: 4220 normalizados … 1 importables, 4219 duplicados, 0 rechazados
```

**Cero rechazados**, frente a los dos que había. Las cifras de la venta de QNT:

| | |
|---|---|
| Cantidad | 0,95674537 |
| Bruto | 189,51212289 € |
| Comisión | 4,87785736 € |
| **Neto** | **184,63426553 €** |

El neto coincide **al céntimo de céntimo** con lo que Bit2Me ingresó en la cuenta, que es lo que se quería: el ingreso sale del destino y no de multiplicar por el cambio publicado.

## Una suposición de la propuesta que era falsa

La propuesta daba por hecho que la venta de EURC del 6 de mayo de 2025 faltaba y que, al entrar, cambiarían las cifras de 2025 y habría que rehacer el contraste fiscal.

**No era así.** Esa venta ya estaba en la cartera por otra vía; el registro rechazado era el mismo hecho económico visto desde el monedero, y con el adaptador corregido se reconoce como **duplicado**. Sólo ha entrado un movimiento nuevo, el de QNT.

El resultado por ejercicio lo confirma: 2025 sigue en **−24,54759690**, exactamente la cifra que se había dado por validada. El contraste contra la declaración no hay que rehacerlo.

## Lo que queda abierto

El lote de QNT no se consume. La venta es de **0,95674537** y las dos compras suman **0,95674536**: sobra una cienmillonésima, y el motor FIFO se niega a disponer de lo que no consta adquirido. Queda registrado como incoherencia, que es el comportamiento correcto:

```
INCOHERENCIA QNT  falta = 0,00000001
```

No es un fallo de este cambio ni del adaptador: es que la plataforma redondea al vender la posición entera. Se trata aparte, en el motor de cálculo, con una tolerancia relativa a la cantidad vendida.

Mientras tanto, la venta de QNT está importada con sus cifras correctas pero **no cuenta como resultado realizado**, así que el ejercicio 2026 todavía no la refleja.
