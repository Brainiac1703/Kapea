## 1. El botón

- [x] 1.1 Añadir el texto del botón a `UiStrings.resx` y `UiStrings.en.resx`. Se verifica con `LocalizationConventionTests` en verde.
- [x] 1.2 Añadir en `Kapea.Client/Pages/Watchlist.razor` una barra de herramientas con el botón de actualizar, con el mismo icono y posición que el de la cartera, atado al `ReloadAsync` que ya existe. Se verifica con una prueba de componente que comprueba que el botón se renderiza y que al pulsarlo se vuelve a pedir la lista.
- [x] 1.3 Añadir bUnit a `Kapea.Client.Tests`, que no tenía forma de renderizar una pantalla. Se verifica con la prueba de 1.2 en verde.

## 2. Cierre

- [x] 2.1 Ejecutar la batería completa, también con `LANG=en_US`. Se verifica en verde.
- [x] 2.2 Comprobarlo en el entorno local con los datos reales: el botón actualiza sin recargar la página.
