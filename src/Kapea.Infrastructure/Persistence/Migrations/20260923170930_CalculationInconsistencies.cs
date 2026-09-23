using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kapea.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Guarda con la proyección las incoherencias que detecta el cálculo.
    /// </summary>
    /// <remarks>
    /// El motor ya las producía, pero se perdían al terminar el recálculo y la cartera
    /// se presentaba como completa mientras descartaba una venta entera. Se reemplazan
    /// por activo como el resto de la proyección, y por eso la clave es el movimiento
    /// más la clase de incoherencia: recalcular no puede acumular repetidos.
    /// </remarks>
    public partial class CalculationInconsistencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalculationInconsistencies",
                columns: table => new
                {
                    Kind = table.Column<int>(type: "int", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MissingQuantity = table.Column<decimal>(type: "decimal(38,18)", precision: 38, scale: 18, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OccurredAtTimeZoneId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalculationInconsistencies", x => new { x.TransactionId, x.Kind });
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalculationInconsistencies_UserId_AssetId",
                table: "CalculationInconsistencies",
                columns: new[] { "UserId", "AssetId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalculationInconsistencies");
        }
    }
}
