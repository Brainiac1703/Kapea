## 1. La tolerancia

- [x] 1.1 Añadir en `FifoCalculator` el umbral relativo como constante, con el comentario que explique de dónde sale el valor y por qué es relativo y no absoluto. Se verifica compilando.
- [x] 1.2 En `Dispose`, aceptar la venta cuando lo que falta no alcance el umbral, y disponer de la cantidad consumible en lugar de la vendida. Se verifica con una prueba con el caso real: se vende 0,95674537 con 0,95674536 en lotes, la posición queda cerrada y no hay incoherencia.
- [x] 1.3 Comprobar que el ingreso se atribuye entero a lo consumido. Se verifica con una prueba que suma lo repartido entre los lotes y lo compara con el importe obtenido.

## 2. Que no tape un descuadre

- [x] 2.1 Probar el otro lado del umbral: una venta que excede por una proporción apreciable se sigue marcando como incoherencia y no se procesa. Se verifica con una prueba.
- [x] 2.2 Probar que una cantidad despreciable en absoluto pero apreciable frente a una posición pequeña se marca igualmente. Es lo que justifica que el umbral sea relativo, así que sin esta prueba el cambio no está defendido.
- [x] 2.3 Probar que vender un activo sin ningún lote se sigue marcando siempre. Se verifica con una prueba.
- [x] 2.4 Comprobar que las pruebas existentes del motor siguen en verde, en particular las del reparto entre lotes y las del resultado realizado.

## 3. Cierre

- [x] 3.1 Ejecutar la batería completa, también con `LANG=en_US`. Se verifica en verde.
- [x] 3.2 Recalcular en local y comprobar que QNT desaparece de posiciones, que su venta genera resultado realizado y que el contador de incoherencias vuelve a cero.
- [x] 3.3 Anotar las cifras resultantes del ejercicio 2026, que hoy no reflejan esa venta, y contrastar que 2025 no se mueve.
