## Why

Kapea guarda 14.933 precios diarios de 23 activos, todos desde el 21 de agosto de 2024. No es una casualidad ni un límite de los proveedores: es exactamente lo que piden los dos sistemas declarados hoy —medias de hasta 200 días, por dos, más un año de recorrido—, y ni un día más.

Eso bastaba mientras el histórico servía sólo para valorar la cartera y evaluar esos dos sistemas. Deja de bastar en cuanto se quiere juzgar un sistema: dos años dan pocas operaciones, y un sistema con seis operaciones en dos años no ha demostrado nada aunque salgan las seis bien. Peor aún, el suelo se mueve: declarar un sistema con una ventana más larga obliga a descargar más historia, y hasta que esa descarga ocurre el sistema no se puede evaluar.

Los proveedores tienen mucho más. Yahoo entrega años para la renta variable y para las criptomonedas grandes. Descargarlo cuesta cuota una vez y no cuesta nada después.

## What Changes

- **El suelo deja de derivarse de lo que los sistemas declarados necesitan.** Ese cálculo pasa a ser lo que garantiza que un sistema se pueda evaluar, no el tope de lo que se descarga. Se baja a todo lo que el proveedor tenga.
- **El sistema recuerda hasta dónde ha pedido**, por activo, no sólo lo que ha guardado. Es lo que hace posible el cambio anterior: sin esa memoria, un activo cuya historia empieza después del suelo haría que cada vuelta del worker volviera a pedir el mismo hueco vacío, cada pocas horas, indefinidamente. A los tokens que Yahoo no cotiza les ocurriría siempre, porque la capa gratuita de CoinGecko nunca da más de 365 días.
- **Un activo sin más historia disponible deja de parecer un problema.** Hoy «sin cobertura» significa no tener ni un día; un activo que simplemente no existe antes de cierta fecha no es eso.
- **El relleno hacia atrás no compite con la puesta al día.** La primera pasada descarga años de 23 activos de golpe, y eso no puede retrasar el precio de hoy ni agotar la cuota que la puesta al día necesita.
- **Fuera de alcance**: añadir proveedores y pagar la capa de CoinGecko que da más de 365 días.

## Capabilities

### Modified Capabilities

- `price-history`: el alcance pasa a ser todo lo que el proveedor tenga, y la descarga incremental deja de repetir lo que ya se pidió aunque no devolviera nada.

## Impact

- **Datos**: la serie crece de 14.933 filas a lo que haya. Para renta variable pueden ser décadas; para las criptomonedas que Yahoo cotiza, desde que existen; para las que sólo cubre CoinGecko, sigue siendo un año. Aun en el peor caso son centenares de miles de filas, un orden de magnitud manejable.
- **Cuota**: la primera pasada es mucho más larga que las actuales. El coste es puntual, pero hay que evitar que se lleve por delante la puesta al día.
- **Migración**: hace falta guardar por activo hasta dónde se ha pedido. Los activos existentes parten de lo que ya tienen guardado, así que no vuelven a pedir lo que ya está.
- **Efecto secundario deseado**: declarar un sistema con una ventana más larga deja de disparar una descarga y una espera. La historia ya estará.
