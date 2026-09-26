## 1. Separar la cotización de la posición

- [ ] 1.1 Hacer que la serie de un activo se componga de dos fuentes —la cotización, de la serie de precios; las unidades y el valor, de la posición—; verificar con tests de activo con posición, sin ella y con posición sólo en parte del rango
- [ ] 1.2 Verificar con test que un día sin cotización sigue devolviéndose sin precio, sin arrastrar el del día anterior ni interpolar
- [ ] 1.3 Verificar con test que las unidades y el valor quedan vacíos los días sin posición, y que no se confunden con la falta de cotización
- [ ] 1.4 Verificar con tests que la serie de la cartera entera no cambia: mismos valores por día, mismos días incompletos y mismo reparto por clase que antes

## 2. Pedir la cotización del activo consultado

- [ ] 2.1 Pedir la serie de precios del activo consultado aparte de los que alimentan la reconstrucción de la cartera; verificar con test que un activo sin un solo movimiento devuelve su cotización
- [ ] 2.2 Verificar con test que consultar la gráfica de un activo que no se tiene no altera el valor de la cartera de ningún día
- [ ] 2.3 Dejar dicho que un activo sin posición no marca el día como incompleto; verificar con test de día sin cotización de algo que sólo se vigila

## 3. Poder pedir toda la historia

- [ ] 3.1 Admitir en el endpoint de la serie de un activo una petición sin fecha de inicio, resolviéndola como la primera cotización guardada de ese activo; verificar con test de integración de la API
- [ ] 3.2 Verificar con test que un rango que empieza antes de la primera cotización devuelve lo que hay, sin error
- [ ] 3.3 Añadir a la pantalla de un activo el selector de periodo, con la opción de verlo todo; verificar manualmente con un activo largo y otro corto
- [ ] 3.4 Añadir los textos a los recursos es-ES y en, sin literales en el marcado; verificar que el escáner de localización pasa

## 4. Que la gráfica lo aguante

- [ ] 4.1 Medir cuánto tarda en dibujarse la serie completa del activo más largo que hay y dejarlo escrito en el cambio
- [ ] 4.2 Si no va fino, reducir la resolución de lo que se dibuja —nunca la de lo que se calcula— y dejar escrito el criterio; verificar con test que los indicadores no cambian al reducir

## 5. Comprobación con los datos reales

- [ ] 5.1 Comprobar en la aplicación que ADA, que nunca se ha comprado, enseña su gráfica completa con sus medias y su fuerza relativa
- [ ] 5.2 Comprobar que NOW.US enseña su cotización también fuera de los periodos en que se tuvo, y que sus unidades siguen vacías ahí
- [ ] 5.3 Comprobar que el patrimonio, el coste, el resultado realizado, el efectivo y los dos ejercicios fiscales siguen saliendo idénticos a las cifras registradas en la validación con datos reales
