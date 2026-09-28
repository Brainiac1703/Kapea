## Why

La cartera tiene un botón para volver a pedir los precios. La lista de seguimiento no tiene ninguno: carga al entrar y no vuelve a pedir nada hasta que se recarga la página entera.

Es la misma necesidad en las dos pantallas —saber en qué punto está lo que miras— y `market-prices` ya lo exige, sólo que redactado alrededor de la cartera.

## What Changes

- **La lista de seguimiento gana un botón de actualizar**, con el mismo aspecto y el mismo sitio que el de la cartera.
- **El requisito deja de hablar sólo de la cartera** y pasa a hablar de cualquier pantalla que muestre precios.
- **Sin temporizador.** El requisito prohíbe refrescar por cuenta propia de forma periódica, porque las capas gratuitas tienen límite. Se ha hablado de hacerlo mientras la ventana está abierta y queda anotado como mejora futura: exige cambiar ese requisito con un argumento sobre la cuota, no colarlo aquí.
- **Sin guardar el precio de ahora.** Sigue viviendo un minuto en memoria y desapareciendo.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `market-prices`: la actualización bajo demanda vale para cualquier pantalla que muestre precios, no sólo para la cartera.

## Impact

- **`Kapea.Client/Pages/Watchlist.razor`**: una barra de herramientas con el botón, atado al `ReloadAsync` que ya existe.
- **bUnit entra en `Kapea.Client.Tests`**, que hasta ahora sólo probaba modelos y ayudantes y no tenía con qué renderizar una pantalla. Decidido con el usuario: un botón sin prueba es un botón que se puede borrar sin que nada avise, y a partir de ahora cualquier pantalla se puede probar.
- **`UiStrings.resx` / `UiStrings.en.resx`**: el texto del botón.
- **Nada en el servidor.** El endpoint ya devuelve los precios en cada llamada.
- **Comportamiento conocido que no cambia**: el servidor reutiliza el precio obtenido durante un minuto, así que pulsar dos veces seguidas puede devolver la misma cifra. Es lo que hace hoy la cartera y lo que el requisito pide.
- **Fuera de alcance**: los 401 de Yahoo que dejan sin precio las acciones, y las dos criptomonedas que no resuelven. El botón no inventa precios; esas dos averías van aparte.
