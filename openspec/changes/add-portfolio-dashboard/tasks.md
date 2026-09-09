## 1. Saldo de efectivo

- [ ] 1.1 Implementar el cálculo del efecto en caja de cada tipo de movimiento —ingreso, retirada, compra, venta, dividendo, interés, recompensa y comisión— como función pura del dominio; verificar con tests de cada tipo y de una comisión de compra que reduce el saldo
- [ ] 1.2 Implementar el saldo por cuenta y divisa a partir de esos efectos; verificar con test que una cuenta que ha operado en dos divisas devuelve un saldo por cada una y que no se suman entre sí
- [ ] 1.3 Excluir del saldo los movimientos sin clasificar y los traspasos pendientes, devolviendo cuántos quedan; verificar con test que el saldo se marca como incompleto
- [ ] 1.4 Comprobar que un traspaso interno confirmado no altera el efectivo salvo por su comisión de red; verificar con test
- [ ] 1.5 Implementar la conversión del efectivo a euros al tipo del día para el total, dejando fuera las divisas sin tipo disponible; verificar con tests de divisa con tipo y sin él

## 2. Precios de renta variable

- [ ] 2.1 Implementar la conversión del símbolo del bróker al del proveedor con la regla general —sufijo de mercado estadounidense fuera, resto tal cual— y una tabla de excepciones por activo; verificar con tests de ambos casos y de una excepción
- [ ] 2.2 Implementar el proveedor de precios de renta variable contra respuestas grabadas, pidiendo varios símbolos en una sola llamada; verificar que un símbolo desconocido no impide obtener el resto
- [ ] 2.3 Implementar el despachador que elige proveedor por clase de activo; verificar con test que una cartera con acciones y criptos obtiene precios de ambos y que una clase sin proveedor no rompe la consulta
- [ ] 2.4 Añadir a la tabla de CoinGecko los identificadores que faltan, entre ellos TAO, B2M y PAXG; verificar con test que la cartera del usuario queda cubierta por completo
- [ ] 2.5 Implementar la caché breve de precios en el servidor; verificar con test que dos consultas seguidas producen una sola llamada al proveedor y que pasada la ventana se vuelve a consultar
- [ ] 2.6 Implementar la degradación cuando el proveedor falla o agota su cuota; verificar con test que la consulta de cartera responde igual sin precios

## 3. Cartera ampliada

- [ ] 3.1 Añadir a la posición abierta el peso sobre el valor total, las comisiones acumuladas y el resultado ya realizado del activo; verificar con tests, incluido el caso de pesos parciales cuando falta algún precio
- [ ] 3.2 Implementar el resultado acumulado de la cartera —realizado desde el inicio, latente y su suma—; verificar con test que difiere del resultado de un ejercicio concreto y que ambos salen de los mismos datos
- [ ] 3.3 Implementar el valor total del patrimonio como posiciones más efectivo, con su marca de incompleto; verificar con tests de total completo, sin algún precio y sin algún saldo
- [ ] 3.4 Implementar la agrupación por clase de activo con subtotales y peso por grupo; verificar con test que una clase nueva aparece como un grupo más sin cambios en la consulta
- [ ] 3.5 Implementar el resumen de rendimientos cobrados con su retención, agrupados por clase; verificar con test
- [ ] 3.6 Ampliar los contratos de la cartera y su endpoint con todo lo anterior; verificar con tests de integración de la API

## 4. Pantalla de cartera

- [ ] 4.1 Rehacer la pantalla como una sola vista agrupada por clase de activo, con subtotales por grupo y el total del patrimonio arriba; verificar manualmente con posiciones de acciones y de cripto
- [ ] 4.2 Mostrar por posición cantidad, coste medio, precio, valor, resultado latente, resultado realizado, comisiones y peso; verificar manualmente que una posición sin precio se distingue de una con valor cero
- [ ] 4.3 Mostrar el efectivo por cuenta y divisa, con el aviso de que se convierte al tipo del día; verificar manualmente
- [ ] 4.4 Mostrar el instante de cada precio y el de la última actualización, con el botón de refrescar; verificar manualmente que refrescar cambia el instante
- [ ] 4.5 Mostrar las advertencias de cifras incompletas —movimientos sin resolver, precios que faltan— arriba y no al pie; verificar manualmente que se ven antes de leer las cifras

## 5. Validación con los datos del usuario

- [ ] 5.1 Importar el histórico real de acciones y comparar el valor de las posiciones abiertas, el coste y el resultado con los de su hoja de cálculo; documentar cada diferencia y su causa
- [ ] 5.2 Repetir la comparación con el histórico de cripto, incluidos los pesos por activo; documentar las diferencias
- [ ] 5.3 Comparar el efectivo calculado con el saldo neto de su hoja de movimientos; verificar que coincide o explicar por qué no
