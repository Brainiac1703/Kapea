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

## Los ficheros reales no entran en el repositorio

Decidido con el usuario el 26 de septiembre de 2026.

Sus exportaciones llevan datos financieros personales y **no se suben**, ni anonimizadas
ni con los importes reescalados. Se leen en local para contrastar, y nada más.

Los casos de test de extremo a extremo se construyen con **documentos falsos** que
reproduzcan la estructura de cada formato. Lo que hay que probar es que Kapea lee bien
un fichero de XTB o de Bit2Me y calcula lo que debe; eso no necesita que las cifras
sean las suyas.

*Esto contradice la tarea 2.4, escrita antes de tomar la decisión.* Incorporar las
exportaciones reales como casos de test queda descartado; lo que procede es construir
esos casos con datos inventados.

## Lo que sigue pendiente

Queda una sola cosa, y es un dato: **la declaración del ejercicio 2025**. Kapea calcula
−24,55 € de pérdida patrimonial repartida en nueve criptomonedas, y sin lo presentado no
hay contra qué contrastar. Es la comprobación de más valor del change: la única capaz de
destapar que una cifra fiscal está mal.

El PDF que el usuario tenía a mano es el Modelo 100 del ejercicio 2024, presentado en
abril de 2025. Su histórico empieza en mayo de 2025, así que no contiene ninguna de
estas operaciones.
