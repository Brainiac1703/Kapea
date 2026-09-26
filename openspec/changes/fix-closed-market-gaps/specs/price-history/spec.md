## MODIFIED Requirements

### Requirement: Días sin precio

Un día del que ningún proveedor da precio DEBE quedar ausente de la serie, y quien la consuma DEBE poder distinguir esa ausencia de un precio igual a cero. El sistema NO DEBE inventar la cotización de un día en que no se cotizó.

Quien consulte la serie DEBE poder distinguir además **por qué** falta: porque el mercado de ese activo estuvo cerrado ese día, o porque el dato debería existir y no está. No es lo mismo: lo primero es normal y no tiene arreglo posible; lo segundo es una laguna que conviene rellenar.

#### Scenario: Fin de semana en renta variable

- **WHEN** se consulta la serie de una acción en un día que el mercado estuvo cerrado
- **THEN** el sistema indica que no hay precio para ese día, en lugar de devolver cero o el del día anterior

#### Scenario: Distinguir el mercado cerrado del dato ausente

- **WHEN** se consulta un día sin precio de un activo
- **THEN** el sistema dice si ese día su mercado estuvo cerrado o si el dato falta

#### Scenario: Una criptomoneda sin precio un día cualquiera

- **WHEN** falta el precio de una criptomoneda un día en que las demás sí lo tienen
- **THEN** se trata como dato ausente, porque ese mercado no cierra

#### Scenario: Proveedor sin cobertura

- **WHEN** ningún proveedor cubre un activo
- **THEN** su serie queda vacía y el resto de activos se descarga igualmente
