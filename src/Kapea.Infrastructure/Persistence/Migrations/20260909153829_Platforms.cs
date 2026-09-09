using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Kapea.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Platforms : Migration
    {
        /// <inheritdoc />
        // Las cuentas, credenciales e importaciones ya guardaban su plataforma como el
        // mismo texto, así que aquí no se traduce ningún dato: solo aparece el catálogo
        // al que ese texto se refiere. Es lo que mantiene intactas las huellas de
        // deduplicación del histórico ya importado.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Platforms",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ImportKind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    BuiltIn = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Platforms", x => x.Code);
                });

            migrationBuilder.InsertData(
                table: "Platforms",
                columns: new[] { "Code", "BuiltIn", "ImportKind", "Name" },
                values: new object[,]
                {
                    { "Bit2Me", true, "Api", "Bit2Me" },
                    { "Kraken", true, "Api", "Kraken" },
                    { "Xtb", true, "File", "XTB" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Platforms");
        }
    }
}
