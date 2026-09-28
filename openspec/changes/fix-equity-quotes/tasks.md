## 1. La cotización usa el endpoint que responde

- [x] 1.1 Cambiar `YahooMarketPriceProvider` de `v7/finance/quote` a `v8/finance/chart`, leyendo `regularMarketPrice`, `regularMarketTime` y `currency` de la cabecera `meta`, un valor por petición. Se verifica con una prueba sobre una respuesta grabada del endpoint nuevo.
- [x] 1.2 Que el fallo de un valor no impida el de los demás. Se verifica con una prueba: dos valores, el primero responde 401 y el segundo devuelve precio.

## 2. Lo que cotiza fuera del euro se convierte

- [x] 2.1 Inyectar `IExchangeRateProvider` y convertir con el tipo del día cuando la divisa no sea el euro, como ya hace el proveedor de histórico. Se verifica con una prueba: valor en dólares con tipo conocido devuelve el precio en euros.
- [x] 2.2 Sin tipo con el que convertir, el valor se queda sin precio y se deja constancia. Se verifica con una prueba que comprueba que no se entrega una cifra en dólares como si fuera en euros.
- [x] 2.3 Actualizar el registro de dependencias. Se verifica compilando y con la batería completa.

## 3. Cierre

- [x] 3.1 Ejecutar la batería completa, también con `LANG=en_US`. Se verifica en verde.
- [x] 3.2 Comprobar en local que MREO y el resto de acciones enseñan precio, y que las cifras validadas de la cartera no se mueven.
