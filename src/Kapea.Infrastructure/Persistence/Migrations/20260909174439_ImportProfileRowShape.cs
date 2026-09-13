using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kapea.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ImportProfileRowShape : Migration
    {
        /// <inheritdoc />
        // Los valores por omisión son los del comportamiento anterior y no cadenas
        // vacías: una fila existente con «» no se podría volver a leer, y el fallo
        // aparecería al importar y no al migrar.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AmountIsAlwaysPositive",
                table: "ImportProfileVersions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AmountSource",
                table: "ImportProfileVersions",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "Column");

            migrationBuilder.AddColumn<string>(
                name: "FixedAssetClass",
                table: "ImportProfileVersions",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RowShape",
                table: "ImportProfileVersions",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "SingleMovement");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AmountIsAlwaysPositive",
                table: "ImportProfileVersions");

            migrationBuilder.DropColumn(
                name: "AmountSource",
                table: "ImportProfileVersions");

            migrationBuilder.DropColumn(
                name: "FixedAssetClass",
                table: "ImportProfileVersions");

            migrationBuilder.DropColumn(
                name: "RowShape",
                table: "ImportProfileVersions");
        }
    }
}
