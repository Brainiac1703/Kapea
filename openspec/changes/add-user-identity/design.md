## Context

Ver `proposal.md`. El aislamiento por usuario ya existe y está probado: `UserId` en toda entidad de cartera, filtro global en EF Core, y una prueba que comprueba que una consulta que se olvide de filtrar no devuelve datos ajenos. Lo que no existe es el usuario.

Hoy la API resuelve el usuario del token con Entra External ID, o de un usuario fijo cuando no hay autoridad configurada y el entorno es Development. Fuera de desarrollo se niega a arrancar sin identidad. Ese último guardarraíl se conserva tal cual.

## Goals / Non-Goals

**Goals:**

- Que varias personas puedan usar la misma instalación sin verse entre ellas.
- Que Kapea no custodie ninguna contraseña.
- Que perder el acceso a un proveedor no signifique perder el histórico.
- Que el trabajo de aislamiento ya hecho no haya que rehacerlo.

**Non-Goals:**

- Roles y permisos. Todos los usuarios hacen lo mismo con lo suyo.
- Compartir cartera entre usuarios, o dar acceso de lectura a una gestoría. Interesante a futuro, no ahora.
- Borrado de cuenta con todos sus datos. Hará falta si Kapea llega a ser producto; hoy no.

## Decisions

### El identificador interno es nuestro, no del proveedor

Cada usuario tiene un identificador propio, y las identidades externas son filas aparte que apuntan a él. Los datos cuelgan del interno.

Si los datos colgaran del identificador de Google, enlazar Apple obligaría a reasignar todo el histórico, y dejar de usar Google lo dejaría huérfano. Con un identificador propio, enlazar y desenlazar proveedores no toca ni un movimiento.

**Alternativa descartada** — usar el `sub` del proveedor como `UserId`: ahorra una tabla y crea un acoplamiento que no se puede deshacer.

### La identidad se reconoce por el sujeto, nunca por el correo

Google y Apple emiten un identificador de sujeto estable. El correo no lo es: Apple permite ocultarlo y entregar uno enmascarado, y cualquiera puede cambiar el suyo.

Usar el correo como clave tiene además un fallo peor: dos proveedores distintos pueden entregar el mismo correo, y aceptarlo dejaría entrar a quien controle ese correo en otro proveedor. El correo se guarda para mostrarlo, no para identificar.

### Varias identidades por usuario desde el primer día

Cuesta una tabla ahora y evita un problema sin solución después. Sin identidades múltiples, la única vía de acceso a un histórico fiscal de años sería una cuenta de Google; perderla sería perderlo todo.

Se impide desenlazar la última identidad, por el mismo motivo.

### Google primero; Apple es configuración

Se implementa Google, que es gratuito, y el modelo admite cualquier proveedor. Activar Apple será registrar sus credenciales y probar el flujo, no rehacer el modelo. Apple exige una cuenta de desarrollador de pago y se deja para cuando el usuario decida asumirla.

### La sesión se mantiene con una cookie del servidor

El intercambio con el proveedor ocurre en la API, que es quien puede custodiar el secreto de cliente. El cliente WebAssembly no recibe ningún token del proveedor: recibe una cookie de sesión.

Es la misma razón por la que las credenciales de los brókeres viven en el servidor. Un cliente que corre en el navegador no puede guardar nada que no queramos que se vea, y un token de acceso en el navegador es un token expuesto.

**Alternativa descartada** — que el cliente hable directamente con Google y guarde el token: obliga a custodiar en el navegador algo que da acceso a la cuenta, y complica el cierre de sesión.

### Los datos de desarrollo se reasignan, no se tiran

Los movimientos importados hasta ahora están a nombre del usuario fijo de desarrollo. La migración los reasigna al primer usuario que inicie sesión.

Es una decisión de conveniencia y solo vale porque hoy hay una sola persona. Se documenta como tal en la propia migración, para que nadie la tome por una regla general.

### El acceso de desarrollo sobrevive, con el mismo cerrojo

Poder levantar el entorno con `docker compose up` sin dar de alta credenciales de Google sigue siendo valioso. Se conserva la regla actual: solo en Development, solo si no hay proveedor configurado, y fuera de desarrollo la aplicación se niega a arrancar sin identidad.

## Risks / Trade-offs

- **Perder el acceso al proveedor es perder el acceso a Kapea** → Mitigado con identidades múltiples: enlazar una segunda cuenta da una vía alternativa. No se elimina del todo, y conviene decírselo al usuario en la pantalla de acceso.
- **Un fallo en el aislamiento expondría datos financieros de otra persona** → El filtro global y su prueba ya existen. Este change añade pruebas de extremo a extremo con dos usuarios distintos sobre la misma instalación.
- **Dependencia de un servicio externo para entrar** → Real y aceptada: es el precio de no custodiar contraseñas. Google caído significa no poder entrar, no perder datos.
- **La reasignación de los datos de desarrollo** → Solo es correcta con un único usuario. Escrita como decisión consciente y acotada en la migración.

## Open Questions

- **Cuánto dura la sesión.** No cambia el diseño ni las specs; se fija al implementar con un valor prudente y se ajusta con el uso.
- **Si el primer usuario debe ser el único que pueda registrarse.** Hoy la instalación es personal y cualquiera con la URL podría crear su usuario. Con la aplicación en local no tiene consecuencias; antes de exponerla habrá que decidir si el registro es abierto o por invitación.
