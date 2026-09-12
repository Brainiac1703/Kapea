using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kapea.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lo que cuesta operar en cada plataforma, como dato y no como código.
    /// </summary>
    /// <remarks>
    /// El simulador necesita el coste real para decir algo útil: con casi un dos por
    /// ciento por operación completa, hay sistemas inviables por aritmética. Escribirlo
    /// en el programa haría que el día que cambien las tarifas todas las simulaciones
    /// anteriores mintieran sin avisar.
    /// </remarks>
    public partial class PlatformFees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FeeRate",
                table: "Platforms",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FixedFee",
                table: "Platforms",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            // Medidas sobre los movimientos reales del usuario y no copiadas de un
            // folleto: Bit2Me se queda un 0,95 % dentro del precio y Kraken cobró un
            // 1,0006 % en sus dos compras. XTB se queda a cero porque todavía no hay
            // movimientos suyos con los que medirlo, y cero significa que no se sabe.
            migrationBuilder.Sql("UPDATE Platforms SET FeeRate = 0.0095 WHERE Code = 'Bit2Me';");
            migrationBuilder.Sql("UPDATE Platforms SET FeeRate = 0.010006 WHERE Code = 'Kraken';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FeeRate",
                table: "Platforms");

            migrationBuilder.DropColumn(
                name: "FixedFee",
                table: "Platforms");
        }
    }
}
