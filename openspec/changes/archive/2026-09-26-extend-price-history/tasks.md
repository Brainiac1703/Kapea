## 1. Recordar hasta dónde se ha pedido

- [x] 1.1 Añadir por activo la fecha más antigua por la que se ha preguntado y el identificador de proveedor con el que se preguntó, con su migración; verificar con test de integración que un activo nuevo nace sin ella y se guarda al primer relleno
- [x] 1.2 Rellenar el dato de los activos existentes con el primer día que ya tengan guardado, dentro de la migración; verificar con test que tras migrar no se vuelve a pedir lo que ya está
- [x] 1.3 Implementar la decisión de si el tramo de delante hace falta, comparando el suelo con lo ya pedido; verificar con tests de suelo por encima de lo pedido, suelo por debajo, y activo sin nada pedido todavía
- [x] 1.4 Invalidar lo pedido cuando el activo pasa a resolverse con otro identificador de proveedor; verificar con test que entonces sí se vuelve a pedir

## 2. Bajar el suelo

- [x] 2.1 Añadir a la configuración la fecha desde la que se quiere historia, con un valor por omisión muy anterior a cualquier cotización; verificar con test que el valor llega al cálculo del tramo
- [x] 2.2 Hacer que el suelo de cada activo sea esa fecha, conservando `StrategyLookback` y la primera adquisición como garantía por debajo; verificar con tests de activo sólo vigilado, activo con posición y activo con un sistema de ventana larga declarado
- [x] 2.3 Dejar de contar como «sin cobertura» al activo que sí tiene serie pero no llega al suelo; verificar con tests de activo sin ningún día y de activo con serie que empieza después del suelo

## 3. Poner al día antes de rellenar

- [x] 3.1 Separar la pasada en dos fases —primero el tramo de detrás de todos los activos, después el de delante— conservando el guardado activo a activo; verificar con test que con varios activos todos quedan al día antes de que empiece ningún relleno
- [x] 3.2 Conservar lo descargado cuando el proveedor deja de responder a mitad del relleno y reanudar en la vuelta siguiente; verificar con test de proveedor que falla en el tercer activo
- [x] 3.3 Registrar qué se rellenó y qué quedó pendiente, para poder seguir la primera pasada desde los registros; verificar manualmente en el entorno de desarrollo

## 4. Comprobación con los datos reales

- [x] 4.1 Lanzar el relleno sobre los 23 activos del usuario y comprobar que ninguna vuelta posterior vuelve a pedir tramos vacíos; verificar contando las peticiones al proveedor en dos vueltas seguidas
- [x] 4.2 Comprobar hasta dónde llega ahora cada activo y dejar escrito en el cambio cuántos días tiene cada uno y cuáles siguen limitados a un año por cubrirlos sólo CoinGecko
- [x] 4.3 Comprobar que el patrimonio, los resultados de los dos ejercicios y la evolución siguen dando exactamente lo mismo que antes de ampliar; verificar contra las cifras registradas en la validación con datos reales
