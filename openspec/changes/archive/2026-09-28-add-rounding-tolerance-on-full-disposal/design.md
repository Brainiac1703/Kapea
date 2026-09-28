## Context

Ver `proposal.md` — Why. Aquí lo que condiciona la forma, y sobre todo la elección del umbral, que es lo único delicado.

`FifoCalculator.Dispose` compara lo disponible con lo vendido y, si no alcanza, registra la incoherencia y vuelve sin consumir nada. Después reparte el ingreso entre los lotes en proporción a lo que se toma de cada uno, y hay ya un ajuste al final que asigna al último lote lo que el reparto deje suelto por el último decimal.

Las cantidades se guardan con **ocho decimales**, así que un artefacto de redondeo de la plataforma son unas pocas unidades de 1e-8.

## Goals / Non-Goals

**Goals:** que una posición vendida entera se cierre, sin dejar de marcar un descuadre real.

**Non-Goals:** ajustar el inventario con movimientos inventados, y tocar cómo se valoran las ventas.

## Decisions

### El umbral es una millonésima de lo vendido

Se acepta la venta cuando lo que falta no llega a **una millonésima parte** de la cantidad vendida.

El número sale de los dos órdenes de magnitud que hay que separar, no de una intuición:

- **Un redondeo** vive en el último decimal guardado, 1e-8. Frente a cualquier venta realista —del orden de la unidad o mayor— eso es 1e-8 de proporción, cien veces por debajo del umbral. El caso real de QNT: falta 1e-8 sobre 0,95674537, es decir 1,05e-8 de proporción.
- **Un descuadre real** —una compra que no se importó, un traspaso que no se reconoció— es una fracción apreciable de la posición: por ciento, no por millón.

Entre los dos hay seis órdenes de magnitud de margen, así que el valor exacto no es crítico mientras se quede en medio. Una millonésima deja sitio de sobra para plataformas que redondeen a menos decimales que Kapea y sigue estando lejísimos de cualquier descuadre que importe.

*Alternativa descartada:* **un umbral absoluto**, del tipo «tolera hasta 1e-6 unidades». Es más simple de leer y falla justo donde más duele: en un activo cuyo precio unitario es alto, esa misma cantidad absoluta puede valer euros; y en una posición minúscula, puede ser la posición entera. El usuario eligió relativo por esto.

### Se dispone de lo disponible, no de lo vendido

Cuando el umbral acepta la venta, la disposición se hace sobre la cantidad consumible. El resultado realizado registra esa cantidad, y no la que traía el movimiento.

Así la suma de lo consumido de cada lote coincide exactamente con la cantidad del resultado, que es lo que hace el cálculo auditable: cualquier otra elección deja un resultado que dice haber vendido más de lo que desglosa.

La diferencia es la cienmillonésima que se está tolerando, y que ya no aparece en ninguna cifra.

### El ingreso se atribuye entero, sin repartirlo de nuevo

El dinero recibido no se toca: se reparte entre los lotes consumidos en proporción a lo que se toma de cada uno, y el ajuste del último lote que ya existe se encarga del resto.

*Alternativa descartada:* **reducir el ingreso en proporción a lo no consumido**. Perdería una fracción invisible del importe y, peor, introduciría una diferencia entre lo que la plataforma ingresó y lo que Kapea dice haber obtenido. El dinero es un dato real y observable; la cantidad es lo que trae el redondeo.

### Dentro del umbral no hay incoherencia

Es lo que el usuario pidió y tiene una razón de fondo: un aviso que aparece siempre deja de leerse. Si cada venta total dejara una incoherencia, el contador dejaría de servir para detectar las que sí importan, que es exactamente para lo que existe.

El dominio no tiene dependencias y no puede registrar en el log, así que no queda rastro de la tolerancia aplicada. Es aceptable porque la magnitud es la del último decimal guardado; si algún día hace falta auditarlo, el sitio sería el resultado realizado y no un registro.

## Risks / Trade-offs

- **Tapar un descuadre real** → Es el riesgo entero del cambio, y se acota con el margen de seis órdenes de magnitud y con pruebas a los dos lados del umbral. Una venta de un activo sin ningún lote sigue marcándose siempre: la proporción que falta es del cien por cien.
- **Una plataforma que redondee mucho peor que a ocho decimales** → Quedaría por encima del umbral y se marcaría como incoherencia, que es el comportamiento seguro: se ve y se decide, en lugar de aceptarse en silencio.
- **La cantidad del resultado difiere en 1e-8 de la del movimiento** → Es la tolerancia haciendo su trabajo. Lo alternativo sería que difiriera el desglose, que es peor.
- **No contradice «Precisión y redondeo»** → Ese requisito prohíbe redondear valores intermedios almacenados, y aquí no se redondea nada: se compara una diferencia con un umbral y se consume lo que hay, con aritmética decimal exacta.

## Migration Plan

Ninguna. El recálculo de la cartera es determinista y se rehace entero, así que basta con recalcular para que la venta pendiente se procese.
