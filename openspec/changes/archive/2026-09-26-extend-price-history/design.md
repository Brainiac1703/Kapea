## Context

Ver proposal.md para el porqué. Lo que condiciona el diseño es cómo funciona hoy el relleno.

- `PricedAssetRepository.ListAsync` decide desde cuándo hace falta la serie de cada activo: la primera adquisición para lo que se ha tenido, y `StrategyLookback.From(hoy, requiredDays)` para lo que sólo se vigila. `StrategyLookback.Days` da 365 sin sistemas declarados y `requiredDays * 2 + 365` con ellos. Con los dos sistemas actuales salen 765 días, que es exactamente donde empieza la serie guardada.
- `PriceHistoryUpdater.Missing` compara lo pedido con `StoredRange(First, Last)` y devuelve hasta dos tramos: el de delante, cuando el suelo es anterior a `covered.First`, y el de detrás.
- **`StoredRange` sólo sabe qué hay guardado.** No hay memoria de qué se ha pedido.
- `PriceHistoryDispatcher` pregunta a Yahoo primero, que entrega años, y a CoinGecko después para los días que Yahoo no cubra. `CoinGeckoPriceHistoryProvider.FreeHistoryDays` es 365 y su capa gratuita no da más.
- El worker `Kapea.Sync` llama a `UpdateAsync` en cada vuelta, cada pocas horas, después de la sincronización de brókeres.
- Un activo cuenta como «sin cobertura» cuando no se descargó nada y no había nada guardado.

## Goals / Non-Goals

**Goals:**

- Tener de cada activo toda la historia que sus proveedores den.
- Que pedirla una vez no signifique pedirla siempre.
- Que la primera pasada, que es larga, no retrase el precio de hoy.

**Non-Goals:**

- Proveedores nuevos, ni pagar la capa de CoinGecko que da más de un año.
- Cambiar `StrategyLookback`. Sigue siendo la cuenta que garantiza que un sistema se pueda evaluar; deja de ser el tope.

## Decisions

### El suelo es una fecha configurada, no «sin suelo»

Una fecha muy anterior a cualquier cotización que interese —del orden de 2000— en la configuración, con `StrategyLookback` como garantía por debajo.

*Por qué una fecha y no pedir sin límite:* los proveedores necesitan un rango. «Sin suelo» acabaría siendo una fecha igualmente, pero escondida en el adaptador y distinta en cada uno. Y una fecha configurada se puede mover sin tocar código si algún día interesa menos historia.

*Por qué no el primer día de cotización del activo:* habría que preguntarlo, y ninguno de los dos proveedores lo da como tal. Pedir desde antes y quedarse con lo que venga es lo mismo con una petición menos.

### Se recuerda hasta dónde se ha pedido, no sólo qué se ha guardado

Una fecha por activo: la más antigua por la que se ha preguntado. Mientras el suelo no baje de ahí, el tramo de delante se considera resuelto aunque el proveedor no devolviera nada.

Es la pieza que hace posible todo lo demás. Sin ella, un activo que empezó a cotizar en 2021 haría que cada vuelta pidiera 2000-2020 y recibiera una lista vacía, cada pocas horas, para siempre. Y el fallo no se notaría: la serie estaría bien, sólo se gastaría cuota en silencio.

*Dónde vive:* junto al activo y no dentro de la serie de precios. La serie guarda días con precio; esto es un hecho sobre la descarga, no sobre el mercado.

*Alternativa descartada:* deducirlo del rango guardado suponiendo que lo más antiguo guardado es lo más antiguo pedido. Es falso justo en el caso que importa —cuando se pidió más de lo que había— que es el caso permanente.

*Cuándo se invalida:* si el suelo baja por debajo de lo ya pedido, o si el activo pasa a resolverse con otro identificador de proveedor. Lo segundo importa porque `ProviderId` puede llegar después, al seguir el activo desde una búsqueda, y entonces se está preguntando por otra cosa.

### Poner al día primero, rellenar después

Dos fases en la misma vuelta del worker: primero el tramo de detrás de todos los activos, después el de delante. No dos procesos ni dos horarios.

*Por qué:* la primera pasada descarga años de 23 activos. Si se hace activo por activo completo, el vigésimo tercero no tiene precio de hoy hasta que terminan los veintidós anteriores, y si la cuota se agota antes, no lo tiene en absoluto. Ordenándolo así, lo urgente cabe siempre y lo que se queda a medias es lo que puede esperar.

*Reanudación:* el relleno se guarda activo a activo, como ya hace el relleno actual. Una vuelta que no termina deja hecho lo que hizo.

*Alternativa descartada:* un proceso aparte para el relleno. Añadiría un despliegue más para algo que ocurre una vez y luego no vuelve a ocurrir.

### Quedarse sin historia no es quedarse sin cobertura

Hoy «sin cobertura» significa no haber descargado nada y no tener nada. Con el suelo bajado, un activo que empezó a cotizar en 2021 devolverá cero días para el tramo anterior en la primera pasada, y eso no es un problema: es toda la historia que tiene.

Se cuenta como sin cobertura sólo lo que no tiene ni un día en toda la serie, que es lo que ya significaba antes de bajar el suelo.

## Risks / Trade-offs

- **La primera pasada es larga y puede agotar la cuota.** → Poner al día primero y reanudar después. El coste es puntual; a partir de la segunda vuelta no hay relleno.
- **La serie crece mucho.** → De 14.933 filas a centenares de miles en el peor caso. Es un orden de magnitud que SQL Server lleva sin inmutarse, y muy por debajo de lo que plantea la ingesta de derivados.
- **Historia antigua de peor calidad.** → Precios muy antiguos pueden venir sin ajustar por splits o dividendos. La serie ya declara su fuente, y el criterio de splits ya está especificado aparte; esto no lo cambia.
- **Los tokens que sólo cubre CoinGecko seguirán con un año.** → No hay forma gratuita de arreglarlo. Lo que sí cambia es que se pedirá una vez y no en cada vuelta.

## Migration Plan

La memoria de lo pedido nace vacía. Para no volver a pedir lo que ya está, los activos existentes arrancan con el primer día que tengan guardado, que es exactamente lo que se les pidió la última vez.

A partir de ahí, la primera vuelta del worker baja el suelo y rellena. No hay que lanzar nada a mano.

Revertir es subir el suelo. Lo descargado se queda y no estorba.

## Open Questions

Ninguna.
