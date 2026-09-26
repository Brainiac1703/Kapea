## Context

Ver proposal.md para el porqué. Lo que condiciona el diseño es lo que ya existe y lo que las fuentes permiten.

**En Kapea:**

- `DailyPrices` guarda un cierre diario por activo: hoy son 14.933 filas para 23 activos y dos años. Es el orden de magnitud con el que Kapea está acostumbrada a trabajar.
- `Asset` guarda `ProviderId`, el identificador con que lo conoce su proveedor de precios, y manda sobre cualquier traducción del símbolo. Se añadió en `add-asset-search` precisamente porque adivinarlo desde el símbolo traería el dato de otro activo sin que nada fallara.
- `Kapea.Sync` corre en su propio contenedor con un `PeriodicTimer`, lanza la sincronización de brókeres y completa el histórico de precios en la misma vuelta. **No mantiene ninguna conexión abierta**: todo lo que hace es pedir, guardar y dormir.

**En las fuentes**, comprobado el 26 de septiembre de 2026:

| Magnitud | Cómo se obtiene ahora | Cómo se obtiene el histórico |
|---|---|---|
| Tipo de financiación | API pública | Ficheros mensuales por símbolo en `data.binance.vision`, desde enero de 2020 |
| Interés abierto | API pública | Ficheros diarios en `data.binance.vision`, desde septiembre de 2020. **La API viva `/futures/data/openInterestHist` sólo devuelve 30 días**, así que no sirve para rellenar |
| Liquidaciones | WebSocket `!forceOrder@arr` | No hay. `allForceOrders` fue retirado y el archivo no las publica |

## Goals / Non-Goals

**Goals:**

- Que las liquidaciones empiecen a acumularse cuanto antes, que es lo único irrecuperable.
- Que lo guardado sirva luego para contrastar: sin saber desde cuándo hay dato y con qué resolución, no sirve.
- Que el volumen no crezca sin control ni obligue a cambiar de base de datos.
- Que nada de esto pueda romper lo que ya funciona.

**Non-Goals:**

- Sacarle partido al dato. Reglas, señales, backtest y pantalla son `add-derivatives-signals`, y se deciden cuando haya con qué contrastar.
- Hyperliquid. Aporta contraste —publica posiciones reales en cadena frente a las estimadas de Binance— pero no aporta urgencia. Nada de este diseño lo impide: sería una fuente más.
- Enseñar el dato. Mientras no haya señales, se comprueba consultando la base y los registros.

## Decisions

### El dato de derivados es una capacidad aparte, no una extensión de `price-history`

Serie propia, con su propia clave: contrato, fuente, magnitud e instante. Cuelga del contrato y no del activo, porque un activo puede cotizar en varias fuentes con volúmenes distintos y mezclarlos sería inventar un promedio que no significa nada.

*Alternativa descartada:* añadir columnas a la serie diaria de precios. Obligaría a que todo lo diario tuviera hueco para lo intradía, y a que un activo sin perpetuo arrastrara columnas siempre vacías.

### La correspondencia activo-contrato se guarda, no se deduce

Por cada activo y fuente: el identificador del contrato y el factor de multiplicación. Es el mismo criterio que `ProviderId`, por la misma razón y con el mismo precedente.

El factor no es un detalle: Binance cotiza contratos como `1000PEPEUSDT`, donde cada unidad son mil del activo. Comparar su interés abierto con el de otra fuente sin corregirlo daría tres órdenes de magnitud de diferencia, y esa divergencia parecería una señal.

### Tres ritmos distintos, porque las fuentes no ofrecen lo mismo

1. **Relleno histórico**: descarga de los ficheros publicados, una vez por activo, en segundo plano al asociar un contrato. Es lento y grande, como ya lo es la primera carga del histórico de precios.
2. **Refresco periódico**: financiación e interés abierto actuales, en la vuelta del worker que ya existe. No necesita nada nuevo.
3. **Grabación continua**: liquidaciones por WebSocket. **Es la pieza que Kapea no tiene y la que más puede fallar.**

*Por qué la conexión va en el worker y no en la API:* el worker ya existe por esta razón —ciclo de vida distinto, y que un fallo de lo que depende de terceros no arrastre a la API—. Una conexión permanente dentro de la API la ataría a un proceso que se reinicia con cada despliegue.

### Un hueco se registra, no se disimula

Si la conexión se cae tres días, esas liquidaciones no existen. Un hueco silencioso convierte un backtest futuro en una mentira: la regla no disparó porque no había dato, no porque no se cumpliera la condición.

Por eso el alcance de cada magnitud —desde cuándo hay dato, con qué resolución, con qué huecos— es parte de este cambio y no del siguiente. Recoger sin saber qué se ha recogido no vale para nada.

### La resolución baja con la edad, y el alcance lo dice

El interés abierto en tramos finos para veinte activos durante cinco años son del orden de diez millones de filas por magnitud, frente a las 14.933 que Kapea guarda hoy. Es viable en SQL Server pero no es gratis, y nadie va a mirar el detalle de cinco minutos de hace tres años.

Se conserva el detalle fino durante una ventana reciente y se agrega lo anterior. Lo que impide que esto sea una pérdida encubierta es que el alcance declara la resolución de cada tramo, para que después un backtest pueda negarse a dar por contrastado lo que no lo está.

*Alternativa descartada:* una base de series temporales. Resuelve un problema que Kapea todavía no tiene y añade una pieza más que desplegar. Si el volumen llega a molestar, se decide entonces con datos.

### Sólo los activos seguidos

El dato se recoge de lo que el usuario sigue y que tenga perpetuo. Recoger el mercado entero multiplicaría el volumen por cien para servir a una función —proponer activos nuevos— que pertenece al cambio siguiente y que aún no está decidida.

## Risks / Trade-offs

- **Se recoge un dato cuya utilidad no está demostrada.** → Es el precio de no perder las liquidaciones. Barato: semanas y almacenamiento, no meses.
- **Un WebSocket abierto es frágil.** → Reconexión con espera creciente, y el intervalo perdido anotado. Que se caiga es aceptable; que se caiga en silencio no.
- **El volumen puede desbordar la base.** → Resolución decreciente con la edad y sólo activos seguidos. Se vigila con el alcance, que hay que calcular igualmente.
- **Las fuentes pueden cambiar o cerrar el acceso.** → Detrás de un puerto, como los proveedores de precios. Que caiga deja de dar dato, no rompe nada.
- **Guardar sin explotar puede quedarse en nada.** → Puede. Pero la alternativa es no poder decidirlo nunca con datos propios.

## Migration Plan

Aditivo: nada de lo que hay cambia de forma. Los activos existentes empiezan sin contrato asociado y siguen funcionando igual.

El relleno se lanza activo por activo, así que entra poco a poco en lugar de en una carga única.

Revertir es apagar la ingesta. Nada más depende todavía de este dato, que es una ventaja de hacerlo antes que las señales.

## Open Questions

- **Cuánto tiempo se conserva el detalle fino** antes de agregar. Depende de lo que ocupe de verdad, que se sabrá con los primeros activos rellenados. No cambia las specs ni el reparto de tareas.
