## 1. El cursor sale de los movimientos

- [x] 1.1 Añadir al puerto `IImportRepository` la consulta del instante del movimiento más reciente de una cuenta, con su porqué documentado. Se verifica compilando.
- [x] 1.2 Implementarla en `ImportRepository` como `MAX(OccurredAt)` filtrando por cuenta. Se verifica con una prueba contra la base de datos real.
- [x] 1.3 Usarla como `from` en `SynchronizationService.ImportAsync`, corrigiendo el comentario que describía el defecto. Se verifica con la batería en verde.

## 2. Pruebas que lo fijan

- [x] 2.1 Reescribir la prueba que afirmaba lo contrario —exigía partir de la hora de la pasada anterior— para que exija partir del último movimiento. Se verifica viendo que falla con el código anterior.
- [x] 2.2 Probar el caso que se perdía: un movimiento cuya fecha es anterior a la ejecución previa entra en la siguiente. Se verifica con esa prueba en verde.
- [x] 2.3 Probar que la relectura completa sigue ignorando el cursor, y que el último movimiento de una cuenta no se cuela en otra. Se verifica con dos pruebas más.

## 3. Cierre

- [x] 3.1 Ejecutar la batería completa. Se verifica en verde.
- [ ] 3.2 Desplegar y comprobar en los registros que la línea «Sincronizando la cuenta … desde X» muestra la fecha del último movimiento y no la de la pasada anterior.
