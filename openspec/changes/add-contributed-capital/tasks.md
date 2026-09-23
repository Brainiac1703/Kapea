## 1. El cálculo

- [x] 1.1 Añadir al dominio el cálculo del dinero aportado a partir de los movimientos valorados —ingresado, retirado y neto en euros, excluyendo activos, traspasos internos confirmados y lo no resuelto—, y verificar con pruebas de unidad cada exclusión
- [x] 1.2 Declarar en ese mismo cálculo que la cifra se queda corta cuando ha entrado algún activo desde fuera, y verificar con una prueba que se detecta y que sin esas entradas no se declara
- [x] 1.3 Dar la diferencia con el patrimonio en euros y en porcentaje, sin porcentaje cuando no se ha aportado nada, y verificar los dos casos con pruebas de unidad
- [x] 1.4 Colocar el resultado en `PortfolioSummary.Build` junto al resto de las cifras, y verificar con una prueba que una cartera compuesta lo devuelve

## 5. Una sola definición

- [x] 5.1 Llevar a una única definición compartida lo que cuenta como aportación y usarla también en la serie de evolución, y verificar con pruebas que un traspaso interno confirmado y una entrada de activo dejan de contar como dinero puesto

## 2. La respuesta

- [x] 2.1 Añadir las cifras al contrato de cartera y rellenarlas desde la consulta, y verificar con una prueba de API que un ingreso, una retirada y una compra devuelven el aportado y la diferencia esperados
- [x] 2.2 Verificar con una prueba de API que un traspaso entre dos cuentas del propio usuario no altera lo aportado

## 3. Las pantallas

- [x] 3.1 Mostrar en Cartera lo aportado junto al patrimonio, con lo ingresado y lo retirado desglosados y la diferencia en euros y en porcentaje, y verificar en el navegador que las cifras cuadran con las del histórico
- [x] 3.2 Mostrar la advertencia cuando lo aportado se queda corto y el texto de «nada con lo que comparar» cuando no hay aportaciones, y verificar que el escáner de localización sigue pasando
- [x] 3.3 Mostrar lo aportado en Inicio junto a las cifras que ya están, y verificar en el navegador que coincide con Cartera

## 4. Cierre

- [x] 4.1 Ejecutar la compilación y toda la batería de pruebas y verificar que pasan
- [x] 4.2 Comprobar en el navegador con el histórico real que lo aportado son 4.235,04 € y que la diferencia con el patrimonio es la que se ve en Cartera
- [x] 4.3 Documentar en `docs/uso.md` de dónde sale la cifra y qué no incluye, para que no se lea como una rentabilidad
