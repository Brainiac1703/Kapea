## Why

Kapea nace atada a tres plataformas. XTB, Kraken y Bit2Me son las que el usuario tiene hoy, pero no hay ninguna razón de fondo para que la aplicación solo entienda esas tres: un extracto de movimientos es una tabla con fechas, cantidades e importes, y eso lo exporta cualquier bróker.

Hoy cada plataforma necesita un adaptador escrito a mano. XTB tiene el suyo, con sus formatos codificados en el binario; añadir un bróker nuevo es trabajo de programación, y cada vez que uno cambia su exportación la importación se rompe hasta que alguien la arregla. Para una aplicación personal que quiere aceptar «el fichero que sea», eso no escala.

La propuesta inicial ya anticipaba la salida: usar IA para normalizar formatos, sin dejar que toque el cálculo. Este change la concreta. Un modelo mira las cabeceras y dos filas de ejemplo y responde qué es cada columna y qué significa cada concepto. Esa respuesta se guarda como **perfil de importación** y, a partir de ahí, importa el motor determinista de siempre. La segunda vez que subes un extracto del mismo bróker no se llama a la IA.

La distinción es deliberada y es lo que hace el change defendible: **la IA deduce el formato, nunca las cifras**. Un mapeo se guarda, se lee, se compara y se corrige; una respuesta de un modelo sobre 4.000 filas, no. Y recalcular un ejercicio ya presentado tiene que dar exactamente lo mismo dentro de tres años.

## What Changes

- **Perfil de importación** como concepto de primer nivel: correspondencia entre las columnas del fichero y los campos del movimiento normalizado, más la traducción de cada concepto del origen a un tipo de movimiento. Con versión, para que un movimiento importado sepa con qué perfil se interpretó.
- **Adaptador de fichero genérico** guiado por perfiles. Sustituye al adaptador de XTB como pieza especial: sus dos formatos pasan a ser perfiles que Kapea trae de serie.
- **Propuesta de mapeo por IA**: se envían al modelo las cabeceras y hasta tres filas de ejemplo, nunca el fichero entero. Devuelve el mapeo con una confianza por campo.
- **Confirmación cuando el modelo duda**: si falta algún campo obligatorio, si alguna confianza queda por debajo del umbral o si algún concepto no se ha podido traducir, se pide confirmación. Si no, el perfil se acepta y la importación sigue.
- **Mapeo manual siempre disponible**: la misma pantalla, eligiendo las columnas a mano. Sin Azure OpenAI configurado la aplicación funciona igual; solo desaparece la propuesta automática.
- **Vista previa con filas interpretadas**: además de los recuentos, las primeras filas ya normalizadas —fecha, tipo, activo, cantidad, importe—. Es lo que permite ver un mapeo equivocado antes de persistir nada, también cuando se aceptó solo.
- **Las plataformas dejan de estar codificadas**: pasan a ser datos, con su forma de importación asociada. Añadir un bróker de fichero deja de tocar código.
- **Kapea pasa a ser multiplataforma.** XTB, Kraken y Bit2Me dejan de ser las plataformas soportadas para ser las que vienen configuradas de serie. Cualquier bróker que exporte una tabla de movimientos se da de alta desde la aplicación.
- **BREAKING** para el contrato de cuentas: la plataforma deja de ser un conjunto cerrado de tres valores.

## Capabilities

### New Capabilities

- `import-profiles`: perfiles de importación —creación manual o propuesta por IA, versionado, emparejamiento con un fichero, y la trazabilidad de qué perfil interpretó cada movimiento.

### Modified Capabilities

- `transaction-import`: el adaptador de fichero pasa a ser genérico y guiado por perfiles; la vista previa incorpora las filas ya interpretadas.
- `transaction-import/xtb-file`: los formatos de XTB dejan de estar codificados y se distribuyen como perfiles de serie, conservando las garantías actuales.
- `portfolio-domain`: la plataforma de una cuenta deja de ser un conjunto cerrado.

## Impact

- **Código**: nuevo adaptador genérico y el motor de perfiles; el adaptador de XTB se reduce a perfiles de datos; el modelo de plataformas cambia de enumerado a entidad; la pantalla de importación gana el paso de mapeo.
- **Infraestructura**: Azure OpenAI entra en el proyecto por primera vez, y solo aquí. Es opcional: sin él se mapea a mano.
- **Privacidad**: al modelo se le envían cabeceras y hasta tres filas de ejemplo. El resto del fichero no sale de la máquina.
- **Datos existentes**: los movimientos ya importados no se tocan. Los perfiles de XTB de serie reproducen el comportamiento actual, así que una reimportación sigue detectando los duplicados.
- **Fuera de alcance**: extractos en PDF y cualquier otro origen no tabular. Ahí la IA tendría que extraer cifras, no solo mapear, y eso merece su propio change.
