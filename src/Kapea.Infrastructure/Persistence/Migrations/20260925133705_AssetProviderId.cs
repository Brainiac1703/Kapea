using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kapea.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Guarda con qué identificador conoce cada proveedor a un activo.
    /// </summary>
    /// <remarks>
    /// Hasta ahora se deducía del símbolo con una lista escrita a mano, y lo que no
    /// estaba en ella se quedaba sin precios. Al elegir un activo en una búsqueda, el
    /// identificador viene con lo elegido y deja de haber nada que adivinar.
    ///
    /// Nace vacío: lo que entró importando movimientos se sigue resolviendo por su
    /// símbolo, como hasta ahora.
    /// </remarks>
    public partial class AssetProviderId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProviderId",
                table: "Assets",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProviderId",
                table: "Assets");
        }
    }
}
