using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kapea.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DailyPrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyPrices",
                columns: table => new
                {
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PriceInEuros = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyPrices", x => new { x.AssetId, x.Date });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyPrices");
        }
    }
}
