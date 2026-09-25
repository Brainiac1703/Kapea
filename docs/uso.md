# Guía de uso

Esta guía recorre Kapea en el orden en que se usa: entrar, dar de alta de dónde vienen
tus movimientos, meterlos, revisarlos y, a partir de ahí, leer la cartera, los
resultados fiscales y tus sistemas.

- [Entrar](#entrar)
- [Primeros pasos](#primeros-pasos)
- [Configuración](#configuración)
- [Importar](#importar)
- [Cartera](#cartera)
- [Fiscal](#fiscal)
- [Estrategia](#estrategia)
- [Inicio](#inicio)
- [Cómo calcula Kapea](#cómo-calcula-kapea)
- [Límites conocidos](#límites-conocidos)

## Entrar

Kapea no guarda contraseñas. Se entra con una cuenta que ya tienes; hoy, Google.

La primera vez que entras se crea tu usuario. Todo lo que importes queda a tu nombre y
nadie más lo ve, aunque la instalación la compartan varias personas.

En **Tu cuenta** se ven los proveedores enlazados. Con uno solo, perder esa cuenta de
Google es perder el acceso a tu histórico. Cuando haya un segundo proveedor disponible,
enlázalo desde ahí.

En el entorno local, sin Google configurado, aparece el botón de usuario de
desarrollo. Funciona igual que un acceso real, con su sesión y su cierre.

## Primeros pasos

1. **Crea una cuenta** en *Configuración → Cuentas* por cada sitio donde operas.
2. **Trae los movimientos:**
   - Si la plataforma tiene API, da de alta su credencial en *Configuración →
     Credenciales* y pulsa *Sincronizar ahora*.
   - Si exporta un fichero, súbelo en *Importar → Importar fichero*.
   - Si no hay ni una cosa ni otra, apúntalos en *Cartera → Movimientos → Nuevo
     movimiento*.
3. **Revisa lo pendiente** en *Importar → Por revisar*. Mientras quede algo, las cifras
   de la cartera y los resultados salen incompletas.
4. **Mira la cartera** en *Cartera → Posiciones*.

## Configuración

### Plataformas

Los brókeres y exchanges de los que importas. Vienen tres de serie:

| Plataforma | Cómo llegan los movimientos |
|---|---|
| Kraken | Por su API |
| Bit2Me | Por su API |
| XTB | Por fichero exportado de xStation5 |

Puedes añadir cualquier otra que exporte un fichero, por ejemplo DeGiro o Interactive
Brokers. El código de la plataforma se guarda en cada movimiento importado y no se
puede cambiar después: elige uno corto, sin espacios ni acentos.

Sólo se añaden plataformas de fichero. Una plataforma de API necesita un adaptador que
es código.

Una plataforma con cuentas no se puede retirar hasta borrarlas.

### Cuentas

Una cuenta por cada sitio donde tienes dinero. Si tienes dos cuentas en la misma
plataforma, el alias las distingue. La divisa base es la de la cuenta, normalmente
euros.

Una cuenta con movimientos importados no se puede borrar. Hay que eliminar antes sus
importaciones desde el historial.

### Credenciales

Las claves de API con las que Kapea descarga el histórico de Kraken y Bit2Me.

- **Créalas de sólo lectura** en la plataforma. Kapea comprueba los permisos al darla
  de alta y rechaza cualquier clave que pueda operar o retirar fondos.
- **Se guardan en el servidor** y nunca llegan al navegador. En producción van a Azure
  Key Vault; la base de datos sólo guarda con qué nombre.
- **Rotar** sustituye clave y secreto conservando lo ya importado.
- **Revocar** deja de usarla y borra el secreto. Los movimientos importados se quedan.

La sincronización automática corre cada seis horas. Tras dar de alta una credencial no
hace falta esperar:

- **Sincronizar ahora** pide lo nuevo desde la última sincronización.
- **Releer todo el histórico** vuelve a pedir todo desde el principio. Lo que ya estaba
  se descarta por duplicado, así que es seguro repetirlo.

Una credencial que la plataforma rechaza pasa a *Inválida* y queda fuera de la
sincronización hasta que la rotes.

### Perfiles de importación

Un perfil es la receta con la que se lee un formato de fichero. Vienen tres de serie
para XTB: operaciones de efectivo, posiciones cerradas y posiciones abiertas.

Un perfil dice:

- **Qué cabeceras lo reconocen.** El fichero puede traer más columnas; basta con que
  estén esas.
- **Cómo vienen los datos:** separador, formato de los números (europeo o con punto
  decimal), formatos de fecha que se prueban en orden, zona horaria y divisa si el
  fichero no la trae.
- **Qué es cada fila:** un apunte, una posición abierta (sólo la compra) o una posición
  cerrada (compra y venta).
- **De dónde sale el importe:** de su columna o de multiplicar cantidad por precio, si
  va siempre en positivo y si ya lleva la comisión descontada.
- **Qué columna alimenta cada campo.** Lo que el fichero no traiga se deja en blanco.
- **Qué significa cada concepto del bróker**, una línea por concepto con la forma
  `texto del bróker=Tipo`. Lo que no esté aquí entra sin clasificar y aparece en *Por
  revisar*.
- **Qué conceptos no tienen efecto financiero.** Se cuentan y se descartan.

Los tipos de movimiento son `Buy`, `Sell`, `Deposit`, `Withdrawal`, `Transfer`,
`Dividend`, `Fee`, `Interest`, `Reward` y `Split`.

Corregir un perfil crea una versión nueva y conserva la anterior. Cada movimiento
importado sigue apuntando a la versión con la que se leyó, así que siempre se puede
saber con qué reglas entró. Los perfiles de serie se pueden corregir pero no borrar.

## Importar

### Importar fichero

1. Elige la cuenta de destino y el fichero.
2. Kapea busca el perfil que reconoce las cabeceras y enseña una **vista previa**:
   cuántos registros leyó, cuántos entrarían, cuántos son duplicados, cuántos se
   rechazan y cuántos no tienen efecto financiero.
3. Comprueba las **primeras filas ya interpretadas**. Un mapeo equivocado produce
   cifras plausibles, y es aquí donde se ve que la fecha, el tipo o el importe no son
   los que esperas.
4. **Confirma** o **descarta**. Hasta confirmar no entra nada.

Subir otra vez el mismo fichero, o uno que se solapa con otro ya importado, no duplica
nada: cada movimiento tiene una huella y la segunda vez se marca como duplicado.

#### Cuando ningún perfil reconoce el fichero

Kapea lo dice y ofrece crear el perfil con las columnas del fichero delante.

Si el servicio de modelos está configurado, aparece *Proponer el mapeo*. Antes de
pulsarlo se enseña exactamente qué se envía: las cabeceras y como mucho tres filas. Lo
que vuelve es qué columna es cuál, nunca un importe. Revisa la propuesta antes de
guardar; si Kapea tiene dudas, señala los puntos concretos.

Un libro de Excel con **varias hojas** se importa de una vez: Kapea busca en todas y lee
las que reconoce, cada una con su perfil. La vista previa dice de qué hoja sale cada
fila. Las hojas que ningún perfil reconoce se dejan sin leer y se avisa de cuáles son.

Tampoco importa que el informe traiga sus datos administrativos delante —número de
cuenta, periodo, títulos—: la tabla se busca donde esté. Y la fila de totales con la que
algunos informes se despiden no entra como movimiento, porque no trae ni fecha ni
identificador.

Cuando la misma operación aparece en dos hojas —en una con su cantidad y su precio, en
otra sólo como un importe— se importa una sola vez, por la que más dice. La otra se
cuenta entre los registros sin efecto financiero, junto a los totales.

### Historial

Cada importación con su fecha, su origen, lo que entró, lo duplicado y lo rechazado.
En cada una se ven los registros rechazados con su fila, el motivo y el contenido
original.

**Eliminar una importación** borra también los movimientos que creó. Se rechaza si
alguno forma parte de un traspaso ya confirmado.

### Por revisar

Lo que Kapea no puede decidir por ti. El número junto a *Por revisar* en el menú dice
cuánto queda.

**Traspasos propuestos.** Una salida de una cuenta que coincide con una entrada en
otra, por ejemplo mover bitcoin de Bit2Me a Kraken. Kapea no lo da por hecho:

- *Es un traspaso* mueve los lotes de compra de una cuenta a la otra sin generar
  ganancia ni pérdida. La comisión de red se suma al coste de los lotes movidos.
- *Son independientes* deja una venta y una compra separadas, cada una con su efecto.

*Buscar traspasos* vuelve a buscar coincidencias.

**Posibles duplicados de movimientos apuntados a mano.** Un apunte manual y un importado
con la misma cuenta, tipo, activo, cantidad y día. *Borrar el apunte manual* se queda
con el importado; *Son distintos* deja de señalar esa pareja.

**Movimientos sin clasificar.** El origen trajo un concepto que no encaja en ningún
tipo conocido. Quedan fuera del cálculo hasta resolverlos. La forma habitual es añadir
el concepto al perfil y pulsar *Releer con las reglas actuales*, que vuelve a
interpretarlos sin pedir nada a la plataforma.

## Cartera

### Posiciones

Lo que tienes ahora mismo, agrupado por clase de activo.

| Columna | Qué es |
|---|---|
| Cantidad | Unidades que te quedan |
| Coste medio | Lo que te costó cada unidad que queda, comisiones incluidas |
| Precio | Último precio de mercado, con la hora a la que se tomó |
| Valor | Cantidad por precio |
| Resultado latente | Valor menos coste, con su porcentaje sobre el coste. Es lo que ganarías o perderías si vendieras hoy |
| Resultado realizado | Lo ya ganado o perdido en ventas de ese activo |
| Comisiones | Todas las pagadas en ese activo |
| Peso | Parte de la cartera, con una barra para comparar de un vistazo |

Si un sistema tuyo propuso entrar en un activo, bajo el peso aparecen su objetivo y su
nivel de salida, con un aviso cuando el precio ya los ha tocado.

Arriba se ven los totales. **Patrimonio** es el valor de las posiciones más el efectivo
de todas las cuentas; **Valor total** es sólo el de las posiciones.

**Aportado** es el dinero que has puesto de tu bolsillo: lo ingresado en las plataformas
menos lo retirado de ellas. Debajo, una frase compara lo aportado con lo que tienes hoy,
en euros y en porcentaje. Es la respuesta a «¿gano o pierdo dinero?», y no es lo mismo
que el resultado acumulado, que mide las ganancias y las pérdidas de las operaciones.

Qué cuenta como aportación:

- Sólo el dinero. Una transferencia, una tarjeta o un pago móvil suman; un activo que
  llega de una cartera de fuera, no, porque no se sabe si lo compraste, te lo regalaron
  o te pagaron con él. Cuando existe alguno, Kapea avisa de que lo aportado se queda
  corto y de que la comparación exagera la pérdida.
- Mover dinero entre tus propias cuentas no aporta nada: ese dinero ya estaba dentro.
  Kapea lo sabe cuando ha emparejado las dos patas del traspaso. Si nunca llegó a
  emparejarlas, las contará como dinero nuevo, y conviene resolverlas en *Por revisar*.
- No incluye lo que ocurriera antes de lo que hayas importado. Si tu histórico empieza
  tarde, la cifra empieza ahí.

Los avisos van antes que las cifras:

- **Cifras incompletas** si queda algo por revisar.
- **Faltan precios** si algún activo no tiene cotización. Su valor no se suma, y el
  patrimonio sale corto.
- **Falta efectivo** si hay divisas sin tipo de cambio.
- **Concentración** si tus tres mayores posiciones pasan del 70 % de la cartera.

*Actualizar precios* vuelve a pedirlos. *Recalcular* rehace lotes, posiciones y
resultados desde los movimientos; hace falta rara vez, porque importar ya recalcula.

Pulsar el símbolo de un activo abre su evolución.

### Evolución

Lo que ha valido tu cartera cada día, frente a lo que has ido aportando, en el periodo
que elijas: un mes, tres meses, un año o todo.

Los días en que falta el precio de algún activo se dejan en blanco en lugar de
rellenarse. Rellenarlos dibujaría caídas que no ocurrieron.

**Rendimiento** mide el periodo como se mide un fondo:

- **Rentabilidad de las decisiones** (ponderada por tiempo). Neutraliza cuándo metiste
  el dinero. Sirve para comparar tu gestión con otra.
- **Rentabilidad de tu dinero** (ponderada por capital). Tiene en cuenta cuándo entró
  cada euro. Es lo que te has llevado de verdad.
- **Volatilidad**, anualizada. Cuánto oscila la cartera.
- **Mayor caída** desde un máximo, y si ya se ha recuperado.
- **Referencia:** qué habría rendido poner el mismo dinero, en las mismas fechas, en tu
  mayor posición.

**Reparto por clase** apila el valor de cada clase de activo a lo largo del tiempo.

#### Evolución de un activo

Precio diario con su media móvil simple, su media exponencial y su fuerza relativa
(RSI). La ventana de los indicadores se elige arriba.

### Movimientos

Todo lo que ha entrado, venga de API, de fichero o apuntado a mano. Se filtra por
cuenta, activo, tipo, año, procedencia y estado, y se busca por el contenido original.

La columna **Procedencia** dice de dónde viene cada uno:

| Procedencia | Qué es |
|---|---|
| API | Descargado de la plataforma con su credencial |
| Fichero | Importado de una exportación, con el perfil y la versión con que se leyó |
| A mano | Apuntado por ti, con su nota, cuándo lo apuntaste y cuándo lo editaste |
| Ajuste | La versión corregida de un importado, con el motivo de la corrección |

Pasando el ratón por encima se ve el detalle.

#### Apuntar un movimiento a mano

*Nuevo movimiento* da de alta un movimiento en cualquier cuenta: una compra de un
bróker que no exporta, una operación antigua de la que sólo queda el justificante o
algo que todavía no has importado.

1. Elige cuenta y tipo.
2. Si el tipo mueve un activo (compra, venta, recompensa o traspaso), indica su símbolo,
   su clase, la cantidad y el precio. Si el activo no existe todavía, se da de alta.
3. Indica importe, divisa, comisión y fecha.
4. La nota es opcional.

Pasa por las mismas comprobaciones que un movimiento importado. Si va en otra divisa,
se guarda el tipo de cambio del Banco Central Europeo de su fecha.

Un movimiento apuntado a mano se puede **editar** y **borrar** cuando quieras. Al
editar, si cambias la fecha o la divisa, se vuelve a buscar el tipo de cambio.

#### Corregir o anular un movimiento importado

Un movimiento importado no se edita: detrás está el registro de la plataforma, que es
lo que defiende la cifra. Hay dos formas de arreglarlo:

- **Corregir** abre el formulario con sus datos. Cambias lo que esté mal y escribes el
  motivo. Al guardar, el importado queda anulado con ese motivo y se registra un
  **ajuste** con los datos buenos.
- **Anular** lo deja fuera de la cartera y de los resultados, con un motivo. Sirve para
  un duplicado o algo que no debió entrar.

Un movimiento anulado no se borra: sigue en la lista, tachado, con su motivo y de dónde
vino. Por eso volver a importar el fichero o releer el histórico no lo trae de nuevo.
*Deshacer anulación* lo devuelve al cálculo con los mismos datos.

Un ajuste no se edita: se borra y se corrige de nuevo, para que cada versión de la
cifra tenga su motivo. Borrar un ajuste no deshace la anulación del importado.

No se puede corregir ni anular un movimiento que forma parte de un traspaso ya
confirmado.

#### Aviso de ejercicio anterior

Antes de apuntar, editar, borrar, corregir, anular o deshacer, si la fecha es de un
ejercicio anterior al actual, Kapea lo avisa. Si ya lo declaraste, sus resultados y los
de los ejercicios siguientes pueden cambiar. El aviso no impide nada.

#### Lo que apuntas a mano y después importas

Un movimiento apuntado a mano no tiene huella de la plataforma, así que la
deduplicación no lo reconoce. Kapea busca coincidencias por cuenta, tipo, activo,
cantidad y día:

- **Al importar un fichero**, la vista previa avisa de las filas que coinciden con un
  apunte manual y enseña cuál.
- **Al sincronizar por API**, que no tiene vista previa, la pareja aparece en *Por
  revisar*. Ahí eliges *Borrar el apunte manual* o *Son distintos*.

## Fiscal

Ganancias y pérdidas patrimoniales de un ejercicio, para la declaración. Se ofrecen el
ejercicio actual y los cuatro anteriores, que es el plazo de prescripción.

- **Total del ejercicio:** valor de transmisión, coste de adquisición y resultado.
- **Por activo:** lo mismo, activo a activo.
- **Transmisiones:** cada venta, y dentro de cada una los lotes de compra que consumió,
  con su fecha, su coste y su procedencia. Así se ve qué parte de un resultado se apoya
  en movimientos apuntados a mano.

Si quedan movimientos sin clasificar, lo dice arriba. Una venta sin clasificar no
cuenta, y el resultado del ejercicio saldría mal.

Los rendimientos cobrados, como dividendos o intereses, con su retención, están en
*Posiciones*, al final.

## Seguimiento

Lo que vigilas: lo que tienes en cartera y lo que quieres seguir **antes** de tenerlo.
Tus sistemas se evalúan sobre esta lista, así que es aquí donde decides de qué quieres
recibir señales.

- **Lo que compras entra solo.** No hay que añadir nada: la primera importación de un
  activo lo pone en la lista.
- **Vender del todo no lo saca.** Sigues viendo su precio y sus señales, por si quieres
  volver a entrar.
- **Lo que tienes no se puede dejar de seguir.** Dejaría una posición de tu cartera sin
  precio, sin señales y sin evolución.
- **Al añadir un activo**, Kapea comprueba si algún proveedor da precios y te lo dice. Si
  no los hay se añade igualmente, porque puede cubrirse más adelante, pero mientras tanto
  no tendrá precio ni señales.
- **Anotar una idea** sobre un activo lo pone en seguimiento: sin precios no habría forma
  de comprobar si acertó.

La lista distingue lo que tienes (**En cartera**, con su posición) de lo que sólo vigilas.
Un activo sin precio lo dice, en lugar de aparecer con un cero.

El histórico de precios se descarga para todo lo que sigas, con la profundidad que pidan
tus sistemas: si uno necesita doscientos días de ventana, un activo recién añadido recibe
suficiente para poder evaluarse desde el primer momento.

## Estrategia

Kapea no recomienda comprar ni vender. Aplica siempre igual las reglas que tú escribes
y te enseña qué habría pasado.

### Sistemas

Un sistema es un conjunto de reglas:

- **Entrada:** cuándo comprar.
- **Salida:** cuándo vender.
- **Objetivo** y **nivel de salida**, opcionales, para cerrar la posición al tocarlos.

Cada regla compara dos cosas:

| Qué se compara | |
|---|---|
| Precio | Cierre del día |
| Media móvil simple y exponencial | Con su número de días |
| Fuerza relativa (RSI) | Con su número de días |
| MACD | Línea, señal o distancia entre ambas |
| Bandas de Bollinger | Superior, media o inferior |
| Recorrido diario medio | Cuánto se mueve el activo en un día |
| Número | Un valor fijo |

Las comparaciones son *mayor que*, *menor que*, *cruza al alza* y *cruza a la baja*. Un
cruce exige que ayer estuviera al otro lado; por eso no es lo mismo que *mayor que*.
Las reglas se combinan con *todas* o *cualquiera*, y se pueden anidar.

El objetivo y el nivel de salida se fijan de tres maneras:

- **Porcentaje** sobre el precio de entrada.
- **Múltiplo del recorrido diario**, para que un activo que se mueve mucho tenga
  niveles más holgados.
- **Múltiplo del riesgo**, que pone el objetivo a tantas veces la distancia hasta el
  nivel de salida.

**Describir con palabras.** Si el servicio de modelos está configurado, puedes pegar
la descripción de un método y Kapea propone las reglas. Lo que no sepa traducir te lo
dice en lugar de inventarlo. Revisa siempre la propuesta antes de guardar.

**Corregir** un sistema crea una versión nueva. Las señales ya emitidas siguen diciendo
con qué versión salieron.

Cada sistema indica cuántos días de histórico necesita. Un activo con menos histórico
no da señales con él.

### Simular

Qué habría pasado aplicando un sistema a uno de tus activos durante el último año.

- El capital es lo que se invierte en cada operación.
- La compra o la venta se ejecutan **al día siguiente** de la señal, al cierre. Así no
  se opera con un precio que sólo se conoce cuando el día ha terminado.
- Se descuenta la comisión de la plataforma en cada compra y cada venta.
- Se aplican los tramos del impuesto del ahorro a la ganancia.
- Una posición abierta al final se valora al último cierre, sin cobrar la venta.

El resultado se compara con **comprar y no tocar** el mismo activo. Si las comisiones
se comen el sistema, lo avisa: con muchas operaciones, cada compra y venta se lleva su
parte antes de ganar nada.

### Señales

Lo que dicen tus sistemas sobre tus activos, con la fecha, el precio, la condición que
la disparó, y el objetivo y nivel de salida si el sistema los tiene.

*Calcular señales* evalúa todos tus sistemas con los precios del día. No se calculan
solas: pulsa el botón cuando quieras ver cómo están.

Una señal de entrada no se repite mientras la posición del sistema siga abierta. Si un
mismo día se tocan el objetivo y la salida, gana la salida.

### Ideas

Lo que otros proponen sobre un valor, anotado con su fecha y sus niveles para saber
después qué dio.

1. **Da de alta la fuente**: un analista, un canal o una publicación.
2. **Anota la idea** a mano, o pega el texto de la publicación y pulsa *Sacar ideas*.
   El texto pegado no se guarda: sólo las ideas y el enlace.
3. **Actualizar desenlaces** sigue cada idea con los precios: llegó al objetivo, saltó
   la salida, sigue viva o caducó a los 90 días.

**Balance por fuente** resume aciertos, fallos y rendimiento neto de comisiones de las
ideas ya resueltas. Es la forma de saber si una fuente merece la pena.

### Diario de decisiones

Una anotación sobre un movimiento o una señal: por qué lo hiciste, o por qué no.
Junto a cada anotación Kapea enseña qué pasó: el importe del movimiento, o el objetivo
de la señal. Dentro de seis meses eso vale más que la señal.

## Inicio

El panel resume lo que se consulta a diario:

- **Patrimonio**, con el coste de lo que tienes.
- **Resultado acumulado**, realizado más latente, con su porcentaje sobre el coste.
- **Último día**: cuánto cambió el valor entre los dos últimos días con precio, sin
  contar lo que aportaste ese día. Comprar cien euros no es ganar cien euros.
- **Evolución del último año** frente a lo aportado.
- **Reparto por clase**, con el efectivo aparte.
- **Avisos**: lo pendiente de revisar y las señales de la última semana.
- **Últimos movimientos.**

## Cómo calcula Kapea

**Todo en euros.** Cada operación en otra divisa se convierte con el tipo de cambio
del Banco Central Europeo de su fecha, y ese tipo queda guardado con el movimiento.
Recalcular un ejercicio ya presentado da siempre lo mismo.

**FIFO por activo, no por cuenta.** Cada venta consume primero los lotes más antiguos
de ese activo, aunque se compraran en otra cuenta. Es el criterio de la normativa
española para valores homogéneos.

**Las comisiones forman parte del coste o de la venta.** Una comisión de compra sube el
coste del lote; una de venta rebaja lo obtenido; la de red de un traspaso sube el coste
de los lotes que se mueven.

**Permutas.** Cambiar una criptomoneda por otra es vender una y comprar otra al mismo
tiempo, con su resultado. No mueve efectivo.

**Los días sin precio no se inventan.** Si falta la cotización de un activo, su valor
no se suma y la pantalla lo dice. Nunca se rellena con un cero ni con el último precio
conocido.

**De dónde salen los precios:**

| Fuente | Para qué |
|---|---|
| CoinGecko | Precio actual de criptomonedas e histórico del último año |
| Yahoo Finance | Precio actual de acciones e histórico largo de criptomonedas y acciones |
| Banco Central Europeo | Tipos de cambio diarios |

El histórico de precios lo completa el proceso de sincronización en cada vuelta. La
primera vez tarda, porque descarga desde la fecha de tu primera compra.

## Límites conocidos

- No se puede deshacer un traspaso confirmado, ni corregir o anular sus movimientos.
- No se puede borrar un sistema de especulación.
- Las comisiones por plataforma que usan la simulación y el balance de ideas no tienen
  pantalla. Ver [Configuración](configuracion.md#comisiones-por-plataforma).
- Kapea no lee vídeos ni publicaciones por su cuenta. Las ideas se pegan o se anotan a
  mano.
- Kapea no ejecuta órdenes.
