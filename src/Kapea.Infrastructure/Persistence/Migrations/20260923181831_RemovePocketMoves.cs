using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kapea.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Borra los pasos a un producto de rendimiento que se habían importado como
    /// traspasos.
    /// </summary>
    /// <remarks>
    /// Meter un activo en Earn o recuperarlo no es una compra, ni una venta, ni un
    /// traspaso entre cuentas: se mueve entre bolsillos de la misma cuenta y no cambia lo
    /// que se tiene. Importarlo llenaba la lista de apuntes que no significan nada y,
    /// encima, duplicados: Bit2Me expone el mismo paso en el monedero y en los
    /// movimientos de Earn, cada uno con su identificador, así que la deduplicación no
    /// los reconocía como el mismo y aparecían dos movimientos iguales a la misma hora.
    ///
    /// El adaptador ya no los entrega, pero los guardados no se corrigen releyendo el
    /// histórico —se descartan por duplicado—, así que se borran aquí. No hace falta
    /// volver a leer nada: no tienen que regresar en otra forma, simplemente dejan de
    /// existir.
    ///
    /// Se respeta lo que el usuario haya tocado —anulado, corregido o emparejado a mano
    /// con un movimiento suyo— y lo que tenga algo calculado colgando. No debería haber
    /// ninguno así, porque un traspaso sin destino no crea ni consume lotes, pero
    /// comprobarlo cuesta nada.
    /// </remarks>
    public partial class RemovePocketMoves : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM Transactions
                WHERE Type = 'Transfer'
                  AND Origin = 'Imported'
                  AND VoidedAt IS NULL
                  AND DistinctFrom IS NULL
                  AND ISJSON(Source_RawContent) = 1
                  AND (
                        -- Bit2Me, la cara del monedero; Kraken, el paso automático entre
                        -- el activo y su variante en Earn.
                        JSON_VALUE(Source_RawContent, '$.subtype') IN ('earn', 'autoallocation')
                        -- Bit2Me, la cara del producto de rendimiento. Las recompensas
                        -- llevan su propio tipo y no entran aquí.
                        OR (JSON_VALUE(Source_RawContent, '$.movementId') IS NOT NULL
                            AND LOWER(JSON_VALUE(Source_RawContent, '$.type')) IN ('deposit', 'withdrawal'))
                      )
                  AND NOT EXISTS (SELECT 1 FROM RealizedResults AS r WHERE r.DisposalTransactionId = Transactions.Id)
                  AND NOT EXISTS (SELECT 1 FROM CapitalIncomes AS c WHERE c.TransactionId = Transactions.Id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No hay vuelta atrás, y no hace falta: estos apuntes no representan ningún
            // hecho económico. Recuperarlos sería volver a ensuciar la lista.
        }
    }
}
