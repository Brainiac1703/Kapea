## Context

Kapea ya guarda el cierre diario de cada activo desde su primera adquisición, calcula el valor de la cartera día a día y tiene medias móviles y fuerza relativa. También tiene un servicio de Azure OpenAI que propone el mapeo de un fichero de importación y espera aprobación humana antes de aplicarlo. Ver proposal.md para el porqué.

Dos cifras de sus propios datos condicionan el diseño: **casi un 1,9 % de coste por operación completa** en Bit2Me, y **4.850 € de patrimonio**. A esa escala, una operación de 500 € paga unos 9,50 € de comisión antes de ganar un céntimo.

## Goals / Non-Goals

**Goals:**

- Que cualquier sistema se pueda escribir, simular y comparar sin tocar el programa.
- Que una señal se pueda explicar: qué regla, con qué valores, de qué versión.
- Que el coste de operar y los impuestos estén dentro de toda cifra que se use para decidir.

**Non-Goals:**

- Ejecutar órdenes. Kapea propone; quien opera es el usuario en su plataforma.
- Optimizar parámetros automáticamente. Buscar los números que mejor habrían ido es la forma más rápida de construir un sistema que solo funciona en el pasado.
- Recomendar. Kapea evalúa las reglas que el usuario ha escrito y lo dice en la pantalla.

## Decisions

### Las reglas se declaran como árbol de condiciones, no como texto libre ni como código

Una regla es una condición sobre un indicador, el precio o la posición, combinable con otras. Se guarda estructurada, así que el motor puede evaluarla, la pantalla puede enseñarla y el sistema puede rechazar al guardar lo que no sabrá ejecutar.

*Alternativas descartadas:* un lenguaje de expresiones propio, que habría que parsear, documentar y proteger; y guardar código, que convierte cada sistema en un despliegue y abre la puerta a ejecutar lo que venga de fuera.

### El motor evalúa sobre la serie, y el simulador usa el mismo motor

Las señales de hoy y las del backtest salen del mismo código recorriendo los mismos días. Dos motores distintos divergen, y entonces la simulación deja de decir nada sobre lo que pasará.

El recorrido es día a día hacia delante, con el estado acumulado hasta ese día. Es la misma forma que ya tiene la reconstrucción de la cartera.

### La ejecución simulada ocurre al día siguiente de la señal

La señal nace del cierre, así que comprar a ese mismo cierre es comprar a un precio que ya no existía cuando se supo. Es el error más común y el que más infla los resultados de un backtest.

*Alternativa descartada:* ejecutar al cierre del día de la señal. Más favorable y falso.

### Los costes y los tramos fiscales son datos de la plataforma y de la configuración

La comisión sale de lo que cada plataforma cobre, declarado junto a ella; los tramos del ahorro, de configuración. Escribir el 0,95 % de Bit2Me en el código haría que el día que lo cambien todas las simulaciones pasadas mintieran sin avisar.

### La IA traduce a reglas y redacta explicaciones, y nada más

Mismo patrón que el mapeo de importación: propone, marca lo que no ha sabido traducir y espera aprobación. La propuesta es un árbol de condiciones que el usuario ve antes de guardar.

Queda fuera a propósito leer imágenes de gráficas. Un modelo estimando niveles sobre un dibujo da una respuesta distinta cada vez y no se puede auditar, cuando los números exactos están en la base de datos.

*Alternativa descartada:* pedirle al modelo la señal directamente. No es reproducible, no es explicable y no se puede simular.

### La rentabilidad ponderada por tiempo se calcula encadenando días

Cada día es un tramo cuyo rendimiento se mide contra el valor del día anterior más lo aportado ese día. Encadenarlos neutraliza el efecto de las aportaciones, que es justo lo que hoy impide saber si las decisiones fueron buenas.

La ponderada por dinero se resuelve buscando la tasa que iguala aportaciones y valor final. Es una búsqueda numérica sencilla y acotada.

### Las señales emitidas se guardan

No se recalculan al vuelo cada vez que se abre la pantalla: una señal es un hecho con fecha, y conservarla permite comparar después lo que el sistema dijo con lo que el usuario hizo, que es la mitad del valor del diario.

## Risks / Trade-offs

- **Un sistema puede parecer bueno solo porque se probó sobre el pasado que lo inspiró** → El resultado se presenta siempre junto al de no hacer nada y junto al número de operaciones y a lo pagado en comisiones. No se optimizan parámetros automáticamente.
- **Con 4.850 € y casi un 2 % por operación, muchos sistemas son inviables por aritmética** → Es un resultado legítimo y el simulador lo enseñará. Mejor saberlo antes.
- **Traducir un método descrito en palabras puede producir reglas que no son las del autor** → Nada se guarda sin que el usuario lo apruebe, y lo que el traductor no entiende se marca en lugar de completarse.
- **La fiscalidad es un terreno donde equivocarse sale caro** → Las cifras después de impuestos se presentan como estimación y con los tramos a la vista, para contrastarlas con una gestoría.
- **Las señales sobre series con huecos** → No se emiten, y se dice por qué. Es preferible a una señal calculada sobre una ventana a medias.

## Migration Plan

Todo es aditivo: sin ningún sistema declarado, no hay señales y las pantallas nuevas aparecen vacías. Las métricas de rendimiento se calculan sobre datos que ya existen. Revertir es dejar de mostrar las pantallas nuevas; nada de lo anterior depende de ellas.

## Open Questions

- Qué referencia por omisión conviene para comparar una cartera casi entera de cripto. Se decide al implementar la comparación, y no cambia ni las specs ni el reparto de tareas.
