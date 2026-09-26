## 1. Que un backtest no pueda mentir

- [ ] 1.1 Calcular, para las magnitudes que usa un sistema, el periodo realmente cubierto a la resolución que la regla necesita, a partir del alcance que guarda la ingesta; verificar con tests de cobertura completa, de magnitud que empieza tarde y de tramo agregado por debajo de lo necesario
- [ ] 1.2 Devolver ese periodo en el resultado y no dar por contrastado lo anterior; verificar con test que un backtest pedido desde antes del primer dato lo dice en lugar de devolver un resultado plano
- [ ] 1.3 Avisar antes de ejecutar cuando una regla usa una magnitud que sólo se acumula desde que se empezó a grabar; verificar con test
- [ ] 1.4 Contar los días no evaluables por huecos y devolverlos con el resultado; verificar con test

## 2. Reglas y señales

- [ ] 2.1 Añadir las magnitudes de derivados como operandos de una regla; verificar con tests de una regla sobre financiación aceptada y de una sobre una magnitud inexistente rechazada al guardarla
- [ ] 2.2 Rechazar al guardar una regla de derivados declarada para un activo sin contrato, diciendo por qué; verificar con test
- [ ] 2.3 Implementar la evaluación de esas reglas en el motor determinista, sin mirar el futuro; verificar con tests de que dos evaluaciones seguidas dan lo mismo y de que no se usa dato posterior al instante evaluado
- [ ] 2.4 Hacer que una señal de derivados diga sus magnitudes, su fuente, su instante y si el dato era estimado; verificar con tests de señal sobre dato observado y sobre dato estimado
- [ ] 2.5 No emitir señal en un instante sin dato y dejar constancia de ello; verificar con tests de activo sin contrato y de instante dentro de un hueco

## 3. Hyperliquid como contraste

- [ ] 3.1 Implementar el adaptador de Hyperliquid para la financiación histórica y el interés abierto actual contra respuestas grabadas; verificar que el dato se marca como observado y no como estimado
- [ ] 3.2 Implementar el descubrimiento del contrato en Hyperliquid; verificar con test de activo cubierto y no cubierto
- [ ] 3.3 Verificar con test que el mismo activo cubierto por las dos fuentes produce dos series separadas, cada una con su procedencia, y que ninguna se promedia con la otra

## 4. Proponer activos que no se siguen

- [ ] 4.1 Decidir y documentar, con el coste de almacenamiento ya medido, hasta dónde se amplía la recogida a activos no seguidos y para qué magnitudes; dejarlo escrito en el cambio antes de implementarlo
- [ ] 4.2 Implementar la detección de condiciones destacables sobre activos fuera de la lista, con su umbral como dato; verificar con test que un activo seguido no se propone
- [ ] 4.3 Implementar la propuesta con su condición y sus cifras, y las acciones de aceptar y descartar; verificar con tests de que aceptar deja el activo en seguimiento y de que descartar impide que vuelva por la misma condición
- [ ] 4.4 Limitar cuántas propuestas se hacen y decir cuántas quedaron fuera; verificar con test de un movimiento general en el que la condición la cumplen muchos

## 5. La explicación redactada

- [ ] 5.1 Implementar el puerto que recibe señales y magnitudes ya calculadas y devuelve una explicación, siguiendo la forma de los usos de modelo que ya existen; verificar contra respuestas grabadas
- [ ] 5.2 Implementar la comprobación de que toda cifra del texto está entre las enviadas, descartando el texto que no lo cumpla y dejando constancia; verificar con tests de texto correcto y de texto con una cifra inventada
- [ ] 5.3 Devolver las señales sin explicación cuando el modelo falla o no responde; verificar con test que la consulta responde igual
- [ ] 5.4 No redactar nada cuando no hay señales ni magnitudes destacables; verificar con test

## 6. La pantalla

- [ ] 6.1 Ampliar los contratos y el endpoint con el dato de derivados de los activos seguidos, sus señales y su alcance; verificar con tests de integración de la API
- [ ] 6.2 Construir la pantalla con el estado de cada activo seguido y sus señales, distinguiendo el dato observado del estimado; verificar manualmente con activos de ambas fuentes
- [ ] 6.3 Mostrar la explicación redactada separada visualmente de lo calculado; verificar manualmente que se distingue sin leer el texto
- [ ] 6.4 Mostrar las propuestas de activos nuevos con aceptar y descartar; verificar manualmente que aceptar lo deja en la lista de seguimiento
- [ ] 6.5 Mostrar el alcance de cada magnitud y avisar de las fuentes ausentes arriba y no al pie; verificar manualmente que se ve antes de leer las cifras
- [ ] 6.6 Añadir los textos a los recursos es-ES y en, sin literales en el marcado; verificar que el escáner de localización pasa

## 7. Comprobación de conjunto

- [ ] 7.1 Escribir una regla sobre financiación, backtestearla desde el primer dato disponible y comprobar que el resultado declara la cobertura completa
- [ ] 7.2 Escribir una regla sobre liquidaciones y comprobar que el resultado declara que sólo cubre desde que se empezó a grabar, y no antes
- [ ] 7.3 Comprobar sobre los activos seguidos si alguna de las reglas escritas supera a no hacer nada, y dejar escrito el resultado, sea cual sea
