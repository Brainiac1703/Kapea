## 1. El puerto aprende a decir que ha fallado

- [x] 1.1 Añadir en `Kapea.Application/Abstractions/PriceHistory.cs` el tipo que devuelve una petición de histórico —los precios más si el proveedor pudo contestar—, con el comentario que explique por qué no basta una lista vacía. Se verifica compilando.
- [x] 1.2 Cambiar la firma de `IPriceHistoryProvider.GetHistoryAsync` a ese tipo y reescribir el comentario del puerto, que hoy declara «devuelve una serie vacía, nunca un error». Se verifica viendo que el compilador señala los tres implementadores y sus dobles de prueba.

## 2. Los proveedores declaran qué les pasó

- [x] 2.1 En `YahooPriceHistoryProvider`, devolver contestado en el camino normal y no contestado en el `catch`, corrigiendo el comentario que promete un reintento. Se verifica con una prueba nueva en `Kapea.Infrastructure.Tests` que, con una respuesta 401 grabada, comprueba que el resultado declara el fallo y no una serie vacía.
- [x] 2.2 Lo mismo en `CoinGeckoPriceHistoryProvider`, distinguiendo el `catch` de los dos retornos vacíos legítimos —símbolo fuera del mapa y respuesta sin datos—, que sí son contestados. Se verifica con dos pruebas: una de fallo y otra de símbolo desconocido, que deben dar resultados distintos.
- [x] 2.3 Componer la respuesta en `PriceHistoryDispatcher`: los días se acumulan como hoy, y el rango se da por contestado sólo si ninguno de los proveedores preguntados falló. Se verifica con pruebas en `PriceHistoryDispatcherTests` para los tres casos: todos responden, el primero falla y el segundo trae días, y el primero completa el rango sin llegar a preguntar al segundo.

## 3. El alcance sólo se escribe cuando hay respuesta

- [x] 3.1 En `PriceHistoryUpdater.UpdateAsync`, condicionar `RecordReachAsync` y la actualización del diccionario `reached` a que el tramo haya quedado contestado, dejando el guardado de precios donde está. Se verifica con una prueba que, tras un tramo fallido, comprueba que no se registró alcance y que la pasada siguiente vuelve a pedir el mismo tramo.
- [x] 3.2 Probar el caso que se perdía: relleno hacia atrás que falla en un activo cuya serie ya llega hasta hoy. Se verifica comprobando que el hueco anterior sigue apareciendo en la pasada siguiente, que es lo que hoy no ocurre.
- [x] 3.3 Probar el caso peor: activo sin ningún precio guardado cuya primera petición falla entera. Se verifica comprobando que la pasada siguiente vuelve a pedir su serie desde el principio, en las dos direcciones.
- [x] 3.4 Probar que no se ha roto la protección original: un tramo contestado y vacío se sigue anotando y no se vuelve a pedir. Se verifica con la prueba existente del escenario «Tramo pedido que no devolvió nada», que debe seguir en verde.

## 4. Que se vea desde fuera

- [x] 4.1 Añadir a `PriceHistoryUpdate` el recuento de activos con algún tramo sin contestar y llevarlo a la línea de registro final, junto a los días escritos y los activos sin cobertura. Se verifica con una prueba que comprueba el recuento tras una pasada con un activo fallido y otro correcto.
- [x] 4.2 Repasar los llamantes de `PriceHistoryUpdate` para que ninguno interprete el contador nuevo como un fallo de la ejecución entera: una pasada con fallos parciales sigue siendo una pasada correcta. Se verifica compilando y con la batería completa.

## 5. Cierre

- [x] 5.1 Ejecutar la batería completa y comprobar que sigue en verde, también con `LANG=en_US` para no repetir el fallo de cultura de la PR #28.
- [ ] 5.2 Desplegar y comprobar en producción, con el registro ya limpio, que la siguiente pasada informa del recuento de fallos y que un activo que falló vuelve a pedirse. Se verifica en los registros de `log-kapea`.
- [ ] 5.3 Mirar en producción qué activos tienen alcance por debajo de su primer precio guardado —los huecos ya perdidos, como USDG-USD— y decidir con el usuario si se borran esas filas para que se vuelvan a pedir. Es la pregunta abierta del diseño y no bloquea nada de lo anterior.
