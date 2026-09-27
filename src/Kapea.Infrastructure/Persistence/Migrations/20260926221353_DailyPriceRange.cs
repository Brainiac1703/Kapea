using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kapea.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DailyPriceRange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RangeRequested",
                table: "PriceHistoryReaches",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "HighInEuros",
                table: "DailyPrices",
                type: "decimal(28,12)",
                precision: 28,
                scale: 12,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LowInEuros",
                table: "DailyPrices",
                type: "decimal(28,12)",
                precision: 28,
                scale: 12,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OpenInEuros",
                table: "DailyPrices",
                type: "decimal(28,12)",
                precision: 28,
                scale: 12,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RangeRequested",
                table: "PriceHistoryReaches");

            migrationBuilder.DropColumn(
                name: "HighInEuros",
                table: "DailyPrices");

            migrationBuilder.DropColumn(
                name: "LowInEuros",
                table: "DailyPrices");

            migrationBuilder.DropColumn(
                name: "OpenInEuros",
                table: "DailyPrices");
        }
    }
}
