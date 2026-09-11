using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kapea.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Marca las permutas ya importadas como lo que son: movimientos que no pasaron por
    /// la caja.
    /// </summary>
    /// <remarks>
    /// Se valoran en euros para saber lo que costaron, pero ningún euro entró ni salió.
    /// Contarlas como una venta y una compra dejaba en el saldo la diferencia entre los
    /// dos cambios, que no es dinero de nadie.
    ///
    /// Las patas de una permuta son las únicas que el importador nombra con los sufijos
    /// «:out» e «:in», así que se reconocen por ahí. Releer el histórico no las habría
    /// corregido: ya están guardadas y se descartan por duplicado.
    /// </remarks>
    public partial class SwapSettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SettledInCash",
                table: "Transactions",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql("""
                UPDATE Transactions
                SET SettledInCash = 0
                WHERE SourceNaturalId LIKE '%:out' OR SourceNaturalId LIKE '%:in';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SettledInCash",
                table: "Transactions");
        }
    }
}
