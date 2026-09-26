## 1. Guardar el recorrido del día

- [ ] 1.1 Añadir a la serie diaria la apertura, el máximo y el mínimo como opcionales, con su migración; verificar con test de integración que un día sin ellos se guarda y se lee como hasta ahora
- [ ] 1.2 Rechazar un recorrido incoherente —máximo menor que el mínimo, o cierre fuera de ambos— conservando el cierre; verificar con tests de cada caso
- [ ] 1.3 Leer apertura, máximo y mínimo del proveedor de Yahoo contra respuestas grabadas; verificar que un activo sin ellos sigue entregando su cierre
- [ ] 1.4 Dejar que CoinGecko entregue sólo el cierre sin que eso parezca un fallo; verificar con test que el activo queda marcado como sin recorrido
- [ ] 1.5 Volver a pedir el recorrido de lo ya descargado, en segundo plano y sin retrasar la puesta al día; verificar con test que el cierre guardado no cambia y que una segunda pasada no vuelve a pedir lo mismo

## 2. Entregar y agregar la serie

- [ ] 2.1 Llevar el recorrido diario a la serie de un activo y a sus contratos, distinguiendo el día sin recorrido del de recorrido nulo; verificar con tests de integración de la API
- [ ] 2.2 Implementar la agregación por semanas y por meses: apertura del primero, cierre del último, extremos de todos; verificar con tests de semana completa, de semana con días de mercado cerrado y de tramo sin ningún dato
- [ ] 2.3 Verificar con test que el máximo de un tramo es el mayor de los máximos de sus días y no el mayor de sus cierres, que es el error clásico al agregar
- [ ] 2.4 Admitir el intervalo en el endpoint de la serie de un activo, con el que sugiere el periodo por omisión; verificar con tests de integración de la API
- [ ] 2.5 Resolver «lo que va de año» como desde el 1 de enero en curso, y un periodo mayor que la historia disponible como lo que haya; verificar con tests

## 3. Los indicadores con el recorrido

- [ ] 3.1 Calcular el recorrido medio con máximos y mínimos cuando los haya, y de cierre a cierre cuando no, declarando cuál se ha usado; verificar con tests de ambos casos
- [ ] 3.2 Verificar con test que las dos formas no se mezclan en una misma serie
- [ ] 3.3 Entregar las bandas de volatilidad y la envolvente del recorrido para dibujarlas, con la ventana sobre la que se calculan; verificar con tests, incluida la serie demasiado corta

## 4. La escala de periodos

- [ ] 4.1 Sustituir el selector de la pantalla de un activo por la escala completa —1 semana, 1 mes, 3 meses, 6 meses, año en curso, 1 año, 2 años, 5 años y todo—; verificar manualmente con un activo largo y otro corto
- [ ] 4.2 Añadir el selector de intervalo —días, semanas, meses— con el que sugiere el periodo; verificar manualmente que cambiar el periodo cambia la sugerencia y que se puede llevar la contraria
- [ ] 4.3 Llevar la misma escala a la pantalla de evolución del patrimonio; verificar manualmente
- [ ] 4.4 Añadir los textos a los recursos es-ES y en, sin literales en el marcado; verificar que el escáner de localización pasa

## 5. Dibujar mejor

- [ ] 5.1 Dibujar velas cuando el activo tenga recorrido y los puntos sean pocos; verificar manualmente con tres meses de una acción
- [ ] 5.2 Dibujar la línea del cierre con la banda del recorrido cuando los puntos sean muchos; verificar manualmente con cinco años
- [ ] 5.3 Dibujar sólo la línea cuando el activo no tenga recorrido, diciendo por qué; verificar manualmente con POL, PEPE o TAO
- [ ] 5.4 Implementar el cursor que recorre la gráfica y lee el valor y la fecha del punto, leyendo de la serie entera y no de la dibujada; verificar manualmente en días, semanas y meses
- [ ] 5.5 Verificar con test que reducir o agregar para dibujar no cambia lo que el cursor lee ni lo que los indicadores calculan

## 6. Las cifras junto a la gráfica

- [ ] 6.1 Calcular y entregar el bloque de cifras: apertura, máximo y mínimo del último día; máximo, mínimo y variación del periodo; y máximo y mínimo de 52 semanas; verificar con tests de integración de la API
- [ ] 6.2 Verificar con test que las cifras del periodo cambian al cambiar el periodo, y que las de 52 semanas no
- [ ] 6.3 Mostrar el bloque bajo la gráfica, con las cifras que falten dichas como tales; verificar manualmente con un activo con recorrido y otro sin él
- [ ] 6.4 Mostrar las bandas de dispersión rotuladas como descripción del recorrido pasado, nunca como probabilidad; verificar manualmente que el rótulo no sugiere pronóstico

## 7. Comprobación con los datos reales

- [ ] 7.1 Comprobar en la aplicación los nueve periodos y los tres intervalos sobre MSTR.US, que tiene 6.723 cotizaciones desde 2000, midiendo lo que tarda cada combinación y dejándolo escrito en el cambio
- [ ] 7.2 Comprobar que POL, PEPE y TAO se ven bien sin recorrido y que la pantalla explica por qué
- [ ] 7.3 Comprobar cuántos de los 23 activos tienen recorrido tras el relleno y dejarlo escrito
- [ ] 7.4 Comprobar que el patrimonio, el coste, el resultado realizado, el efectivo y los dos ejercicios fiscales siguen saliendo idénticos a las cifras registradas en la validación con datos reales
