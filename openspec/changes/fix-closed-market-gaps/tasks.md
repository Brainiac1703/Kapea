## 1. Saber cuándo estuvo cerrado un mercado

- [x] 1.1 Implementar en el dominio la deducción de qué días estuvo cerrado el mercado de una clase, a partir de qué activos de esa clase tienen precio; verificar con tests de día con todas las acciones sin precio, de día con una sola sin precio, y de criptomoneda, que nunca cierra
- [x] 1.2 Verificar con test que con un único activo de una clase el día se trata como dato ausente y no como mercado cerrado, que es el límite conocido de la heurística
- [x] 1.3 Exponer por la serie de precios si un día sin cotización lo es por mercado cerrado o por dato ausente; verificar con tests de ambos casos

## 2. Valorar al último cierre cuando el mercado estuvo cerrado

- [x] 2.1 Pedir, además del rango, el último precio conocido de cada activo anterior a su inicio; verificar con test que un rango que empieza en sábado puede valorarse
- [x] 2.2 Valorar una posición al último cierre conocido los días de mercado cerrado, dejando constancia de que el precio viene arrastrado y de qué día es; verificar con tests de fin de semana, de festivo de una sola bolsa y de día normal
- [x] 2.3 Dejar de marcar incompleto un día cuyas únicas ausencias sean por mercado cerrado; verificar con tests de fin de semana con acciones y de día con una laguna real
- [x] 2.4 No arrastrar cuando no hay ningún cierre anterior; verificar con test de día anterior a la primera cotización
- [x] 2.5 Verificar con test que el valor total de un sábado incluye la renta variable y no cae respecto al viernes por este motivo

## 3. Que se vea

- [x] 3.1 Llevar a los contratos si una valoración usa precio arrastrado y de cuándo, y si un día lo tiene; verificar con tests de integración de la API
- [x] 3.2 Distinguir en la pantalla de evolución los días valorados con precios arrastrados; verificar manualmente con un fin de semana
- [x] 3.3 Añadir los textos a los recursos es-ES y en, sin literales en el marcado; verificar que el escáner de localización pasa

## 4. Que «Todo» empiece donde hay datos

- [x] 4.1 Resolver el rango completo del patrimonio como el primer día con movimientos; verificar con test de integración de la API
- [x] 4.2 Decir que no hay nada que enseñar cuando no existe ningún movimiento, en lugar de devolver años vacíos; verificar con test
- [x] 4.3 Hacer que la pantalla de evolución pida el rango completo sin número de días; verificar manualmente que la línea ocupa la gráfica entera

## 5. Comprobación con los datos reales

- [x] 5.1 Comprobar que del 2 al 6 de julio de 2026 no hay corte y que el valor del 3, 4 y 5 ya no cae respecto al del 2
- [x] 5.2 Comprobar cuántos días siguen incompletos y que los que queden son lagunas de verdad y no fines de semana; dejarlo escrito en el cambio
- [x] 5.3 Comprobar que el coste, el resultado realizado, el efectivo y los dos ejercicios fiscales salen idénticos a las cifras registradas en la validación con datos reales
