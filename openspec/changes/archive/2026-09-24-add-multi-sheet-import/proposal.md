## Why

La exportación actual de XTB no se puede importar. Es un libro de Excel con tres hojas —posiciones cerradas, operaciones de efectivo y posiciones abiertas— y cuatro filas de metadatos delante de cada tabla. Kapea lee sólo la primera hoja y toma su primera fila como cabecera, así que cree que las columnas se llaman «Account number» y «53882396», no reconoce ningún perfil y rechaza el fichero.

No es un caso raro de una plataforma: un informe con su cabecera administrativa delante y varias pestañas es la forma normal de exportar de muchos brókeres. Mientras el lector suponga «una hoja, cabecera en la primera fila», la promesa de dar de alta una plataforma sin escribir código se queda a medias.

Además, los perfiles de XTB que Kapea trae de serie se escribieron para una exportación anterior: ahora las columnas se llaman `Ticker`, `Position ID` y `Open Time (UTC)`, y aparecen conceptos que no estaban.

## What Changes

- El lector de ficheros deja de suponer que la tabla empieza en la primera fila de la primera hoja: busca dónde está, saltando el preámbulo, y puede leer cualquiera de las hojas del libro.
- Una sola subida puede alimentarse de varias hojas del mismo libro, cada una con el perfil que la reconoce. El usuario sube el fichero una vez.
- Un perfil puede decir a qué hoja se aplica, para que dos formatos parecidos dentro del mismo libro no se confundan.
- Los perfiles de XTB se actualizan a su exportación actual: las cabeceras nuevas y los conceptos que faltaban.
- Lo que ya llega por otra hoja deja de importarse dos veces. Las compras y las ventas salen de las posiciones cerradas, que son las únicas que traen cantidad y precio; en la hoja de efectivo se descartan, junto a la fila de totales del informe, y se cuentan entre los registros sin efecto financiero.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `import-profiles`: el emparejamiento debe mirar todas las hojas de un libro y encontrar la tabla aunque no empiece en la primera fila; un perfil puede declarar la hoja que reconoce; y una importación puede aplicar varios perfiles, uno por hoja.
- `transaction-import`: una subida puede producir movimientos leídos de varias hojas del mismo fichero, sin duplicar lo que aparece en dos de ellas.

## Impact

- `src/Kapea.Infrastructure/Import/Tabular/TabularReader.cs`: lectura por hoja y localización de la fila de cabeceras.
- `src/Kapea.Infrastructure/Import/Tabular/FileInspector.cs` y `ProfileFileImportAdapter.cs`: emparejar por hoja y leer varias.
- `src/Kapea.Domain/ImportProfiles/`: la hoja como parte del perfil, con su migración.
- `src/Kapea.Infrastructure/Import/Tabular/BuiltInProfiles.cs`: perfiles de XTB al día, y cómo llega esa actualización a una base ya sembrada.
- `src/Kapea.Client`: la vista previa, que debe seguir enseñando lo que se importará y de qué hoja sale.
