## Purpose

Convierte el dato de derivados en señales evaluables con el mismo motor determinista que las de precio, diciendo siempre sobre qué dato se apoyan y hasta dónde se han podido contrastar.

## ADDED Requirements

### Requirement: Una señal de derivados dice sobre qué se apoya

Una señal derivada del dato de derivados DEBE decir, además de lo que dice cualquier señal, qué magnitudes ha usado, de qué fuente y a qué instante corresponden.

Una señal apoyada en un dato marcado como estimado DEBE señalarse como tal. Presentarla igual que una apoyada en posiciones observadas haría creer que ambas tienen el mismo fundamento.

#### Scenario: Señal sobre financiación extrema

- **WHEN** un sistema señala por un tipo de financiación fuera de su rango habitual
- **THEN** la señal dice la magnitud, la fuente, el instante y el valor que la disparó

#### Scenario: Señal apoyada en dato estimado

- **WHEN** la magnitud que dispara la señal proviene de una estimación
- **THEN** la señal lo indica junto al valor

### Requirement: Una señal no se emite sobre un periodo sin dato

Un sistema NO DEBE emitir una señal de derivados sobre un instante para el que la magnitud que necesita no tiene dato. El sistema DEBE decir que no pudo evaluarse, en lugar de no emitir nada sin explicación o de tratar la ausencia como un valor.

#### Scenario: Activo sin contrato perpetuo

- **WHEN** un sistema con reglas de derivados se evalúa sobre un activo sin perpetuo
- **THEN** el sistema dice que no se puede evaluar ahí y sigue con los demás

#### Scenario: Instante dentro de un hueco

- **WHEN** la magnitud tiene un hueco en el instante que se evalúa
- **THEN** no se emite señal y queda constancia de que faltó dato

### Requirement: El mercado entero puede proponer activos que no se siguen

El dato de derivados describe el mercado, no la cartera, así que el sistema PUEDE detectar una condición destacable en un activo que el usuario no sigue y proponérselo.

Una propuesta NO DEBE añadir nada por su cuenta: es una sugerencia que el usuario acepta o descarta. Un activo descartado NO DEBE volver a proponerse por la misma condición sin que medie algo nuevo.

El sistema DEBE limitar cuántas propuestas hace, para que la utilidad de la lista no se pierda bajo el ruido del mercado.

#### Scenario: Condición destacable fuera de la lista

- **WHEN** un activo que el usuario no sigue cumple una condición destacable
- **THEN** el sistema se lo propone, diciendo qué condición y con qué cifras

#### Scenario: Aceptar una propuesta

- **WHEN** el usuario acepta una propuesta
- **THEN** el activo pasa a la lista de seguimiento como si lo hubiera añadido él

#### Scenario: Descartar una propuesta

- **WHEN** el usuario descarta una propuesta
- **THEN** no vuelve a proponerse por esa misma condición

#### Scenario: Muchos activos a la vez

- **WHEN** el mercado entero cumple la condición en un movimiento general
- **THEN** el sistema propone sólo los más destacados y dice cuántos ha dejado fuera
