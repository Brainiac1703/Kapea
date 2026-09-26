## Why

La gráfica del patrimonio se corta todos los fines de semana desde que hay acciones en la cartera. Del 28 de junio al 10 de julio de 2026, con los datos reales:

| Día | | Valor | Completo |
|---|---|---|---|
| jueves 2 | | 3.527,27 | sí |
| viernes 3 | fiesta del 4 de julio en EE. UU. | 3.166,94 | **no** |
| sábado 4 | | 3.115,38 | **no** |
| domingo 5 | | 3.122,36 | **no** |
| lunes 6 | | 3.660,96 | sí |

No falta ningún día ni hay ningún valor vacío. Lo que pasa es que **la renta variable desaparece del total**: las cuatro acciones estadounidenses tienen precio 2 de esos 5 días y las dos alemanas 3. El día se marca incompleto, la pantalla no lo dibuja, y por eso se ve el corte. En todo el histórico hay **167 días incompletos**, que son esencialmente todos los fines de semana.

Marcarlos así evita enseñar un desplome de 360 € cada sábado, que sería peor. Pero la respuesta correcta no es ninguna de las dos: **una posición el sábado vale lo que valía el viernes al cierre**. El mercado no cotizó; el valor no desapareció. Es lo que hace cualquier bróker.

Y hay un segundo problema en la misma pantalla: pedir «Todo» devuelve **3.653 días de los que sólo 511 tienen valor**, desde el primer movimiento del 4 de mayo de 2025. Los otros nueve años son ceros que aplastan la línea real contra el borde derecho, y de ahí la impresión de que «sólo hay valores del último año».

## What Changes

- **Una posición en día de mercado cerrado se valora al último cierre**, y la valoración declara que el precio viene arrastrado. Ni hueco, ni desplome, ni fingir que el dato es de ese día.
- **Un día así deja de contar como incompleto.** Incompleto pasa a significar lo que debería haber significado siempre: falta un precio que debería existir.
- **La serie de precios no cambia.** Un día que el mercado estuvo cerrado sigue sin tener cotización, porque no la hubo. Lo que se añade es poder **distinguir** ese caso del de un dato que falta, que es lo que hoy no se puede.
- **«Todo» empieza donde hay datos**, no un número fijo de años antes. En el patrimonio, el primer movimiento.
- **Fuera de alcance, y va en un cambio aparte**: la escala de rangos completa, la agregación semanal y mensual, guardar apertura, máximo y mínimo, las velas, el cursor que lee valores y el bloque de cifras.

## Capabilities

### Modified Capabilities

- `price-history`: un día sin cotización pasa a poder decir si fue porque el mercado estaba cerrado o porque falta el dato.
- `portfolio-history`: una posición se valora al último cierre cuando su mercado estuvo cerrado, eso no hace incompleto el día, y el rango completo empieza en el primer dato.

## Impact

- **Cifras**: el patrimonio de un día de mercado cerrado pasa a incluir la renta variable, así que **cambia hacia arriba** en 167 días de la serie histórica. Es la corrección de un error, no un efecto secundario. Lo que no puede cambiar es nada de lo validado contra los datos reales: coste, resultado realizado, efectivo y los dos ejercicios fiscales, que no dependen de precios de mercado.
- **Cómo se sabe que un mercado estaba cerrado**: sin calendario de festivos por bolsa, que habría que mantener. Es una decisión del diseño.
- **Interfaz**: los días con precio arrastrado tienen que poder distinguirse, porque no son lo mismo que un cierre del día.
