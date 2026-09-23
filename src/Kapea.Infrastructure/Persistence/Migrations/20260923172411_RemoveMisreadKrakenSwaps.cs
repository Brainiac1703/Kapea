using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kapea.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Borra las patas de permuta de Kraken que se importaron como traspasos sueltos.
    /// </summary>
    /// <remarks>
    /// Cambiar una cripto por otra deja en el libro dos apuntes con la misma referencia
    /// y ninguna pata en dinero. Hasta ahora el adaptador solo emparejaba el par cuando
    /// una pata era dinero, así que estos entraban como dos traspasos: ni restaban del
    /// activo entregado ni creaban lote del recibido, y una venta posterior se quedaba
    /// sin las unidades recibidas.
    ///
    /// Releerlas no las arregla —se descartan por duplicado—, así que se borran para que
    /// la siguiente lectura del histórico las traiga ya como venta y compra, valoradas
    /// con el precio del día. Borrarlas es seguro porque hoy no participan en nada: no
    /// crean lote, no consumen lote y no mueven la caja.
    ///
    /// Se respeta todo lo que el usuario haya tocado: anulado, corregido o emparejado a
    /// mano con un movimiento suyo. Eso lleva una decisión detrás y no se deshace sin él.
    ///
    /// Tampoco se toca una pata de la que cuelgue algo calculado. No debería haberla
    /// —por eso existe este arreglo—, pero comprobarlo cuesta nada y evita borrar el
    /// movimiento que defiende una cifra de un ejercicio ya presentado.
    /// </remarks>
    public partial class RemoveMisreadKrakenSwaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                WITH Legs AS (
                    SELECT
                        t.Id,
                        JSON_VALUE(t.Source_RawContent, '$.refid') AS ReferenceId
                    FROM Transactions AS t
                    WHERE t.Type = 'Transfer'
                      AND t.Origin = 'Imported'
                      AND t.VoidedAt IS NULL
                      AND t.DistinctFrom IS NULL
                      AND t.AssetId IS NOT NULL
                      AND ISJSON(t.Source_RawContent) = 1
                      AND JSON_VALUE(t.Source_RawContent, '$.type') IN ('spend', 'receive')
                ),
                Pairs AS (
                    SELECT ReferenceId
                    FROM Legs
                    WHERE ReferenceId IS NOT NULL
                    GROUP BY ReferenceId
                    HAVING COUNT(*) = 2
                )
                DELETE FROM Transactions
                WHERE Id IN (
                    SELECT l.Id
                    FROM Legs AS l
                    INNER JOIN Pairs AS p ON p.ReferenceId = l.ReferenceId
                    WHERE NOT EXISTS (SELECT 1 FROM RealizedResults AS r WHERE r.DisposalTransactionId = l.Id)
                      AND NOT EXISTS (SELECT 1 FROM CapitalIncomes AS c WHERE c.TransactionId = l.Id)
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No hay vuelta atrás: lo borrado se recupera releyendo el histórico, que es
            // de donde salió. Reconstruirlo aquí significaría inventar lo que se leyó.
        }
    }
}
