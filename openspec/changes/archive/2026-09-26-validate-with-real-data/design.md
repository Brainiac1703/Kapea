## Context

Ver `proposal.md`. El núcleo ya está construido; lo que falta es contrastarlo. Las especificaciones y el diseño del núcleo están en el change archivado `add-portfolio-core`.

Dos incógnitas concretas que este change resuelve:

- **Las cabeceras de los formatos de XTB son una hipótesis.** Están en `XtbFormat.Known` y se escribieron sin una exportación real delante. El adaptador está construido para fallar entero y en voz alta si no las reconoce, así que un desajuste se verá; lo que no se sabe es cuánto hay que corregir.
- **El criterio FIFO no se ha contrastado nunca contra una declaración.** La batería de casos de referencia tiene cifras calculadas a mano, que comprueban que el motor hace lo que se pensó, no que lo que se pensó sea lo correcto.

## Goals / Non-Goals

**Goals:**

- Que un ejercicio ya presentado se pueda reproducir con Kapea y cuadre.
- Que cada diferencia encontrada quede documentada con su causa, no solo corregida.
- Que las correcciones se conviertan en casos de test permanentes, para que la misma diferencia no pueda volver.

**Non-Goals:**

- Cambiar requisitos. Si la validación obliga a cambiar comportamiento especificado, eso es otro change.
- Automatizar la comparación con la declaración. Se hace una vez y a mano; construir una herramienta para ello costaría más que el propio contraste.

## Decisions

### Se compara contra la declaración presentada, no contra otra herramienta

Es el único contraste que importa: la cifra que se declaró es la que tiene consecuencias. Compararse con otra calculadora solo trasladaría la confianza a un tercero igual de opinable.

### Toda diferencia se documenta antes de corregirse

Se escribe primero qué difiere y por qué, y después se toca el código. Al revés es fácil «arreglar» una diferencia ajustando el cálculo hasta que cuadre, que es la forma más rápida de esconder un error real detrás de una cifra bonita.

### Cada diferencia se convierte en caso de referencia

La batería de casos fiscales vive en los tests de dominio y no necesita base de datos, así que un caso nuevo cuesta unas líneas y protege para siempre.

### Los datos reales no entran sin decidir antes cómo

Un extracto lleva saldos, identificadores de cuenta y operaciones reales. Antes de añadir ningún fichero al repositorio se decide si va anonimizado, con importes reescalados o fuera del control de versiones. La decisión la toma el usuario, no este documento.

## Risks / Trade-offs

- **Puede aparecer un error en el cálculo fiscal** → Es el objetivo del change, no un riesgo a evitar. El coste de encontrarlo aquí es incomparablemente menor que el de encontrarlo en una inspección.
- **El formato real de XTB puede diferir mucho de lo supuesto** → El adaptador ya rechaza el fichero entero cuando no reconoce las cabeceras, así que el fallo será evidente y localizado. Corregir un formato es editar una entrada de tabla.
- **Los datos reales son personales** → Se decide su tratamiento antes de incorporarlos, no después.

## Open Questions

- **Cómo se incorporan los ficheros reales**: anonimizados, con importes reescalados o fuera del repositorio. Se decide con el usuario al empezar, y condiciona solo a los tests, no al código.
