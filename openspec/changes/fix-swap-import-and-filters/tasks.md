## 1. Permuta en el adaptador de Kraken

- [x] 1.1 Añadir a `ImportRecord` y a `StagedPayload` la petición de valorar el registro por precio de cierre cuando el origen no lo valora, y verificar que compila y que los registros existentes siguen comportándose igual
- [x] 1.2 Emparejar en `KrakenImportAdapter` los apuntes `spend`/`receive` sin pata en dinero como las dos patas de una permuta —venta del activo que sale, compra del que entra, ambas sin efecto en caja y pendientes de valorar, cada una con su propio identificador de apunte— y verificar con una prueba de unidad que un par de ese tipo produce una venta y una compra
- [x] 1.3 Cubrir con pruebas de unidad el apunte sin pareja (queda como movimiento suelto pendiente de revisión) y el par con pata en dinero (sigue siendo compra o venta), y verificar que ambas pasan

## 2. Valoración estimada

- [x] 2.1 Añadir al dominio la marca de importe estimado en `Transaction`, con su configuración de EF Core y su migración, y verificar que `dotnet ef migrations add` genera la columna con valor falso por omisión
- [x] 2.2 Crear en la capa de aplicación el puerto que da el precio de cierre de un activo en una fecha, resolviéndolo primero del histórico guardado y descargándolo sólo si falta, y verificar con una prueba que la segunda consulta del mismo día no pide nada al proveedor
- [x] 2.3 Valorar en `ImportPipeline` los registros que lo piden, marcándolos como estimados, y verificar con una prueba de integración que una permuta importada sin importe queda valorada al precio de ese día
- [x] 2.4 Dejar pendiente de revisión, sin importe estimado, el registro cuyo precio no se puede obtener, y verificar con una prueba que la importación no falla y el movimiento aparece en revisión
- [x] 2.5 Retirar la marca de estimado al corregir el importe de un movimiento, y verificar con una prueba de API que tras la corrección el movimiento ya no se declara estimado y la cartera se recalcula

## 3. Incoherencias visibles

- [x] 3.1 Guardar las incoherencias del cálculo como una proyección más por activo, reemplazada en cada recálculo, con su migración, y verificar con una prueba que recalcular dos veces no las duplica
- [x] 3.2 Rellenar el campo `Inconsistencies` de la cartera desde esa proyección en lugar de la lista vacía, y verificar con una prueba de API que una venta sin lotes suficientes devuelve la incoherencia y la cartera deja de declararse completa
- [x] 3.3 Mostrar las incoherencias en la pantalla de Cartera con texto localizado compuesto en el cliente —activo, fecha y cantidad que falta—, y verificar que el escáner de localización sigue pasando

## 4. Corrección de lo ya importado

- [x] 4.1 Escribir la migración que borra las patas de permuta de Kraken mal importadas, respetando las que el usuario haya anulado, corregido o emparejado con un manual, y verificar en la base de desarrollo que borra exactamente los diez movimientos afectados y ninguno más
- [x] 4.2 Releer el histórico completo de Kraken en desarrollo y verificar que las cinco permutas vuelven como venta y compra valoradas, que la posición de PAXG queda cerrada con su resultado realizado y que ETH, DOGE, PEPE, BTC y USDG pierden las unidades entregadas

## 5. Filtro de tipos en Movimientos

- [x] 5.1 Cambiar el tipo suelto por una colección en `TransactionQuery`, en la consulta de persistencia y en el endpoint de búsqueda, tratando la colección vacía como todos los tipos, y verificar con una prueba de API que filtrar por compras y ventas devuelve ambos tipos y ninguno más
- [x] 5.2 Pasar la colección desde `KapeaApiClient` y convertir el desplegable de tipos en selección múltiple con los nombres traducidos por las claves que ya usa la tabla, y verificar en el navegador que el filtro aparece en castellano y admite varios tipos
- [x] 5.3 Verificar que la llamada con un solo tipo sigue funcionando, para no romper enlaces ni pruebas existentes

## 6. Pasos a Earn

- [x] 6.1 Descartar en el adaptador de Bit2Me las dos caras del paso a Earn —la transacción de monedero con subtipo de rendimiento y el movimiento de Earn de entrada o salida—, contándolas como registros sin efecto financiero, y verificar con una prueba de unidad que no se emiten y que las recompensas sí
- [x] 6.2 Descartar en el adaptador de Kraken los apuntes de traspaso que son dos anotaciones del mismo activo canónico bajo una misma referencia, una que entra y otra que sale, y verificar con una prueba de unidad que no se emiten y que un traspaso de verdad sigue entrando
- [x] 6.3 Escribir la migración que borra los pasos a rendimiento ya importados, con las mismas salvaguardas que la de las permutas, y verificar en la base de desarrollo que borra exactamente los 92 movimientos afectados y ninguno más
- [x] 6.4 Releer el histórico en desarrollo y verificar que no vuelve ninguno, que la lista se queda sin traspasos y que las 2.129 recompensas de Earn siguen intactas

## 7. Cierre

- [x] 7.1 Ejecutar la compilación y toda la batería de pruebas y verificar que pasan
- [x] 7.2 Comprobar en el navegador la cartera, la lista de movimientos y el detalle fiscal del ejercicio afectado, y verificar que las cifras de PAXG cuadran con lo que el usuario vendió
- [ ] 7.3 Desplegar, aplicar la migración en producción, releer allí el histórico de Kraken y verificar que la cartera de producción refleja lo mismo que la de desarrollo
