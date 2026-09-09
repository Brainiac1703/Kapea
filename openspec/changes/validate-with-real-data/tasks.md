## 1. Preparación de los datos

- [ ] 1.1 Reunir las exportaciones reales de xStation5 (operaciones de efectivo, posiciones cerradas y posiciones abiertas) y el histórico de Kraken y Bit2Me; verificar que cada fichero se abre y contiene el periodo esperado
- [ ] 1.2 Decidir con el usuario cómo se incorporan al repositorio —anonimizados, con importes reescalados o fuera del control de versiones— y dejarlo escrito en el propio change antes de añadir ningún fichero
- [ ] 1.3 Identificar el ejercicio ya declarado contra el que contrastar y reunir las cifras presentadas; verificar que se tiene la ganancia patrimonial total y su desglose por activo

## 2. Contraste del adaptador de XTB

- [ ] 2.1 Importar cada exportación real y anotar si el formato se reconoce; verificar que las cabeceras reales coinciden con las declaradas en XtbFormat.Known
- [ ] 2.2 Corregir las entradas de XtbFormat.Known que no coincidan con la exportación real; verificar que la importación completa deja de rechazar el fichero
- [ ] 2.3 Revisar los movimientos que queden sin clasificar y decidir con el usuario el tipo que les corresponde, en particular las líneas de compraventa del informe de efectivo; verificar que el recuento de sin clasificar baja a cero o que lo que queda está justificado
- [ ] 2.4 Incorporar las exportaciones reales como casos de test de extremo a extremo con sus cifras esperadas; verificar que la importación produce exactamente los movimientos esperados

## 3. Contraste con la declaración presentada

- [ ] 3.1 Importar el histórico completo de las tres plataformas y calcular el ejercicio declarado; verificar que la importación termina sin inconsistencias ni movimientos sin clasificar
- [ ] 3.2 Comparar la ganancia patrimonial total y su desglose por activo con lo presentado; documentar cada diferencia y su causa en una nota junto a este change, antes de tocar nada
- [ ] 3.3 Corregir las discrepancias cuya causa sea un error de Kapea; verificar que la cifra pasa a coincidir y que ningún test existente se rompe
- [ ] 3.4 Añadir cada discrepancia corregida como caso de la batería de referencia, con sus cifras calculadas a mano; verificar que la batería completa pasa

## 4. Reproducibilidad sobre datos reales

- [ ] 4.1 Ejecutar un recálculo completo desde cero sobre el histórico real; verificar que lotes, posiciones y resultados son idénticos a los de la primera pasada
- [ ] 4.2 Repetir la importación de todos los ficheros ya importados; verificar que no entra ningún movimiento nuevo y que todo se descarta por duplicado
- [ ] 4.3 Dejar escrito en el change el resultado del contraste: qué cuadró, qué no y qué queda pendiente de confirmar con un asesor
