## 1. Medir antes de decidir

- [x] 1.1 Implementar la rentabilidad ponderada por tiempo encadenando los tramos diarios; verificar con tests de una aportación sin rendimiento, de dos periodos encadenados y de un periodo sin movimientos
- [x] 1.2 Implementar la rentabilidad ponderada por dinero; verificar con test que coincide con la anterior sin aportaciones y que la supera cuando se aporta justo antes de una subida
- [x] 1.3 Implementar volatilidad, caída máxima desde el pico y tiempo de recuperación; verificar con tests de caída recuperada y de caída aún abierta
- [x] 1.4 Implementar la comparación con una referencia aplicándole las mismas aportaciones en las mismas fechas; verificar con tests, incluido el de una referencia a la que le falta algún día
- [x] 1.5 Marcar como incompletas las métricas calculadas sobre periodos con días sin precio; verificar con test
- [x] 1.6 Exponer el rendimiento en la API y en una pantalla, con el periodo seleccionable; verificar con test de integración y manualmente

## 2. Indicadores que faltan

- [x] 2.1 Implementar el MACD con sus dos medias y su línea de señal; verificar con tests de serie corta y de cruce
- [x] 2.2 Implementar las bandas de Bollinger; verificar con tests de serie plana y de precio fuera de banda
- [x] 2.3 Implementar el recorrido medio verdadero; verificar con tests de dos activos con recorridos distintos y de serie de un solo día
- [x] 2.4 Añadir la media de 200 días como filtro de tendencia disponible para las reglas; verificar con test

## 3. Sistemas de especulación como dato

- [x] 3.1 Modelar la condición y su combinación por conjunción y disyunción, con los operandos que el motor sabe evaluar; verificar con tests de cada tipo de condición
- [x] 3.2 Modelar el sistema con sus reglas de entrada, de salida, de objetivo y de nivel de salida, con validación que rechaza lo que no se sabrá ejecutar; verificar con tests, incluido el del indicador desconocido
- [x] 3.3 Implementar el versionado, conservando las versiones anteriores; verificar con tests de corrección y de simulación de una versión antigua
- [x] 3.4 Persistir sistemas y versiones; verificar con test de integración que una versión guardada se recupera igual
- [x] 3.5 Añadir la pantalla de sistemas con alta, corrección y consulta de versiones; verificar manualmente
- [x] 3.6 Dar de alta un sistema de serie, sencillo y documentado, para tener con qué empezar; verificar con test que se puede simular sin tocar nada

## 4. Motor de señales

- [x] 4.1 Implementar el evaluador de condiciones sobre la serie de un activo; verificar con tests de cada operador y de una condición compuesta
- [x] 4.2 Implementar el recorrido día a día que produce señales con su regla y su versión; verificar con tests de determinismo y de explicación de la señal
- [x] 4.3 Comprobar que ninguna señal usa datos posteriores a su día; verificar con test que evaluar el histórico entero da las mismas señales que evaluar día a día
- [x] 4.4 Implementar objetivo y nivel de salida cuando el sistema los declare; verificar con tests de sistema con objetivo y sin él
- [x] 4.5 No emitir señal cuando falten días en la ventana que la regla necesita, diciendo por qué; verificar con test
- [x] 4.6 Persistir las señales emitidas y exponerlas en la API; verificar con test de integración
- [x] 4.7 Añadir la pantalla de señales vigentes, con el sistema del que viene cada una; verificar manualmente

## 5. Simulador

- [x] 5.1 Implementar el recorrido de la simulación ejecutando al día siguiente de la señal; verificar con test que una compra nunca usa el precio del día de la señal
- [x] 5.2 Declarar la comisión junto a cada plataforma y aplicarla en cada operación simulada; verificar con tests de comisión dentro del precio y de comisión aparte
- [x] 5.3 Implementar el resultado después de impuestos con criterio FIFO y tramos configurables; verificar con tests de ganancia realizada y de cambio de tramos
- [x] 5.4 Implementar la comparación con no hacer nada en el mismo periodo; verificar con test de un sistema peor que la referencia
- [x] 5.5 Devolver el detalle de la simulación: operaciones, aciertos, caída máxima, comisiones e impuestos; verificar con test
- [x] 5.6 Exponer la simulación en la API y en una pantalla con su detalle operación a operación; verificar con test de integración y manualmente
- [x] 5.7 Simular el sistema de serie sobre el histórico real del usuario y dejar escrito el resultado frente a no hacer nada

