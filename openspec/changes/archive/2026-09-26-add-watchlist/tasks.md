## 1. El seguimiento por usuario

- [x] 1.1 Añadir la lista de activos vigilados por usuario, con su configuración de EF Core y una migración que dé de alta lo que cada uno ha tenido alguna vez, y verificar que tras migrar ningún activo con historial queda fuera
- [x] 1.2 Añadir el activo a la lista del usuario al resolverlo durante una importación, y verificar con una prueba que la primera compra de un activo nuevo lo deja en seguimiento
- [x] 1.3 Cubrir con una prueba que vender toda la posición no lo saca de la lista

## 2. Añadir y quitar

- [x] 2.1 Añadir al seguimiento un activo por símbolo y clase, creándolo en el catálogo si no existía, y verificar con una prueba de API que aparece en la lista y que añadirlo dos veces no lo duplica
- [x] 2.2 Comprobar al añadirlo si algún proveedor da precios y devolver esa respuesta al usuario, sin impedir el alta cuando no los haya, y verificar los dos casos con pruebas
- [x] 2.3 Quitar del seguimiento un activo sin posición, conservando el activo y su histórico, y verificar con una prueba de API que desaparece de la lista y que sus precios siguen ahí
- [x] 2.4 Rechazar quitar un activo con posición abierta, explicando por qué, y verificar con una prueba de API que se rechaza

## 3. El histórico sigue al seguimiento

- [x] 3.1 Llevar a un sitio común el cálculo de cuánto histórico necesitan los sistemas declarados, hoy dentro del servicio de estrategias, y verificar con una prueba que da lo mismo que antes
- [x] 3.2 Descargar la serie de los activos en seguimiento desde el mayor de dos orígenes —su primera adquisición y lo que pidan los sistemas—, y verificar con una prueba que un activo recién añadido y nunca comprado recibe histórico suficiente para evaluarse
- [x] 3.3 Dejar de actualizar la serie de lo que ya no se sigue, conservando lo descargado, y verificar con una prueba

## 4. Las señales saben la situación

- [x] 4.1 Alimentar el motor de señales con los activos en seguimiento en lugar de con todo el catálogo, y verificar con una prueba que un activo fuera de la lista no genera señales
- [x] 4.2 Devolver con cada señal si su activo se tiene o sólo se vigila, y la posición cuando la haya, y verificar con una prueba de API los dos casos
- [x] 4.3 Presentar una salida sobre algo que no se tiene como no accionable y una entrada sobre algo que ya se tiene como ampliación, y verificar en el navegador que se distinguen

## 5. La pantalla de Seguimiento

- [x] 5.1 Crear la pantalla con la lista, distinguiendo lo que se tiene de lo que sólo se vigila, con el último precio y su fecha, y verificar en el navegador con activos de los dos tipos
- [x] 5.2 Decir en la lista cuándo un activo no tiene precio, en lugar de enseñar un cero, y verificar con un activo sin cobertura
- [x] 5.3 Añadir y quitar desde la pantalla, con el aviso cuando no haya precios, y verificar que el escáner de localización sigue pasando
- [x] 5.4 Enlazar cada activo con su evolución y sus señales, y verificar en el navegador

## 6. Las ideas se apoyan en la lista

- [x] 6.1 Poner en seguimiento el activo de una idea al registrarla, y verificar con una prueba de API que una idea sobre un activo nuevo lo deja en la lista
- [x] 6.2 Permitir registrar una idea sobre un activo sin posición, y verificar que no se rechaza

## 7. Cierre

- [x] 7.1 Ejecutar la compilación y toda la batería de pruebas y verificar que pasan
- [x] 7.2 Comprobar en el navegador con datos reales: los doce activos de la cartera en la lista, añadir uno que nunca se ha tenido, ver que recibe precios y que sus sistemas lo evalúan
- [x] 7.3 Documentar la pantalla y el criterio de la lista en `docs/uso.md`
