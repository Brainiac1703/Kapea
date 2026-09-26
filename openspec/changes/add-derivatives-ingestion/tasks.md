## 1. Las liquidaciones, que es lo que corre prisa

- [ ] 1.1 Modelar la serie de derivados en el dominio —contrato, fuente, magnitud, instante, valor y si es observado o estimado— como tipo sin dependencias; verificar con tests de creación válida y de rechazo de un valor sin instante o sin procedencia
- [ ] 1.2 Modelar el alcance de una magnitud —desde cuándo hay dato, con qué resolución y qué huecos tiene— con las operaciones de extenderlo y anotar un hueco; verificar con tests de extensión hacia atrás, de hueco intercalado y de dos huecos contiguos que se funden en uno
- [ ] 1.3 Implementar la persistencia de la serie y del alcance con su migración, indexando por contrato, magnitud e instante; verificar con test de integración que una consulta por rango no recorre la tabla entera
- [ ] 1.4 Implementar la conexión permanente que recibe liquidaciones de Binance y las guarda, con reconexión de espera creciente; verificar con test sobre un servidor de prueba que corta la conexión y comprobar que se reanuda
- [ ] 1.5 Anotar como hueco el intervalo no grabado tras una caída; verificar con test que el alcance refleja el corte y que no se rellena con nada
- [ ] 1.6 Implementar la agregación de liquidaciones sueltas a tramos, conservando el lado y el importe; verificar con tests de un tramo con liquidaciones de ambos lados y de un tramo vacío
- [ ] 1.7 Alojar la conexión en el worker `sync`; verificar manualmente que el proceso levanta la conexión junto a la sincronización y que un fallo de una no tumba la otra

## 2. Qué contrato corresponde a cada activo

- [ ] 2.1 Añadir al activo el contrato perpetuo por fuente con su factor de multiplicación, y su migración; verificar con test que un activo sin contrato se guarda y se lee igual que antes
- [ ] 2.2 Implementar la conversión de magnitudes a la unidad del activo aplicando el factor; verificar con test de un contrato con factor mil y otro con factor uno, comprobando que la misma posición real da la misma cifra
- [ ] 2.3 Implementar el puerto de aplicación que pregunta a una fuente si un activo cotiza como perpetuo y con qué contrato; verificar con test de activo cubierto, no cubierto y fuente caída
- [ ] 2.4 Implementar el descubrimiento del contrato en Binance, incluido el factor de los que cotizan en múltiplos; verificar con tests de un contrato normal y de uno con factor mil
- [ ] 2.5 Hacer que la correspondencia guardada mande sobre cualquier deducción a partir del símbolo; verificar con test que un activo con contrato guardado no vuelve a resolverse por símbolo
- [ ] 2.6 Decir al añadir un activo al seguimiento si cotiza como perpetuo, sin impedir añadirlo si no; verificar con tests de activo con perpetuo y sin él

## 3. Financiación e interés abierto

- [ ] 3.1 Implementar el adaptador de Binance para el tipo de financiación y el interés abierto actuales contra respuestas grabadas; verificar que un contrato desconocido no impide obtener el resto
- [ ] 3.2 Implementar la descarga del histórico desde los ficheros publicados —mensuales para la financiación, diarios para el interés abierto—, con su descompresión y lectura; verificar con tests sobre ficheros de ejemplo inventados con el formato real
- [ ] 3.3 Implementar la degradación ante cuota agotada o error, conservando lo descargado y reanudando donde se quedó; verificar con test que una segunda pasada no vuelve a pedir lo ya obtenido
- [ ] 3.4 Añadir al worker la vuelta que refresca financiación e interés abierto de los activos seguidos con contrato; verificar con test que un activo sin contrato se salta sin error
- [ ] 3.5 Implementar el relleno histórico en segundo plano al asociar un contrato por primera vez; verificar con test que no bloquea la respuesta y que al terminar el alcance empieza en el primer instante descargado

## 4. Que lo guardado sirva para contrastar

- [ ] 4.1 Implementar la consulta del alcance por activo y magnitud; verificar con test que un activo sin dato devuelve alcance vacío en lugar de fallar
- [ ] 4.2 Implementar la agregación de dato antiguo a menor resolución conservando el alcance con su resolución por tramo; verificar con tests de que agregar no altera los valores del tramo reciente y de que el alcance dice la resolución de cada tramo
- [ ] 4.3 Verificar con test de integración que una vuelta completa sobre varios activos deja el alcance coherente con lo guardado

## 5. Comprobación con datos reales

- [ ] 5.1 Asociar el contrato de un activo real seguido, rellenar su histórico y comprobar que el alcance empieza en la fecha esperada y que las cifras coinciden con las que publica la fuente
- [ ] 5.2 Dejar la grabación de liquidaciones corriendo un día y comprobar que el alcance crece sin huecos, o que los huecos que hay corresponden a cortes reales
- [ ] 5.3 Medir cuánto ocupa un activo con histórico completo y dejarlo escrito en el cambio, para decidir con datos cuánto detalle fino se conserva
