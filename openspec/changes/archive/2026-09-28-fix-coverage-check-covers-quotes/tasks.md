## 1. La comprobación mira las dos cosas

- [x] 1.1 Sustituir el booleano que devuelve `HasPricesAsync` por algo que distinga qué falta —serie, cotización o las dos— sin cambiar todavía quién lo consume. Se verifica compilando.
- [x] 1.2 Consultar también `IMarketPriceProvider` en esa comprobación, con el mismo tratamiento de fallo que ya tiene la del histórico: un proveedor caído cuenta como ausencia y se deja constancia, sin romper el alta. Se verifica con pruebas en `Kapea.Application.Tests` para los cuatro casos: las dos, sólo serie, sólo cotización, ninguna.
- [x] 1.3 Comprobar que las dos rutas de alta —por símbolo y eligiendo de una búsqueda— usan la comprobación nueva. Se verifica con una prueba por ruta.

## 2. El contrato y el aviso dicen cuál falta

- [x] 2.1 Cambiar `WatchAssetResponse.HasPrices` para que transporte qué falta, con su comentario explicando por qué un sí o no engañaba. Se verifica compilando y con las pruebas de la API en verde.
- [x] 2.2 Añadir a `UiStrings.resx` y `UiStrings.en.resx` los textos de los dos avisos nuevos: sin cotización —mencionando que elegirlo de una búsqueda puede resolverlo— y sin serie. Se verifica con `LocalizationConventionTests` en verde.
- [x] 2.3 Enseñar el aviso que corresponda en `Watchlist.razor`, sin `switch` en el marcado porque el escáner de localización falla con eso. Se verifica con una prueba de componente con bUnit por cada aviso.

## 3. Cierre

- [x] 3.1 Ejecutar la batería completa, también con `LANG=en_US`. Se verifica en verde.
- [ ] 3.2 Comprobar en local: añadir por símbolo algo que tenga serie y no cotización debe avisar. Sirve USDG, que hoy está así.
