## ADDED Requirements

### Requirement: Convergencia y divergencia de medias

El sistema DEBE calcular el MACD de una serie a partir de dos medias exponenciales y su línea de señal, devolviendo también la diferencia entre ambas.

#### Scenario: Serie más corta que la ventana larga

- **WHEN** la serie no llega para calcular la media larga
- **THEN** no se devuelve ningún valor

#### Scenario: Cruce de la línea de señal

- **WHEN** la línea principal pasa por encima de la de señal
- **THEN** la diferencia entre ambas cambia de signo ese día

### Requirement: Bandas de volatilidad

El sistema DEBE calcular las bandas de Bollinger: una media móvil y dos bandas separadas de ella un múltiplo de la desviación típica de la ventana.

#### Scenario: Serie plana

- **WHEN** todos los precios de la ventana son iguales
- **THEN** las dos bandas coinciden con la media

#### Scenario: Precio fuera de la banda

- **WHEN** el precio cierra por encima de la banda superior
- **THEN** el sistema lo refleja en el valor de ese día

### Requirement: Recorrido medio

El sistema DEBE calcular el recorrido medio verdadero de una serie, que mide cuánto se mueve un activo en un día.

#### Scenario: Activo tranquilo y activo movido

- **WHEN** se calcula sobre dos activos con recorridos distintos
- **THEN** el más movido tiene un recorrido medio mayor

#### Scenario: Serie con un solo día

- **WHEN** la serie tiene un único día
- **THEN** no se devuelve ningún valor, porque no hay variación que medir
