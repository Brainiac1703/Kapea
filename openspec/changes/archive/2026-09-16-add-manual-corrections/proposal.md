## Why

Hoy un movimiento sólo entra en Kapea importado, por API o por fichero. No se puede apuntar una compra a mano: la de un bróker que no exporta, la de una cuenta cuya exportación aún no has bajado, o una operación antigua de la que sólo queda el justificante.

Tampoco se puede arreglar un importado suelto. Lo único posible es eliminar la importación entera, y en una cuenta con API eso borra meses de histórico que la siguiente sincronización vuelve a traer igual. Antes de meter el histórico real de XTB y contrastarlo con un ejercicio declarado hacen falta las dos cosas.

## What Changes

**Registrar movimientos a mano**

- Desde *Movimientos*, *Nuevo movimiento* da de alta un movimiento en cualquier cuenta con tipo, activo, cantidad, precio, importe, divisa, comisión, fecha y una nota opcional.
- Pasa por las mismas reglas de coherencia que un importado y, en otra divisa, congela el tipo de cambio de su fecha.
- Es un dato tuyo, sin registro de origen: se puede **editar** y **borrar**.
- Cuenta en lotes, posiciones, efectivo, evolución y resultados como cualquier otro.
- Si después importas un movimiento que coincide con uno manual de la misma cuenta, la vista previa lo señala para que decidas.

**Corregir movimientos importados**

- **Anular** un importado con motivo. Sale del cálculo, pero no se borra: conserva su fila de origen y su huella, así que reimportar o releer el histórico no lo resucita.
- **Deshacer** la anulación.
- **Corregir** en un paso: parte de los datos del importado, lo anula con el motivo y registra un ajuste con los datos corregidos y el mismo motivo.
- Un ajuste de corrección no se edita; se borra y se registra otro.

**En los dos casos**

- Recálculo de lotes, posiciones y resultados tras cada cambio.
- Aviso antes de confirmar si la fecha es de un ejercicio anterior al actual, porque sus resultados y los siguientes pueden cambiar.
- En *Movimientos* se distinguen importados, manuales, ajustes y anulados, con su nota o su motivo, y se filtran por origen y estado.

No es un cambio de ruptura: los movimientos existentes siguen igual.

Fuera de alcance: alta masiva a mano, editar los datos de un importado, deshacer un traspaso confirmado y tipos de plataforma nuevos.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `portfolio-domain`: se añaden los movimientos manuales con alta, edición y borrado; la anulación de un importado con su exclusión del cálculo y su deshacer; la corrección en un paso; el borrado de un ajuste; y el aviso de ejercicios afectados. La inmutabilidad del importado no cambia.
- `transaction-import`: la huella de un anulado sigue contando como duplicado, y la vista previa señala coincidencias con movimientos manuales.

## Impact

- **Domain**: origen `Manual` con nota y edición; estado de anulación en `Transaction` sin tocar datos financieros de un importado.
- **Infrastructure**: migración con nota y anulación; los anulados quedan fuera de toda lectura que alimente cifras, salvo la deduplicación y la lista de movimientos; búsqueda de coincidencias con manuales en la vista previa.
- **Application**: servicio que registra, edita, borra, corrige, anula y deshace, y recalcula.
- **Api**: endpoints de movimientos manuales, corrección, anulación, restauración e impacto; filtros nuevos en la búsqueda.
- **Shared**: contratos de movimiento con origen, nota, motivo y anulación; marca de coincidencia en la vista previa.
- **Client**: formulario *Nuevo movimiento*, acciones por origen y estado en *Movimientos*, aviso en la vista previa; textos en español e inglés.
- **Docs**: la guía de uso explica el registro manual y las correcciones, y quita el límite conocido.
