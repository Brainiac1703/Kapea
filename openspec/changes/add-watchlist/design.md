## Context

Ver proposal.md para el porqué. Lo que condiciona el diseño es lo que ya existe:

- El catálogo de activos (`Asset`, con símbolo canónico, clase, nombre, ISIN y la marca de verificado) sólo se llena desde el pipeline de importación, que llama a `AssetCatalog.ResolveAsync`. Hay `Asset.CreateUnverified` y `Verify()`, pensados para un alta que aún no se ha comprobado.
- `StrategyStore.ListSeriesAsync` coge todos los activos del catálogo y se queda con los que tienen serie de precios. `StrategyService.RunAsync` calcula cuánto histórico necesita con `RequiredDays` del sistema más exigente y evalúa cada uno sobre esas series.
- Los precios históricos se descargan por activo, con descarga incremental y proveedores por clase: Yahoo para renta variable, CoinGecko para cripto, con un despachador que elige.
- Las posiciones abiertas se calculan de los lotes, no se guardan como lista.
- Las ideas ya referencian un activo.

## Goals / Non-Goals

**Goals:**

- Que un sistema de entrada sirva para lo que existe: decidir dónde entrar.
- Que lo que está en cartera se vigile sin que el usuario tenga que mantener una lista paralela.
- Que añadir un activo diga en el acto si va a poder seguirse de verdad.
- Que una señal se lea distinto según se tenga el activo o no.

**Non-Goals:**

- Buscar activos por nombre contra un proveedor. De momento se añade por símbolo y clase, que es como ya se identifican.
- Avisar por correo o notificación cuando salte una señal. Las señales se ven donde ya se ven.
- Cambiar cómo se evalúan las reglas de un sistema. Cambia sobre qué activos se evalúan, no el motor.

## Decisions

### El seguimiento va por usuario, y tener posición ya cuenta como seguir

El catálogo de activos es global a propósito: el mismo BTC vale para cualquier usuario, y duplicarlo partiría en dos su cola FIFO. Por eso el seguimiento no puede ser una marca del activo —lo que uno decidiera seguir lo decidiría por todos—, sino una tabla por usuario, con su filtro, como el resto de lo suyo.

Lo que evita la reconciliación no es dónde se guarda, sino cómo se pregunta: **está en seguimiento lo que está en la lista o lo que tiene posición abierta**. Así nadie tiene que acordarse de añadir al comprar, y un activo con posición nunca puede quedarse fuera aunque su fila falte.

Aun así, comprar añade la fila. No por la consulta, que ya lo cubriría, sino para que al vender entero siga en la lista, que es lo que el usuario pidió.

*Consecuencia:* «está en la lista» y «tiene posición» son dos cosas distintas y las dos hacen falta. La primera es un dato guardado; la segunda se calcula de los lotes, como ya se hace.

*Alternativa descartada:* una marca en el activo. Era lo primero que se pensó, y no sobrevive a que el catálogo sea compartido.

### Comprar pone en seguimiento; vender no lo quita

Al resolver un activo durante una importación se añade a la lista del usuario. Es una línea en el sitio por el que ya pasan todos los activos que entran.

Vender no lo desmarca, porque el usuario lo pidió expresamente y porque es la situación en la que más se mira un precio: se acaba de salir y se quiere ver si se acertó.

### Un activo que se tiene no se puede dejar de seguir

Quitar del seguimiento algo con posición abierta dejaría una posición en la cartera sin precio, sin señales y sin evolución. El sistema lo rechaza y lo explica, en lugar de admitirlo y dejar la cartera a medio valorar.

### Al añadir se comprueba la cobertura, pero no se bloquea

Se pregunta al proveedor que corresponde a la clase del activo si tiene precios recientes. Si los tiene, el activo entra verificado y su serie empieza a bajarse; si no, entra igualmente pero advirtiendo.

No se bloquea porque el proveedor puede fallar o tardar en cubrir algo nuevo, y un activo sin precio hoy puede tenerlo mañana. Lo que no puede pasar es que el usuario lo añada y descubra tres días después que nunca iba a funcionar.

*Alternativa descartada:* aceptar sin comprobar. Deja filas muertas en la lista, y el usuario no tiene forma de distinguir «aún no ha descargado» de «esto no existe».

### Cuánto histórico se descarga lo dicen los sistemas

El alcance deja de ser «desde la primera adquisición» y pasa a ser el mayor de dos cosas: desde la primera adquisición, si la hay, y lo que necesite el sistema más exigente para evaluar con su ventana completa. Es el mismo cálculo que `StrategyService` ya hace para pedir series, sacado a donde también lo pueda usar la descarga.

Sin esto, un activo añadido hoy tendría un solo día de serie y ningún sistema podría decir nada de él, que es justo lo contrario de para lo que se añadió.

### La situación del activo viaja con la señal, y no se recalcula en la pantalla

Cuando se consultan las señales, cada una llega diciendo si su activo se tiene o sólo se vigila. La pantalla no consulta la cartera por su cuenta para averiguarlo.

Si lo hiciera, la lista de señales y la cartera podrían contar cosas distintas del mismo activo en el mismo instante, que es el tipo de incoherencia que ya costó una sesión entera con el dinero aportado.

### Anotar una idea pone el activo en seguimiento

Una idea sobre algo que no se vigila no sirve para nada: no hay precio con el que comprobar si acertó, y la capacidad de ideas ya promete seguir su resultado. Registrarla marca el activo, que es la única forma de cumplir esa promesa.

## Risks / Trade-offs

- **La lista crece sola** con cada activo que se compre alguna vez, y nunca se vacía. → Se puede quitar lo que no tenga posición, y la lista distingue lo que se tiene de lo que sólo se vigila, así que crecer no la hace ilegible.
- **Más activos en seguimiento es más histórico que descargar** y más tiempo en cada actualización. → La descarga ya es incremental: cada día pide sólo lo que falta. El coste está en el alta, una vez por activo.
- **Un activo añadido a mano puede no ser el que el usuario cree**: el mismo símbolo puede existir en dos mercados. → Se comprueba la cobertura al añadirlo y se enseña el precio que devuelve el proveedor, que es lo que permite darse cuenta en el acto.
- **Las señales de un activo que no se tiene pueden ser mucho ruido** si se siguen demasiadas cosas. → La lista es del usuario: lo que sobra se quita.

## Migration Plan

La tabla nace vacía y no hace falta rellenarla: lo que tiene posición ya cuenta como seguido por la propia consulta. Aun así, la migración da de alta en la lista los activos que cada usuario ha tenido alguna vez, para que los que vendió entero sigan apareciendo.

Revertir es dejar de mirar la lista; nada se destruye.

## Open Questions

Ninguna.
