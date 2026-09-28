## Context

Ver `proposal.md` — Why. Aquí sólo la decisión, porque hay una alternativa razonable que se descartó y conviene que quede escrita.

## Goals / Non-Goals

**Goals:** que un movimiento publicado con retraso entre en la pasada siguiente.

**Non-Goals:** guardar nada nuevo, y tocar los adaptadores.

## Decisions

### El cursor es la fecha del último movimiento

Se decidió con el usuario entre dos formas de cerrarlo:

- **Solapar la ventana**: pedir desde el cursor menos un margen fijo, por ejemplo 48 horas. Barato y acotado, pero deja fuera lo que se publique con más retraso que el margen, y elegir el margen es adivinar.
- **Cursor por fecha de movimiento** (elegida): no pierde nada por retraso, porque el punto de partida no depende del reloj sino de lo que se tiene.

No hace falta columna nueva: la fecha del último movimiento ya está en los propios movimientos, así que la consulta es un `MAX(OccurredAt)` por cuenta.

`CoversUntil` se conserva. Sigue describiendo qué periodo cubrió cada ejecución, que es lo que consultan las pantallas de importación; lo único que cambia es que deja de decidir desde dónde se pide.

## Risks / Trade-offs

- **Una cuenta sin operar durante meses hace crecer la ventana** hasta su último movimiento, y cada pasada relee más → Acotado: Bit2Me descarga el monedero entero en cada pasada de todos modos, y en operaciones de contado sólo amplía el `startTime`. Lo repetido lo absorbe la deduplicación. Si algún día molesta, la salida es poner un suelo por antigüedad, no volver al reloj.
- **Una cuenta sin movimientos pide desde el principio en cada pasada** → Es lo que ya hacía, y es lo correcto: sin nada importado no hay punto de partida que valga.
