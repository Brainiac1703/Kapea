## Why

Una posición vendida entera no se cierra porque la plataforma redondea.

El usuario vendió toda su posición de QNT en Bit2Me. La venta entra bien —eso se arregló en `fix-bit2me-sells`— con **0,95674537** unidades, y sus dos compras suman **0,95674536**. Sobra una cienmillonésima. El motor FIFO se niega a disponer de lo que no consta adquirido y registra la incoherencia:

```
INCOHERENCIA QNT  tipo=InsufficientLots  falta=0,00000001
```

Esa negativa es correcta en general y está escrita a propósito: «no genera un resultado parcial silencioso». Lo que le falta es distinguir **un descuadre real de un artefacto de redondeo**.

El coste de no distinguirlos es alto: la venta está importada con sus cifras exactas —189,51 bruto, 4,88 de comisión, 184,63 netos, justo lo que llegó a la cuenta— pero no genera resultado realizado ni cierra la posición. QNT sigue apareciendo en cartera, el ejercicio no la refleja, y las cifras se presentan como incompletas. Y volverá a pasar cada vez que se venda una posición entera.

## What Changes

- **Una venta que excede lo disponible por un redondeo consume lo que hay y cierra la posición.** El resto sigue igual: una venta que excede por una cantidad apreciable se sigue marcando como incoherencia y no se procesa.
- **El umbral es relativo a la cantidad vendida**, no un valor absoluto. Lo que distingue un redondeo de un descuadre es su proporción: una cienmillonésima sobre 0,95 es ruido, y esa misma cantidad sobre una posición de dos cienmillonésimas es la mitad de la posición.
- **Dentro del umbral no se registra incoherencia.** El contador tiene que seguir significando algo: si se llena de ruido, deja de leerse y volvemos a no enterarnos de los descuadres de verdad.
- **El ingreso se atribuye entero** a lo que se consume. Ese dinero se recibió y fue por esa posición; repartirlo en proporción a una cantidad que no se consumió dejaría un resto sin explicar.
- **Sin ajustes de inventario.** No se crea ninguna micro-adquisición a coste cero para cuadrar: sería un movimiento que nunca ocurrió.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `pnl-engine`: una venta que excede lo disponible por debajo del umbral de redondeo se procesa contra lo disponible en lugar de rechazarse, y no cuenta como incoherencia.

## Impact

- **`Kapea.Domain/Calculation/FifoCalculator.cs`**: el único fichero con lógica nueva, en `Dispose`. La disposición pasa a hacerse sobre la cantidad realmente consumible.
- **Sin migración y sin cambios en la interfaz.** Lo que el usuario nota es que una posición vendida entera desaparece de la cartera, que es lo que esperaba desde el principio.
- **Las cifras cambian, y ése es el objetivo**: la venta de QNT pasa a generar resultado realizado y a contar en el ejercicio 2026, que hoy no la refleja.
- **El riesgo es tapar un descuadre real**, y por eso el umbral y sus dos lados son la parte que más cuidado exige.
- **Fuera de alcance**: cómo se valoran las ventas, el adaptador de Bit2Me y cualquier ajuste automático del inventario.
