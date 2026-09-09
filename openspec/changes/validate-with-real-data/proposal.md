## Why

El núcleo de cartera está construido y probado contra ficheros sintéticos y respuestas grabadas. Eso demuestra que el código hace lo que dice, no que las cifras sean las correctas: las cabeceras de los formatos de XTB son una hipótesis razonada, y el criterio FIFO no se ha contrastado nunca contra una declaración presentada.

Este change cierra esa distancia. No añade funcionalidad: comprueba que la que hay produce las cifras que ya se declararon, y convierte cada discrepancia en un caso de test permanente.

Sale de `add-portfolio-core` porque no depende del código sino de datos que solo el usuario tiene: exportaciones reales de xStation5, el histórico de Kraken y Bit2Me, y una declaración de la renta ya presentada.

## What Changes

- Se incorporan exportaciones reales de xStation5 como casos de test de extremo a extremo, con las cifras esperadas.
- Se contrasta el resultado de un ejercicio ya declarado contra lo presentado, y se documenta cada diferencia con su causa.
- Cada discrepancia encontrada se corrige y se añade a la batería de casos fiscales de referencia.
- Se comprueba que un recálculo desde cero sobre el histórico real reproduce exactamente los mismos lotes, posiciones y resultados.
- **Sin cambios de comportamiento previstos.** Si la validación obliga a alguno, se propone aparte: aquí solo se corrigen errores frente a lo que las specs ya exigen.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

Ninguna. Este change valida el comportamiento ya especificado; no cambia ningún requisito.

## Impact

- **Datos necesarios del usuario**: exportaciones de xStation5 (operaciones de efectivo, posiciones cerradas y abiertas), histórico de Kraken y Bit2Me, y una declaración ya presentada con la que comparar.
- **Privacidad**: los ficheros reales llevan datos financieros personales. Los casos de test se incorporan anonimizados o con importes reescalados si el usuario lo prefiere, y esa decisión se toma antes de añadir nada al repositorio.
- **Riesgo real**: es el change que puede destapar que una cifra fiscal está mal. Ese es justamente su valor.
