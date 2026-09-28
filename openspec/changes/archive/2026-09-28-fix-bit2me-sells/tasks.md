## 1. La venta toma el lado correcto

- [x] 1.1 En `Bit2MeImportAdapter.FromWalletTransaction`, elegir el lado del activo según el tipo: el origen en una venta, el destino en el resto. Se verifica con una prueba sobre el movimiento real anonimizado: la venta sale con su activo y su cantidad.
- [x] 1.2 Tomar el importe en euros del lado que lo esté, no sólo de `denomination`. Se verifica con una prueba que comprueba que el ingreso es el recibido y no la cantidad por el cambio publicado.
- [x] 1.3 Comprobar que el diferencial se registra como comisión sin tocar `Spread`. Se verifica con una prueba que espera la diferencia entre lo publicado y lo recibido.
- [x] 1.4 Comprobar que nada de lo que ya funcionaba cambia: compra, permuta, ingreso y retirada. Se verifica con las pruebas existentes del adaptador en verde.

## 2. Un rechazo se ve

- [x] 2.1 Llevar `ImportRun.RecordsRejected` a `AccountSynchronizationResult` y a la línea de registro de la sincronización. Se verifica con una prueba que sincroniza un movimiento irreconocible y comprueba el recuento.
- [ ] 2.2 Llevarlo al informe que devuelve la API, por la lista de problemas que la pantalla ya enseña. Se verifica con una prueba de la API.
- [ ] 2.3 Añadir el texto del aviso a `UiStrings.resx` y `UiStrings.en.resx` y enseñarlo sólo cuando haya rechazados. Se verifica con `LocalizationConventionTests` y una prueba de componente con bUnit.

## 3. Cierre

- [x] 3.1 Ejecutar la batería completa, también con `LANG=en_US`. Se verifica en verde.
- [x] 3.2 En local, releer el histórico completo y comprobar que las dos ventas entran: QNT queda a cero y la posición desaparece, y la de EURC de mayo de 2025 aparece en el ejercicio que le toca.
- [x] 3.3 Anotar las cifras nuevas —coste, realizado, efectivo y resultado fiscal por ejercicio— y decir en qué cambian respecto a las que se daban por validadas, para poder rehacer el contraste con la declaración.
