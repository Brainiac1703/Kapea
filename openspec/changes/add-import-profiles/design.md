## Context

Ver `proposal.md` para la motivación. Lo que sigue parte de lo que ya existe y está en `openspec/specs`.

El motor de importación ya tiene las costuras necesarias: `IFileImportAdapter` entrega registros normalizados, la fase de preparación clasifica sin escribir en el dominio, la vista previa se confirma o se descarta, y cada movimiento conserva su fila original y su huella de deduplicación. Este change no toca esa tubería: sustituye la pieza que hoy sabe leer solo XTB por una que lee lo que le diga un perfil.

Restricciones que condicionan el diseño:

- **El núcleo fiscal es determinista y auditable.** Es la restricción que decide la forma entera del change: la IA produce un mapeo, jamás una cifra.
- **Azure OpenAI es opcional.** La aplicación tiene que funcionar sin él. Un servicio externo caído no puede impedir dar de alta un bróker.
- **Los extractos son datos personales.** Al modelo van cabeceras y hasta tres filas; el resto no sale del servidor.
- **Decisión del usuario, tomada en sesión**: el mapeo se acepta sin preguntar cuando la propuesta es concluyente. La contrapartida se compensa en la vista previa, no relajando la garantía.

## Goals / Non-Goals

**Goals:**

- Que dar de alta un bróker que exporta CSV no requiera escribir código.
- Que la interpretación de cada movimiento sea reconstruible: qué perfil, qué versión, qué fila.
- Que una importación repetida no llame a la IA ni dependa de que esté disponible.
- Que el adaptador de XTB deje de ser un caso especial.

**Non-Goals:**

- Extractos en PDF y orígenes no tabulares. Allí la IA extraería cifras, no mapeos, y eso cambia la garantía; merece su propio change.
- Deducir el perfil sin ninguna intervención posible. Siempre hay una pantalla donde mirarlo y corregirlo.
- Adivinar el activo. Traducir un símbolo del origen al catálogo ya lo resuelve el catálogo de activos, y no cambia aquí.

## Decisions

### El perfil es un dato, no código

Un perfil guarda: las cabeceras que reconoce, el delimitador, las convenciones de número y fecha, la correspondencia columna → campo del movimiento normalizado, y la traducción concepto → tipo de movimiento. Nada de eso necesita compilarse.

**Alternativa descartada** — seguir escribiendo un adaptador por plataforma: es lo que hay hoy, y es justo lo que no escala. Cada bróker nuevo sería una tarea de programación y cada cambio de formato, una versión de la aplicación.

### La IA propone el mapeo; el motor determinista importa

El modelo recibe cabeceras y hasta tres filas y devuelve el mapeo con una confianza por campo. Ese mapeo se persiste. A partir de ahí, importar es aplicar el perfil: leer la columna que toca y convertirla con las convenciones declaradas.

**Alternativa descartada** — pedir al modelo los movimientos fila a fila: no es reproducible (recalcular un ejercicio ya presentado podría dar otras cifras), no es auditable (un mapeo se compara, una respuesta no), cuesta en cada importación, y un error se reparte por miles de filas en lugar de verse una vez.

### La vista previa enseña filas interpretadas, no solo recuentos

Es la contrapartida de aceptar un mapeo sin preguntar. Aunque el perfil entre solo, antes de persistir nada se ven las primeras filas ya normalizadas: fecha, tipo, activo, cantidad e importe. Un mapeo equivocado —la columna de comisión tomada por el importe— salta a la vista ahí.

Sin esto, «aceptar cuando la IA está segura» sería aceptar a ciegas, porque un modelo puede estar seguro y equivocado.

### El perfil se versiona y viaja con el movimiento

Editar un perfil crea una versión nueva; la anterior se conserva. Cada movimiento registra perfil y versión.

Es lo que permite responder «esta cifra salió de interpretar la columna 5 como importe, según la versión 2 del perfil de XTB», que es exactamente el tipo de respuesta que hace falta si una cifra fiscal se cuestiona. También acota el daño de un mapeo equivocado: se sabe qué importaciones lo usaron.

### Las plataformas pasan a ser entidades

Hoy son un enumerado de tres valores, y eso obliga a tocar código, migraciones y cliente para añadir un bróker. Pasan a ser filas con su nombre y su forma de importación (fichero o API).

Los adaptadores de API siguen siendo código —Kraken y Bit2Me tienen su firma, su paginación y sus alias, y eso no lo deduce un modelo—, así que una plataforma de API sigue necesitando su adaptador registrado. La diferencia es que una plataforma de fichero ya no.

**Consecuencia asumida**: es un cambio incompatible en el contrato de cuentas. Con un solo usuario y el histórico ya importado el coste es una migración de datos que traduce los tres valores actuales a filas.

### La huella de deduplicación no cambia de forma

Se sigue calculando sobre el identificador natural o sobre los datos financieros más el número de fila. Cambiarla ahora invalidaría la deduplicación del histórico ya importado, y reimportar duplicaría todo.

Como consecuencia, los perfiles de XTB de serie tienen que reproducir la interpretación actual campo a campo. Es una condición verificable, y así está escrita en las tareas.

### El umbral de confianza es configuración, no constante

Cuánta confianza basta para no preguntar es un juicio, y el valor bueno solo se sabe usándolo. Sale a configuración con un valor por omisión prudente, para poder subirlo o bajarlo sin tocar código.

## Risks / Trade-offs

- **Un mapeo equivocado con confianza alta entra sin preguntar** → Es la contrapartida de la decisión tomada. Se compensa con las filas interpretadas en la vista previa, con el versionado que permite localizar qué importaciones lo usaron, y con que el perfil sea corregible y reimportable.
- **El modelo confunde dos columnas parecidas** —importe y comisión, o cantidad e importe— → Es el error más probable y el más caro. Además de la vista previa, la propuesta se valida contra las filas de ejemplo: si la columna mapeada como fecha no parsea como fecha en las tres filas, la propuesta se marca como no concluyente aunque el modelo diga estar seguro.
- **Dependencia de un servicio externo** → Acotada por diseño: solo interviene al conocer un formato nuevo, y el mapeo manual cubre su ausencia.
- **Datos personales saliendo de la máquina** → Cabeceras y tres filas. Se documenta en la propia pantalla, para que quien pulsa sepa qué se envía.
- **Los perfiles de serie de XTB podrían no reproducir el comportamiento actual** → Se comprueba con los tests de extremo a extremo que ya existen: los mismos ficheros tienen que producir los mismos movimientos.

## Open Questions

- **Qué modelo de Azure OpenAI y con qué coste por propuesta.** No cambia el diseño: la llamada está detrás de un puerto y el mapeo es un contrato pequeño. Se decide al desplegar.
- **Si los perfiles deberían poder compartirse entre usuarios** cuando Kapea pase a multiusuario. Hoy hay un solo usuario y la pregunta no tiene consecuencias; el perfil nace ligado a la plataforma, no a la persona, así que la puerta queda abierta.
