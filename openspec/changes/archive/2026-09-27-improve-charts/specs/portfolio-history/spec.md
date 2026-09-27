## ADDED Requirements

### Requirement: La serie de un activo entrega su recorrido diario

La serie de un activo DEBE entregar, de cada día que lo tenga, la apertura, el máximo y el mínimo además del cierre. Un día sin recorrido DEBE distinguirse de uno cuyo recorrido fue nulo.

#### Scenario: Activo con recorrido

- **WHEN** se pide la serie de un activo cuyo proveedor da el recorrido
- **THEN** cada día lleva apertura, máximo, mínimo y cierre

#### Scenario: Activo sin recorrido

- **WHEN** se pide la serie de un activo cuyo proveedor sólo da el cierre
- **THEN** los días llevan sólo el cierre, y la serie dice que no hay recorrido

### Requirement: La serie se puede agregar por semanas o por meses

El sistema DEBE poder entregar la serie de un activo agregada por semanas o por meses, además de por días.

Cada tramo agregado DEBE llevar la apertura del primer día con dato, el cierre del último, y el máximo y el mínimo de todos. Un tramo sin ningún día con dato NO DEBE inventarse.

Agregar NO DEBE cambiar los extremos: el máximo de un mes es el mayor de sus días, no el mayor de sus cierres.

#### Scenario: Cinco años por semanas

- **WHEN** se pide la serie de cinco años agregada por semanas
- **THEN** cada punto cubre una semana, con su apertura, su cierre y sus extremos

#### Scenario: Semana con días sin cotización

- **WHEN** una semana tiene días de mercado cerrado
- **THEN** el tramo se calcula con los días que sí cotizaron

#### Scenario: Tramo entero sin datos

- **WHEN** un mes no tiene ningún día con dato
- **THEN** no se devuelve un punto inventado para ese mes

#### Scenario: Los extremos no se pierden al agregar

- **WHEN** un día de la semana tuvo un máximo superior a todos los cierres de esa semana
- **THEN** el máximo del tramo es ese, y no el mayor de los cierres

### Requirement: El periodo se elige de una escala

Quien mire una serie DEBE poder elegir el periodo de una escala que vaya de una semana a todo el histórico, pasando por lo que va de año.

Saltar de un año a todo obliga a mirar años cuando se quieren ver meses. La escala DEBE ofrecer al menos: una semana, un mes, tres meses, seis meses, lo que va de año, un año, dos años, cinco años y todo.

#### Scenario: Lo que va de año

- **WHEN** el usuario pide lo que va de año
- **THEN** la serie empieza el 1 de enero del año en curso

#### Scenario: Periodo más largo que la historia disponible

- **WHEN** se piden cinco años de un activo que sólo tiene uno
- **THEN** se devuelve lo que hay, sin tratarlo como un error
