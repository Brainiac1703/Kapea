## 1. El identificador del proveedor

- [x] 1.1 Añadir al activo el identificador con el que lo conoce su proveedor, opcional, con su configuración de EF Core y su migración, y verificar que los activos existentes siguen sin él
- [x] 1.2 Usar ese identificador al pedir precios cuando exista, y seguir resolviendo por símbolo cuando no, y verificar con pruebas los dos casos

## 2. Buscar en los proveedores

- [x] 2.1 Definir el puerto de búsqueda de activos y su resultado —nombre, símbolo, clase, mercado e identificador—, y verificar que compila con los proveedores actuales
- [x] 2.2 Buscar en CoinGecko por nombre o símbolo devolviendo su identificador, y verificar con una prueba sobre una respuesta grabada
- [x] 2.3 Buscar en Yahoo por nombre o símbolo, quedándose con instrumentos que Kapea sepa tratar, y verificar con una prueba sobre una respuesta grabada
- [x] 2.4 Preguntar a los dos a la vez y juntar los resultados, diciendo que la búsqueda está incompleta cuando uno falle, y verificar con una prueba que un proveedor caído no vacía la respuesta

## 3. Añadir sin duplicar

- [x] 3.1 Reconocer que un resultado corresponde a un activo del catálogo traduciéndolo con la misma función que se usa para pedir precios, y verificar con una prueba que elegir ServiceNow encuentra el activo importado de XTB
- [x] 3.2 Añadir al seguimiento desde un resultado, guardando el identificador del proveedor, y verificar con una prueba de API que el activo queda en la lista con su identificador
- [x] 3.3 Conservar el alta por símbolo y clase, y verificar que las pruebas que ya existen siguen pasando

## 4. La pantalla

- [x] 4.1 Cambiar la caja de añadir por una búsqueda que enseñe los resultados con su nombre, su tipo y su mercado, y verificar en el navegador buscando por nombre y por símbolo
- [x] 4.2 Dejar seguir un activo por símbolo cuando la búsqueda no encuentre nada, y verificar que se entiende la salida
- [x] 4.3 Decir cuándo la búsqueda está incompleta porque un proveedor no respondió, y verificar que el escáner de localización sigue pasando

## 5. Cierre

- [x] 5.1 Ejecutar la compilación y toda la batería de pruebas y verificar que pasan
- [x] 5.2 Comprobar en el navegador con datos reales: buscar «cardano» y encontrarlo, buscar «ServiceNow» y que reconozca el activo que ya existe, y añadir una cripto que no esté en la lista escrita a mano y ver que recibe precios
- [x] 5.3 Documentar en `docs/uso.md` cómo se busca y qué significa el identificador del proveedor
