## Context

Ver `proposal.md` — Why para el motivo. Aquí sólo lo que condiciona la forma de arreglarlo.

Tres piezas se reparten hoy la decisión, y ninguna tiene sola la información que hace falta:

- `IPriceHistoryProvider.GetHistoryAsync` devuelve `Task<IReadOnlyList<DailyPrice>>`. Su propio comentario declara el contrato actual: «Un proveedor que no cubra un activo devuelve una serie vacía, nunca un error». La regla es buena —un proveedor caído no puede tumbar una pasada entera— pero convierte el fallo y la ausencia en el mismo valor.
- `PriceHistoryDispatcher` implementa el mismo puerto y recorre los proveedores en orden, pidiendo a cada uno sólo los días que el anterior no trajo. Es el que tiene que componer la respuesta: puede traer días de CoinGecko habiendo fallado Yahoo.
- `PriceHistoryUpdater.UpdateAsync` decide, tramo a tramo, si llamar a `RecordReachAsync`. Hoy lo llama siempre.

`PriceHistoryReach` vive en Domain y se persiste con EF Core. Lo que el alcance significa —«por aquí ya se preguntó»— es correcto y no cambia; lo que cambia es **cuándo** se escribe.

Lo que la especificación principal ya exige, y el código incumple: «El sistema DEBE conservar lo obtenido y reintentar los días que falten en la siguiente ejecución», y «Cuota agotada a mitad del relleno → la siguiente ejecución sigue donde se quedó». Esto no es una funcionalidad nueva sino una corrección; el delta sólo afina los escenarios para que un fallo así no pueda volver a pasar desapercibido.

## Goals / Non-Goals

**Goals:**

- Que el puerto pueda distinguir «no tengo nada» de «no he podido».
- Que el alcance se escriba únicamente cuando el proveedor haya contestado.
- Que el despachador componga esa distinción entre varios proveedores sin perderla.
- Que una pasada deje dicho cuántos activos se quedaron a medias.

**Non-Goals:**

- Guardar el fallo. No se persiste nada nuevo: el reintento nace de **no** haber escrito el alcance. Si el fallo se guardara, habría que decidir cuándo caduca, y eso es un mecanismo entero para un problema que la ausencia ya resuelve.
- Reintentar dentro de la pasada. Lo cubre la especificación y lo justifica el caso real: el 401 vino de pedir demasiado.
- Cambiar la política de reintentos de Polly, que opera un nivel por debajo y ya hace lo suyo dentro de cada petición.

## Decisions

### El puerto devuelve un resultado, no una lista

`GetHistoryAsync` pasa a devolver un tipo que lleva los precios y si el proveedor pudo contestar. Algo como `PriceHistorySlice(IReadOnlyList<DailyPrice> Prices, bool Answered)`, en `Kapea.Application/Abstractions`, junto al resto del puerto.

*Alternativas descartadas:*

- **Lanzar una excepción al fallar.** Es lo que el contrato actual prohíbe a propósito, y con razón: el despachador tendría que capturarla para seguir con el proveedor siguiente, y volveríamos a tener el control de flujo en un `catch`. Además obligaría a cada llamante a saber qué excepciones significan «pasajero».
- **Devolver `null` al fallar.** Distingue los dos casos con menos código, pero pierde los días que sí llegaron antes del corte y no deja sitio para decir *por qué* si algún día hace falta.
- **Un `out` o una propiedad de estado en el proveedor.** Estado compartido entre llamadas; imposible de usar en paralelo y difícil de probar.

Que sea un `record` y no un `bool` suelto deja la puerta abierta a añadir el motivo más adelante sin volver a tocar la firma.

### «Contestado» se compone con Y lógica en el despachador

El despachador devuelve `Answered` verdadero sólo si **ninguno de los proveedores a los que llegó a preguntar** falló. Los días que traiga se conservan siempre.

Esto es deliberadamente conservador: si Yahoo falla y CoinGecko cubre el último año, el rango no se da por contestado aunque haya días nuevos, y la vuelta siguiente volverá a preguntar. Lo contrario —darlo por bueno porque vino algo— es exactamente el error que se está corrigiendo, sólo que un nivel más arriba.

El corte temprano existente se mantiene: si un proveedor completa el rango, a los siguientes no se les pregunta, y no haber preguntado no cuenta como fallo.

### El alcance se escribe sólo si el tramo quedó contestado

En `PriceHistoryUpdater`, `RecordReachAsync` y la actualización de `reached` pasan a estar dentro de la condición. Lo que se descargó se guarda igual, antes y con independencia de esto.

No hace falta anotar un alcance parcial. `Missing()` recalcula los huecos en cada pasada a partir de lo guardado y lo pedido, así que lo que sí llegó ya cuenta por vía del `StoredRange`, y el tramo que falló reaparece solo. Intentar anotar «pedido hasta donde llegó» añadiría una fecha que el proveedor no da y que habría que inventar.

### La cifra de fallos se cuenta y se registra

`PriceHistoryUpdate` gana un contador de activos con algún tramo sin contestar, y la línea de registro final lo dice. Es lo que habría delatado el problema en septiembre: el registro decía «0 activos con relleno pendiente, 0 sin cobertura» mientras se perdía el histórico de USDG-USD.

## Risks / Trade-offs

- **Un proveedor permanentemente roto vuelve a pedirse en cada pasada** → Es el precio correcto de no perder datos, y es acotado: una petición por activo y vuelta, cada seis horas. El contador nuevo lo hace visible en lugar de silencioso, que es justo lo que faltaba. Si algún día molesta, la solución es espaciar los reintentos, no volver a darlos por hechos.
- **El corte del despachador tras un fallo parcial repite peticiones que ya trajeron días** → Sólo hasta que la vuelta buena complete el rango; a partir de ahí `StoredRange` acota el hueco y las peticiones se encogen solas.
- **Cambiar la firma del puerto toca los tres implementadores y sus pruebas dobles** → Es mecánico y el compilador lo señala todo. Se prefiere a una distinción opcional que un implementador nuevo pudiera olvidar: con un tipo de retorno obligatorio, quien escriba el cuarto proveedor tiene que decidir qué devuelve.
- **El 401 seguirá apareciendo** → Este cambio deja de perder datos, no deja de provocar el rechazo. Limitar el ritmo de la descarga masiva queda anotado como mejora aparte, y ahora es más fácil de justificar porque el contador dirá cuántos activos falla cada vuelta.

## Migration Plan

No hay migración de datos: ni campo nuevo ni cambio en `PriceHistoryReach`.

Los huecos ya perdidos —el de USDG-USD, y los que haya— **no se recuperan solos**, porque su alcance ya está escrito y el código nuevo no lo borra. Se recuperan borrando las filas de alcance afectadas, o esperando a que el suelo baje o el activo cambie de identificador de proveedor, que son las dos vías que ya existen para volver a pedir. Merece una comprobación en producción después de desplegar: mirar qué activos tienen alcance por debajo de su primer precio guardado y decidir si se borran.

Sin cambio en la interfaz de usuario, así que la reversión es desplegar la revisión anterior.

## Open Questions

- Si conviene borrar de una vez los alcances ya corruptos en producción o dejar que se arreglen por las vías existentes. No cambia el diseño ni las tareas: es una decisión de operación posterior al despliegue, y depende de cuántos activos resulten estar afectados.
