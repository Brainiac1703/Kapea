## ADDED Requirements

### Requirement: Una simulación declara el periodo que ha podido cubrir

Una simulación DEBE declarar, por cada magnitud que usan sus reglas, desde qué instante había dato. Cuando el periodo pedido empieza antes que alguna de esas magnitudes, el resultado NO DEBE presentarse como si cubriera todo el periodo.

Un resultado que parece válido sobre un periodo sin dato es peor que no tenerlo: lleva a confiar en una regla que nunca se ha comprobado.

#### Scenario: Periodo cubierto por completo

- **WHEN** todas las magnitudes que usa el sistema tienen dato en todo el periodo pedido
- **THEN** el resultado se presenta sobre ese periodo sin salvedades

#### Scenario: Regla sobre una magnitud que empieza más tarde

- **WHEN** una magnitud sólo tiene dato desde una fecha posterior al inicio pedido
- **THEN** el resultado dice desde cuándo se ha podido contrastar realmente y no da por comprobado lo anterior

#### Scenario: Magnitud sin histórico

- **WHEN** una regla usa una magnitud que sólo se acumula desde que se empezó a grabar
- **THEN** la simulación lo dice antes de ejecutarse y el usuario sabe qué está contrastando

#### Scenario: Huecos dentro del periodo

- **WHEN** una magnitud tiene huecos dentro del periodo simulado
- **THEN** el resultado dice cuántos días no se pudieron evaluar
