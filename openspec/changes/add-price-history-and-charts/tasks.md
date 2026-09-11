## 1. Serie de precios guardada

- [x] 1.1 Añadir la entidad de precio diario —activo, fecha, precio en euros y origen— con su tabla y la clave que impide dos precios del mismo activo y día; verificar con test de integración que reinsertar el mismo día no duplica ni falla
- [x] 1.2 Implementar la lectura de la serie de un activo entre dos fechas y la consulta de hasta qué día llega; verificar con tests, incluido el de un activo sin ninguna serie
- [x] 1.3 Implementar la escritura por lotes de una serie descargada; verificar con test que mezclar días nuevos con días ya guardados deja una sola fila por día

## 2. Descarga del histórico

- [x] 2.1 Ampliar el puerto de precios con la serie histórica entre dos fechas; verificar que las implementaciones existentes siguen compilando y que sus tests pasan
- [x] 2.2 Implementar el histórico de Yahoo contra respuestas grabadas, con la conversión de símbolo para cripto —`BTC` a `BTC-EUR`— y para renta variable; verificar con tests de ambos casos y de un símbolo que Yahoo no cubre
- [x] 2.3 Implementar el histórico de CoinGecko contra respuestas grabadas; verificar con test que una petición fuera de los 365 días permitidos se trata como falta de cobertura y no como error
- [x] 2.4 Implementar el reparto entre proveedores —Yahoo primero, CoinGecko para lo que no cubra— y verificar con test que un activo que ninguno cubre deja su serie vacía sin impedir la de los demás
- [x] 2.5 Comprobar contra los proveedores reales qué días quedan sin cubrir para los doce activos del usuario y dejarlo escrito en el change

## 3. Puesta al día

- [ ] 3.1 Implementar el cálculo de qué falta por activo —desde su primera adquisición o desde el último día guardado, hasta hoy—; verificar con tests de las tres situaciones
- [ ] 3.2 Implementar el relleno de los huecos y añadirlo al trabajador periódico; verificar con test que una segunda pasada sobre una serie completa no pide nada al proveedor
- [ ] 3.3 Implementar la degradación ante un proveedor que falla a mitad; verificar con test que lo descargado se guarda y lo que falta queda para la siguiente vuelta
- [ ] 3.4 Ejecutar la primera carga real sobre la cartera del usuario y comprobar cuántos días quedan cubiertos por activo

## 4. Serie de la cartera

- [ ] 4.1 Implementar en el dominio la reconstrucción de las unidades de cada activo día a día a partir de los movimientos; verificar con tests de un día anterior a la compra, del día de la compra y de un día posterior a la venta total
- [ ] 4.2 Implementar el valor diario combinando esas unidades con la serie de precios; verificar con tests de día completo, de día al que le falta el precio de un activo y de día sin ningún precio
- [ ] 4.3 Separar en la serie lo aportado o retirado de lo que cambió por precio; verificar con test que un ingreso seguido de una compra no produce rendimiento
- [ ] 4.4 Implementar la serie de un solo activo y la del reparto por clase; verificar con tests, incluida una clase incorporada más tarde
- [ ] 4.5 Cachear la serie en el servidor y exponerla en la API con su marca de días incompletos; verificar con tests de integración de la API

## 5. Indicadores

- [ ] 5.1 Implementar la media móvil simple y la exponencial sobre una serie; verificar con tests de ventana incompleta, ventana justa y serie larga
- [ ] 5.2 Implementar el RSI; verificar con tests de serie solo alcista, solo bajista y un caso calculado a mano
- [ ] 5.3 Comprobar que los indicadores no rellenan los días ausentes y que cada valor conserva su fecha; verificar con test de serie con huecos
- [ ] 5.4 Exponer los indicadores de un activo en la API, con la ventana como parámetro; verificar con test de integración

## 6. Gráficas

- [ ] 6.1 Construir el componente de gráfica de líneas en SVG, con ejes, rejilla y formato de importes y fechas del idioma activo; verificar manualmente en claro y oscuro
- [ ] 6.2 Mostrar el valor bajo el cursor al pasar por encima; verificar manualmente que la cifra corresponde al día señalado
- [ ] 6.3 Enseñar los días sin precio como hueco y no como caída a cero; verificar manualmente con un activo sin cobertura
- [ ] 6.4 Añadir la pantalla de evolución con la gráfica del patrimonio y el selector de periodo; verificar manualmente
- [ ] 6.5 Añadir el reparto por clase de activo a lo largo del tiempo como área apilada; verificar manualmente
- [ ] 6.6 Añadir la gráfica de un activo desde su posición en la cartera, con sus indicadores superpuestos; verificar manualmente
- [ ] 6.7 Localizar todos los textos nuevos en los dos idiomas; verificar que el test que prohíbe texto sin localizar sigue pasando

## 7. Validación con los datos del usuario

- [ ] 7.1 Contrastar el valor de la cartera de hoy en la gráfica con el que da la pantalla de cartera; verificar que coinciden
- [ ] 7.2 Contrastar la evolución de un activo con la que enseña su plataforma; documentar cada diferencia y su causa
- [ ] 7.3 Dejar escrito qué periodo queda cubierto por activo y qué se ve en los tramos sin cobertura
