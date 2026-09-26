# Contraste con los datos reales

Estado a 26 de septiembre de 2026. Esta nota recoge lo comprobado y lo que sigue
esperando datos que sólo tiene el usuario.

## Lo que hay importado

| Ejercicio | Movimientos | Plataformas |
|---|---|---|
| 2025 | 1.270 | Kraken, Bit2Me |
| 2026 | 1.094 | Kraken, Bit2Me, XTB |

Sin movimientos sin clasificar y sin incoherencias de cálculo.

## Renta variable

Contrastada al cerrar el panel. Las ocho posiciones de XTB están cerradas y dan
105,17 € de resultado, al céntimo lo que declara el informe del bróker. El detalle
está en la nota de validación de `add-portfolio-dashboard`.

## Reproducibilidad

Un recálculo completo desde cero sobre el histórico real reproduce **exactamente** el
mismo estado: cantidades, coste y resultado realizado de cada posición, el patrimonio
y los dos ejercicios fiscales.

| | Antes | Después |
|---|---|---|
| Patrimonio | 4.148,93 | 4.148,93 |
| Coste | 4.233,48 | 4.233,48 |
| Realizado | 85,26 | 85,26 |
| Latente | −688,32 | −688,32 |
| Fiscal 2025 | −24,55 | −24,55 |
| Fiscal 2026 | 109,81 | 109,81 |

La reimportación sin duplicados está probada de sobra en Kraken y Bit2Me: la
sincronización lleva 347 pasadas contra sus API y ninguna ha introducido un
movimiento nuevo. Con el fichero de XTB no se ha repetido porque ya no está en
disco; la prueba anterior, cuando el usuario reimportó a mano, dio 37 duplicados de
39 leídos.

## Lo que sigue pendiente

Todo lo que queda depende de dos cosas que sólo puede aportar el usuario:

1. **La declaración presentada del ejercicio 2025.** Kapea calcula −24,55 € de pérdida
   patrimonial, repartida en nueve criptomonedas. Sin lo presentado no hay contra qué
   contrastar. Es la comprobación de más valor del change: la única capaz de destapar
   que una cifra fiscal está mal.
2. **Cómo entran los ficheros reales en el repositorio.** Llevan datos financieros
   personales, así que la decisión —anonimizar, reescalar importes o dejarlos fuera del
   control de versiones— se toma antes de añadir ninguno.
