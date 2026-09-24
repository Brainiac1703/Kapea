## 1. Leer el fichero por hojas

- [x] 1.1 Hacer que el lector tabular devuelva todas las hojas del libro con sus filas en crudo, manteniendo el comportamiento actual para CSV, y verificar con una prueba de unidad sobre un libro de tres hojas que las devuelve todas
- [x] 1.2 Localizar en cada hoja la fila de cabeceras como la primera que reconoce un perfil de la plataforma, tratando lo anterior como preámbulo, y verificar con una prueba que un fichero con cuatro filas de metadatos delante se lee entero y sin ellas
- [x] 1.3 Cubrir con una prueba que un libro sin ninguna hoja reconocible informa de las cabeceras encontradas en lugar de fallar en seco

## 2. Una subida, varios perfiles

- [x] 2.1 Cambiar la inspección para que devuelva las hojas reconocidas, cada una con su perfil, y verificar con una prueba que un libro con dos hojas útiles devuelve dos emparejamientos
- [x] 2.2 Importar todas las hojas reconocidas en una sola ejecución, conservando de qué hoja y con qué perfil entra cada movimiento, y verificar con una prueba de integración que una subida produce los movimientos de las dos hojas
- [x] 2.3 Ignorar sin fallar las hojas que ningún perfil reconoce, dejando constancia, y verificar con una prueba que la hoja de posiciones abiertas no impide importar las otras dos
- [x] 2.4 Mostrar en la vista previa de qué hoja sale cada fila y con qué perfil, y verificar en el navegador que se entiende antes de confirmar

## 3. La hoja como parte del perfil

- [x] 3.1 Añadir al perfil el nombre de hoja opcional, con su configuración de EF Core y su migración, y verificar que la columna nace vacía para los perfiles existentes
- [x] 3.2 Usarlo al emparejar, de modo que un perfil con hoja declarada sólo se aplique a esa, y verificar con una prueba que dos hojas de cabeceras parecidas no se cruzan
- [x] 3.3 Dejar editar la hoja desde la pantalla de perfiles, y verificar que el escáner de localización sigue pasando

## 4. Lo que no se cuenta dos veces

- [x] 4.1 Descartar como sin efecto financiero los conceptos de compra y venta de la hoja de efectivo, y verificar con una prueba que su dinero no se cuenta dos veces cuando la otra hoja ya los aporta
- [x] 4.2 Descartar las filas de totales, reconocidas por no traer fecha ni identificador, y verificar con una prueba que no entran como movimiento y sí en el recuento de descartados

## 5. XTB al día

- [x] 5.1 Añadir los perfiles del informe de XTB con las cabeceras y los conceptos actuales —compra, venta, interés, retención del interés y comisión—, y verificar con una prueba que reconocen su forma hoja por hoja
- [x] 5.2 Conservar intactos los perfiles del formato anterior, porque los dos conviven y un perfil sólo reconoce un fichero si están todas sus cabeceras, y verificar que las pruebas del formato antiguo siguen pasando
- [x] 5.3 Importar el fichero real del usuario en desarrollo y verificar que entran las diez compras y las diez ventas con su cantidad y su precio, los tres ingresos, los cinco intereses, las cinco retenciones y las cuatro comisiones, y que no queda ningún movimiento sin clasificar

## 6. Cierre

- [x] 6.1 Ejecutar la compilación y toda la batería de pruebas y verificar que pasan
- [x] 6.2 Comprobar en el navegador que la cartera de XTB cuadra: sin posiciones abiertas, con su resultado realizado y con el saldo que deja el informe
- [x] 6.3 Documentar en `docs/uso.md` que un libro con varias hojas se importa de una vez y qué hace Kapea con cada una
