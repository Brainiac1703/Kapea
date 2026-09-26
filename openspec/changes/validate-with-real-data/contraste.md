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

## Contraste con la declaración de 2025

Hecho el 26 de septiembre de 2026 contra el informe fiscal de Bit2Me y las dos
declaraciones presentadas en abril de 2026. Los ficheros se leyeron en local y no están
en el repositorio.

Lo declarado son 27,17 € de pérdida patrimonial, que salen sólo de Bit2Me. Kapea
calcula 30,51 € para esos mismos activos.

| Activo | Declarado | Kapea | Diferencia |
|---|---|---|---|
| ATOM | −15,65 | −16,13 | −0,48 |
| POL | −7,85 | −8,33 | −0,48 |
| XDC | −3,67 | −4,16 | −0,49 |
| EURC | 0,00 | −1,89 | −1,89 |
| **Total** | **−27,17** | **−30,51** | **−3,34** |

**El valor de transmisión coincide al céntimo en los cuatro.** Toda la diferencia está en
el valor de adquisición, y tiene una sola causa.

### La causa: la comisión de Bit2Me

En ATOM, POL y XDC el usuario puso 50,00 € de cada uno. El informe declara 49,52 € de
adquisición: 0,48 € menos, que es el 0,96 % que Bit2Me cobra como diferencial. El propio
informe lo dice, y los aparta en una línea llamada «Valores totales de transacción no
deducidos — Fee 37,30».

En EURC pasa lo mismo por partida doble: puso 100 € y el informe declara 99,05 € tanto de
adquisición como de transmisión, así que la operación sale a cero. Kapea toma los 100 €
que salieron de su cuenta contra los 98,11 € que entraron, y le salen 1,89 € de pérdida.

Kapea mete la comisión en el coste; el informe la deja fuera y la reporta aparte. **No es
un error de lectura de ninguno de los dos**: son dos criterios distintos sobre el mismo
dato, y la diferencia total —3,34 €— es exactamente la suma de esas comisiones.

Cuál de los dos corresponde aplicar no se decide aquí. Queda para el asesor.

### Kraken no está en la declaración

El ejercicio 2025 de Kapea sale a −24,55 €, no a −30,51 €, porque incluye Kraken:

| BTC | USDG | PEPE | DOGE | ETH | Suma |
|---|---|---|---|---|---|
| −1,30 | +4,96 | +1,24 | +0,94 | +0,12 | **+5,96** |

Son casi todo recompensas vendidas. No aparecen en la declaración, que sólo recoge lo que
Bit2Me informó. Tampoco aparecen los 9,94 € de rendimientos por staking que el propio
informe de Bit2Me separa como rendimiento de capital mobiliario.

Esto no es un fallo de Kapea —al contrario, es justo lo que sirve para detectarlo—, pero
conviene que lo vea el asesor.

### Las dos declaraciones llevan la cifra entera

Ambas son tributación individual del ejercicio 2025, presentadas el mismo día, y las dos
declaran los mismos 27,17 € con líneas idénticas activo por activo. Si el patrimonio es
de ambos, lo habitual es repartirlo, no declararlo dos veces completo. Es una observación
sobre los documentos, no una conclusión fiscal: hay que confirmarlo con el asesor.

## Lo que sigue pendiente

Nada que dependa de datos. Lo que queda son dos decisiones que no son de Kapea:

1. **Si la comisión de compra va o no en el valor de adquisición.** De la respuesta
   depende si Kapea tiene que cambiar de criterio o si la declaración se quedó corta en
   3,34 €. Hasta saberlo no se toca el cálculo.
2. **Qué hacer con Kraken y con los rendimientos de staking**, que no se declararon.

Y queda construir los casos de test de extremo a extremo con documentos falsos, según lo
decidido más arriba.
