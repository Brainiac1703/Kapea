using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kapea.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Marca el perfil de fichero de Bit2Me como lo que es: su importe ya lleva la
    /// comisión descontada.
    /// </summary>
    /// <remarks>
    /// El sembrador solo da de alta los perfiles que faltan, nunca pisa los que ya
    /// están, así que un perfil sembrado antes de existir esta regla no se enteraría.
    /// Se reconoce por el nombre, igual que hace el sembrador.
    /// </remarks>
    public partial class AmountNetOfFee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AmountIsNetOfFee",
                table: "ImportProfileVersions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE v
                SET v.AmountIsNetOfFee = 1
                FROM ImportProfileVersions v
                JOIN ImportProfiles p ON p.Id = v.ProfileId
                WHERE p.Name = 'Bit2Me · Resumen de movimientos';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AmountIsNetOfFee",
                table: "ImportProfileVersions");
        }
    }
}
