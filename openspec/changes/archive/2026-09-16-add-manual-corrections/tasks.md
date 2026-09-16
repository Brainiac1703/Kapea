## 1. Dominio

- [x] 1.1 Añadir `TransactionOrigin.Manual`, la nota, `RegisteredAt` y `RevisedAt`, `Transaction.FromManualEntry` con su huella y `Revise(…)` sólo para manuales. Se verifica con pruebas: alta sin nota con su instante, edición que actualiza `RevisedAt`, rechazo de datos incoherentes con el mismo motivo que un importado, `Revise` sobre un importado o un ajuste falla.
- [x] 1.2 Añadir el estado de anulación con `VoidedAt`, `VoidReason`, `Void(reason, at)` y `Restore()`. Se verifica con pruebas: anular con motivo, rechazar motivo vacío, rechazar anular un manual o un ajuste, restaurar deja los datos intactos.
- [x] 1.3 Añadir `DistinctFrom` y la regla de coincidencia entre un manual y un importado: misma cuenta, tipo, activo, cantidad y día. Se verifica con pruebas de coincidencia, de cada campo que la rompe y de una pareja ya marcada como distinta.

## 2. Persistencia

- [x] 2.1 Comprobar que EF Core 10 con SQL Server admite filtros de consulta con nombre y desactivar uno solo. Se verifica con una prueba de integración mínima; si no, aplicar la alternativa del diseño y actualizarlo.
- [x] 2.2 Convertir el filtro de movimientos en `Owner` e `InForce` y mapear las columnas nuevas. Se verifica con una prueba de integración: un anulado no aparece en una consulta normal, sí al desactivar sólo `InForce`, y nunca aparece uno de otro usuario.
- [x] 2.3 Crear la migración `ManualMovements`. Se verifica aplicándola sobre la base local con datos y comprobando que la cartera no cambia.
- [x] 2.4 Hacer que la búsqueda de huellas desactive `InForce`. Se verifica con una prueba de integración: la huella de un anulado cuenta como existente.
- [x] 2.5 Añadir una prueba de arquitectura que falle si una consulta de movimientos llama a `IgnoreQueryFilters()` sin nombre, salvo la adopción de datos. Se verifica en verde y rompiéndola a propósito una vez.

## 3. Casos de uso

- [x] 3.1 Crear `ManualMovementService.RegisterAsync`. Se verifica con pruebas: manual en euros, en dólares, sin tipo para la fecha y en cuenta ajena; la posición incluye el manual tras recalcular.
- [x] 3.2 Crear `ReviseAsync` y `DeleteAsync`. Se verifica con pruebas: editar cantidad y fecha recalcula y re-resuelve el tipo, editar un importado o un ajuste falla, borrar un importado falla, borrar un manual devuelve las cifras previas.
- [x] 3.3 Crear `VoidAsync` y `RestoreAsync`. Se verifica con pruebas: anular y recalcular, traspaso confirmado bloquea, restaurar devuelve el mismo resultado, anular un sin clasificar lo quita de pendientes.
- [x] 3.4 Crear `CorrectAsync` en una sola operación. Se verifica con pruebas: corrección correcta, ajuste incoherente deja el importado vigente, sin motivo falla, borrar el ajuste no restaura el importado.
- [x] 3.5 Crear la búsqueda de coincidencias para la vista previa y para pendientes, y `MarkDistinctAsync`. Se verifica con pruebas de integración: coincidencia en vista previa, pareja en pendientes, desaparece al marcar distinta y reaparece con otro importado.
- [x] 3.6 Crear el cálculo de impacto con una o dos fechas y el reloj inyectado. Se verifica con pruebas de un año pasado, el actual, el 1 de enero y una edición que cruza de ejercicio.

## 4. API y contratos

- [x] 4.1 Ampliar `TransactionResponse` con procedencia API, fichero, manual o ajuste, nota, instantes de alta y edición, motivo y anulación; ampliar `PendingReviewResponse` y la fila de la vista previa; y añadir los filtros `origin` y `voided` a la búsqueda. Se verifica con pruebas de API sobre la búsqueda filtrada por cada procedencia y el recuento de pendientes.
- [x] 4.2 Añadir la procedencia del movimiento a `RealizedResultResponse` y `ConsumedLotResponse`. Se verifica con una prueba de API: una venta que consume un lote de una compra manual lo indica en el detalle del ejercicio.
- [x] 4.3 Añadir los endpoints de alta, edición, borrado, corrección, anulación, restauración, marca de distintos e impacto, con 409 y mensaje en los rechazos. Se verifica con pruebas de API de cada uno, incluido que un usuario no toca movimientos de otro.
- [x] 4.4 Probar de extremo a extremo que anular y reimportar no resucita. Se verifica importando un fichero, anulando un movimiento y volviendo a importarlo: sale como duplicado y sigue anulado.
- [x] 4.5 Probar de extremo a extremo que un manual y un importado iguales se señalan. Se verifica registrando un manual, importando un fichero que lo incluye y comprobando la marca en la vista previa y la pareja en pendientes.

## 5. Pantalla

- [x] 5.1 Añadir el formulario *Nuevo movimiento* con validación, y su uso para *Editar* y para *Corregir* con motivo en lugar de nota. Se verifica con pruebas del modelo del formulario y en el navegador registrando, editando y corrigiendo.
- [x] 5.2 Añadir las acciones por origen y estado con confirmación y aviso de ejercicio. Se verifica en el navegador sobre datos locales.
- [x] 5.3 Sustituir el valor interno de la columna *Procedencia* por texto e icono de API, fichero, a mano o ajuste, con nota, alta y edición en la descripción; marcar anulados; y añadir los filtros de procedencia y estado. Se verifica en el navegador filtrando cada uno.
- [x] 5.4 Mostrar el icono de procedencia en los últimos movimientos de *Inicio* y en cada transmisión y lote de *Fiscal*. Se verifica en el navegador con una compra manual consumida por una venta.
- [x] 5.5 Marcar coincidencias en la vista previa y añadir la sección de posibles duplicados en *Por revisar*. Se verifica en el navegador con un manual y un fichero que lo incluye.
- [x] 5.6 Dar de alta todos los textos en español e inglés. Se verifica con la prueba de textos sin localizar en verde.

## 6. Cierre

- [x] 6.1 Actualizar la guía de uso: registrar, editar y borrar a mano, corregir, anular y deshacer, coincidencias, y quitar el límite conocido. Se verifica leyendo la guía y comprobando los enlaces.
- [x] 6.2 Pasar la batería completa y probar en local dos casos reales: apuntar a mano una compra de XTB e importar después el fichero que la incluye, y corregir un movimiento de Kraken y releer el histórico. Se verifica con las pruebas en verde y las cifras en pantalla.
