## MODIFIED Requirements

### Requirement: Consumo FIFO de lotes

Una venta DEBE consumir los lotes del mismo activo por orden ascendente de fecha de adquisición, agotando cada lote antes de pasar al siguiente. El criterio FIFO DEBE aplicarse por activo, no por cuenta: los lotes del mismo activo en distintas cuentas del usuario forman una única cola ordenada por fecha.

Cuando una venta exceda la cantidad disponible en una proporción despreciable frente a lo vendido, el sistema DEBE procesarla contra lo disponible y dar la posición por cerrada. Las plataformas redondean al vender una posición entera, y su cifra no siempre coincide al último decimal con la suma de lo que vendieron: rechazar la venta por eso deja el activo en cartera para siempre y el ejercicio sin un resultado que sí ocurrió.

El umbral DEBE ser relativo a la cantidad vendida y NO DEBE ser un valor absoluto. Lo que distingue un redondeo de un descuadre real es su proporción: la misma cantidad que es ruido frente a una posición grande puede ser la mitad de una pequeña.

Ese caso NO DEBE contar como incoherencia. Un contador que se llena de ruido deja de leerse, y entonces los descuadres de verdad vuelven a pasar desapercibidos.

Una venta que exceda lo disponible por encima del umbral DEBE seguir tratándose como hasta ahora: no se procesa y se marca la incoherencia.

#### Scenario: Venta que consume un lote parcialmente

- **WHEN** se vende una cantidad menor que la restante del lote más antiguo
- **THEN** el sistema reduce la cantidad restante de ese lote en la cantidad vendida y no toca los demás

#### Scenario: Venta que abarca varios lotes

- **WHEN** se vende una cantidad mayor que la restante del lote más antiguo
- **THEN** el sistema agota ese lote, continúa con el siguiente por fecha de adquisición y registra el resultado desglosado por lote consumido

#### Scenario: Lotes en cuentas distintas

- **WHEN** el usuario mantiene lotes del mismo activo en dos cuentas y vende en una de ellas
- **THEN** el consumo sigue el orden global de fechas de adquisición del activo, con independencia de la cuenta

#### Scenario: Empate de fecha de adquisición

- **WHEN** dos lotes del mismo activo comparten instante de adquisición
- **THEN** el sistema los consume en un orden estable y reproducible, de modo que recalcular produce siempre el mismo resultado

#### Scenario: Venta sin lotes suficientes

- **WHEN** una venta excede la cantidad disponible por encima del umbral de redondeo
- **THEN** el sistema no genera un resultado parcial silencioso: marca una inconsistencia de datos indicando el activo, la fecha y la cantidad que falta

#### Scenario: Posición vendida entera con redondeo de la plataforma

- **WHEN** una venta excede lo disponible en una proporción despreciable frente a lo vendido
- **THEN** el sistema consume todo lo disponible, cierra la posición y no marca ninguna incoherencia

#### Scenario: El ingreso no se pierde por el redondeo

- **WHEN** se procesa una venta así
- **THEN** el importe obtenido se atribuye entero a lo consumido, sin dejar ninguna parte sin repartir

#### Scenario: Un descuadre pequeño sobre una posición pequeña sí se marca

- **WHEN** la cantidad que falta es despreciable en términos absolutos pero apreciable frente a lo vendido
- **THEN** el sistema la trata como incoherencia, porque el umbral es relativo

#### Scenario: Venta de un activo sin ningún lote

- **WHEN** se vende un activo del que no consta ninguna adquisición
- **THEN** el sistema lo marca como incoherencia, sin que el umbral lo convierta en una venta válida
