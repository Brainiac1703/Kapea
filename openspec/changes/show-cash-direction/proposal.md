## Why

La lista de movimientos enseña los importes todos iguales, sin decir en qué dirección va el dinero. Una compra de 547 € y una venta de 569 € se leen igual, y son lo contrario: una saca dinero de la cuenta y la otra lo mete.

Kapea tiene todo lo necesario para decirlo, pero no lo dice en ninguna parte.

Y no vale con mirar los euros: una recompensa cobrada en unidades del propio activo no mueve ni un céntimo y sin embargo suma, porque te la regalan. Lo que ni suma ni resta es el intercambio: una permuta cambia una cosa por otra y un traspaso la lleva de una cuenta propia a otra.

## What Changes

- Cada movimiento dice si suma o resta, con su signo delante y el color detrás.
- La comisión se muestra igual: siempre resta, y se ve.
- Los intercambios se quedan sin signo y sin color, en lugar de fingir una dirección que no tienen.

Las cifras no cambian: el importe sigue siendo el del extracto y la comisión la suya.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `portfolio-domain`: la lista de movimientos debe decir en qué dirección mueve el dinero cada movimiento.

## Impact

- `src/Kapea.Domain/Transactions/MovementDirection.cs`: qué suma y qué resta, decidido en el dominio.
- `src/Kapea.Shared/Contracts/PortfolioContracts.cs`: esa dirección en la respuesta de un movimiento.
- `src/Kapea.Infrastructure/Persistence/Stores/PortfolioQueries.cs`: rellenarla.
- `src/Kapea.Client/Pages/Transactions.razor` y `Home.razor`, con sus estilos.
