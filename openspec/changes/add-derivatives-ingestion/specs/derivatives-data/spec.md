## Purpose

Guarda lo que las plataformas de derivados dicen del dinero apalancado —tipo de financiación, interés abierto y liquidaciones— para que los sistemas puedan mirar dónde está apostado y no sólo qué ha hecho el precio.

## ADDED Requirements

### Requirement: Un activo sabe cuál es su contrato perpetuo

Un activo DEBE poder guardar, por cada fuente de derivados, el contrato perpetuo que le corresponde y el factor por el que esa fuente multiplica la cantidad. Una fuente que cotiza mil unidades por contrato NO DEBE compararse con otra que cotiza una sin corregir ese factor.

Un activo sin contrato asociado DEBE seguir funcionando con todo lo demás: la mayoría de la renta variable y buena parte de las criptomonedas pequeñas no tienen perpetuo, y eso no es un error ni un dato que falte.

#### Scenario: Activo con perpetuo en varias fuentes

- **WHEN** un activo seguido cotiza como perpetuo en más de una fuente
- **THEN** el sistema guarda el contrato de cada una y puede pedirles dato por separado

#### Scenario: Contrato con factor de multiplicación

- **WHEN** una fuente cotiza el contrato en múltiplos de la unidad del activo
- **THEN** las magnitudes se convierten a la unidad del activo antes de guardarlas

#### Scenario: Activo sin perpetuo

- **WHEN** un activo seguido no cotiza como perpetuo en ninguna fuente
- **THEN** el sistema lo dice al mirarlo y no lo trata como dato pendiente de llegar

### Requirement: Tres magnitudes con procedencia y momento

El sistema DEBE guardar, por contrato y fuente, el tipo de financiación, el interés abierto y las liquidaciones. Cada dato DEBE decir de qué fuente viene y a qué instante corresponde.

Una magnitud DEBE distinguir si la fuente la observa o la estima. Una fuente que publica posiciones reales y otra que las deduce suponiendo un apalancamiento no dicen lo mismo, y presentarlas igual haría creer que coinciden cuando difieren.

#### Scenario: Dato de dos fuentes sobre el mismo activo

- **WHEN** dos fuentes publican el interés abierto del mismo activo
- **THEN** el sistema guarda los dos por separado, cada uno con su procedencia

#### Scenario: Dato estimado

- **WHEN** una fuente deduce una magnitud en lugar de observarla
- **THEN** el sistema lo marca como estimado y lo dice allí donde se enseñe

### Requirement: Cada magnitud declara desde cuándo hay dato

El sistema DEBE saber, por contrato, fuente y magnitud, desde qué instante hay dato guardado y sin huecos. Ese alcance DEBE poder consultarse, porque de él depende lo que se puede contrastar.

Un hueco por una caída o un arranque tardío NO DEBE rellenarse inventando: DEBE quedar como hueco y contarse como tal en el alcance.

#### Scenario: Magnitud rellenada hacia atrás

- **WHEN** se descarga el histórico disponible de una magnitud
- **THEN** su alcance pasa a empezar en el primer instante descargado

#### Scenario: Magnitud que sólo se puede acumular

- **WHEN** una magnitud no tiene histórico descargable y sólo se graba en directo
- **THEN** su alcance empieza el día en que se empezó a grabar, y el sistema lo dice

#### Scenario: Hueco por una interrupción

- **WHEN** la grabación se interrumpe y se reanuda
- **THEN** el intervalo perdido consta como hueco y no como dato

### Requirement: La granularidad que se conserva está declarada

El sistema DEBE declarar con qué resolución guarda cada magnitud y durante cuánto tiempo la conserva a esa resolución. Guardar el detalle más fino de forma indefinida NO es un objetivo: lo es que una regla pueda evaluarse sobre el periodo que declara cubrir.

Agregar dato antiguo a una resolución menor DEBE conservar lo que las reglas necesitan de él y DEBE quedar dicho en el alcance.

#### Scenario: Dato reciente y dato antiguo

- **WHEN** el usuario consulta una magnitud de hace dos años y otra de ayer
- **THEN** obtiene las dos, y el sistema dice con qué resolución está cada una

#### Scenario: Agregación de dato antiguo

- **WHEN** el dato pasa a conservarse con menos resolución
- **THEN** el alcance lo refleja y una regla que necesitaba más resolución deja de darse por cubierta en ese periodo

### Requirement: Una fuente caída no impide usar las demás

Una fuente que no responde, agota su cuota o cierra la conexión NO DEBE impedir que se use lo ya guardado ni que se pidan las demás. El sistema DEBE conservar lo obtenido y reanudar donde lo dejó.

Una pantalla que enseñe este dato DEBE decir que falta una fuente en lugar de presentar lo que queda como si estuviera completo.

#### Scenario: Fuente que no responde

- **WHEN** una fuente falla durante una ingesta
- **THEN** el sistema guarda lo obtenido de las demás y lo reintenta en la siguiente vuelta

#### Scenario: Conexión en directo interrumpida

- **WHEN** se corta la conexión que graba las liquidaciones
- **THEN** el sistema vuelve a conectarse y deja constancia del intervalo no grabado

#### Scenario: Pantalla con una fuente ausente

- **WHEN** el usuario mira los derivados y falta una fuente
- **THEN** la pantalla lo dice y enseña lo que hay
