## Why

Kapea sabe lo que vale la cartera, lo que costó y cuánto se ha ganado operando, pero no dice lo único que contesta a «¿gano o pierdo dinero?»: cuánto dinero ha puesto el usuario de su bolsillo y cuánto tiene ahora.

El dato está desde el principio —cada ingreso y cada retirada se importan y ya alimentan el saldo de cada cuenta— pero no se muestra en ninguna pantalla. En el histórico actual son 4.850 € ingresados y 614,96 € retirados: 4.235,04 € puestos frente a 3.473 € de patrimonio. Esa diferencia, −762 €, no aparece por ningún lado.

El resultado acumulado que hoy se ve en Cartera no la sustituye: mide las ganancias y pérdidas de las operaciones más los rendimientos cobrados, y puede alejarse bastante de lo que de verdad ha entrado y salido del bolsillo.

## What Changes

- Aportar pasa a significar lo mismo en toda la aplicación. La serie de evolución contaba como dinero nuevo los traspasos entre cuentas propias y las entradas de activos; ahora usa la misma definición que la cartera, que es una sola y está en un único sitio.
- La cartera pasa a decir cuánto se ha aportado: el dinero ingresado en las plataformas menos el retirado de ellas, con las dos cifras desglosadas.
- Junto al patrimonio aparece la diferencia entre lo que hay y lo que se puso, en euros y en porcentaje.
- Cuando la cifra no puede ser fiel —porque entró un activo desde fuera, que no es una aportación en dinero— el sistema lo dice en lugar de enseñar un porcentaje que engaña.
- Se muestra en Cartera y en Inicio, junto a las cifras que ya están ahí.

No cambia nada de cómo se importan ni se guardan los ingresos y las retiradas: siguen siendo movimientos como hasta ahora, que es lo que permite auditarlos y corregirlos.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `portfolio-performance`: el sistema debe decir cuánto dinero se ha aportado y cuánto se ha retirado, y comparar lo aportado neto con el patrimonio actual, advirtiendo cuando la comparación no puede ser fiel.
- `portfolio-history`: la serie debe contar como aportación lo mismo que el resumen de la cartera, dejando fuera el dinero movido entre cuentas propias y las entradas de activos.

## Impact

- `src/Kapea.Domain/Calculation/`: cálculo del capital aportado y su hueco en `PortfolioSummary`.
- `src/Kapea.Shared/Contracts/PortfolioContracts.cs`: las cifras nuevas en `PortfolioResponse`.
- `src/Kapea.Infrastructure/Persistence/Stores/PortfolioQueries.cs`: pasar los movimientos al cálculo y devolver el resultado.
- `src/Kapea.Client/Pages/Portfolio.razor` y `src/Kapea.Client/Pages/Home.razor`, con sus recursos de localización.
- Ninguna migración: no se guarda nada nuevo, se compone de lo que ya hay.