## 6. Gestión del riesgo

- [x] 6.1 Implementar el tamaño de posición a partir del capital, el riesgo aceptado y la distancia al nivel de salida; verificar con tests de dos activos con recorridos distintos y de ausencia de nivel de salida
- [x] 6.2 Implementar el tope por posición y su aviso; verificar con test
- [x] 6.3 Implementar el aviso de concentración con su umbral declarado; verificar con test
- [x] 6.4 Implementar los pesos objetivo y las bandas de rebalanceo con la operación que devuelve al objetivo; verificar con tests dentro y fuera de la banda
- [ ] 6.5 Implementar el seguimiento de niveles de salida y objetivos de las posiciones abiertas; verificar con test de precio que alcanza el objetivo
- [ ] 6.6 Enseñar los avisos de riesgo en la cartera; verificar manualmente

## 7. Traducción asistida

- [x] 7.1 Definir el contrato de traducción de una descripción a reglas, con lo que no se ha sabido traducir marcado aparte; verificar con tests contra respuestas grabadas
- [x] 7.2 Implementar el traductor sobre el servicio existente, sin permitir guardar sin aprobación; verificar con test que una propuesta no aceptada no se guarda
- [x] 7.3 Implementar la explicación en castellano de lo que dicen las reglas de un sistema; verificar con test contra respuesta grabada
- [x] 7.4 Comprobar que sin servicio configurado todo sigue funcionando a mano; verificar con test
- [x] 7.5 Añadir a la pantalla de sistemas la revisión de una propuesta antes de guardarla; verificar manualmente

## 8. Diario de decisiones

- [x] 8.1 Modelar la anotación sobre un movimiento o una señal, con su fecha; verificar con tests
- [x] 8.2 Comprobar que anotar no altera el movimiento ni su huella de duplicado; verificar con test de reimportación
- [x] 8.3 Implementar la consulta del diario de un periodo con lo que pasó después; verificar con test
- [x] 8.4 Añadir la anotación a las pantallas de movimientos y de señales; verificar manualmente

## 9. Ideas de fuentes externas

- [x] 9.1 Modelar la idea con su activo, sentido, fecha, fuente, enlace y niveles opcionales; verificar con tests, incluido el de una idea sin niveles
- [x] 9.2 Modelar la fuente vigilada con su última publicación conocida; verificar con test de integración que se recupera igual
- [x] 9.3 Definir el contrato de extracción de ideas de un texto, con lo no extraído marcado aparte; verificar con tests contra respuestas grabadas, incluido un texto sin ninguna idea concreta
- [x] 9.4 Implementar la extracción sobre el servicio de IA existente, sin guardar el texto pegado ni permitir guardar sin aprobación; verificar con tests
- [x] 9.5 Implementar el aviso de publicación nueva contra la interfaz oficial de la fuente, degradando a registro manual cuando no haya forma de consultarla; verificar con tests contra respuestas grabadas
- [x] 9.6 Implementar el seguimiento de cada idea contra la serie de precios hasta objetivo, salida o caducidad; verificar con tests de los tres desenlaces
- [x] 9.7 Implementar el balance por fuente con el rendimiento neto de comisiones; verificar con tests, incluido el de una fuente sin ideas resueltas
- [x] 9.8 Añadir la pantalla de ideas con el pegado de texto, la revisión de lo propuesto y el balance por fuente; verificar manualmente

## 10. Validación con los datos del usuario

- [ ] 10.1 Contrastar la rentabilidad calculada con lo que dicen sus plataformas; documentar cada diferencia y su causa
- [ ] 10.2 Comprobar sobre su histórico cuánto se habría ido en comisiones con un sistema de operativa frecuente; dejarlo escrito
- [ ] 10.3 Dejar escrito qué sistemas se han simulado, con qué resultado y con qué supuestos
