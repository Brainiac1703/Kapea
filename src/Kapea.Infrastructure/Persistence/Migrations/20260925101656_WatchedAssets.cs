using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kapea.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Los activos que cada usuario vigila, tenga posición en ellos o no.
    /// </summary>
    /// <remarks>
    /// Va por usuario y no como una marca del activo porque el catálogo es global: lo
    /// que uno decida seguir no puede decidirlo por el resto.
    ///
    /// Se da de alta lo que cada uno ha tenido alguna vez. La consulta ya cuenta como
    /// seguido lo que tiene posición, así que esto es para lo vendido por completo:
    /// quien ha tenido algo suele querer saber cómo sigue.
    /// </remarks>
    public partial class WatchedAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WatchedAssets",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchedAssets", x => new { x.UserId, x.AssetId });
                });

            migrationBuilder.Sql("""
                INSERT INTO WatchedAssets (UserId, AssetId, AddedAt)
                SELECT t.UserId, t.AssetId, MIN(t.OccurredAt)
                FROM Transactions AS t
                WHERE t.AssetId IS NOT NULL
                GROUP BY t.UserId, t.AssetId;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WatchedAssets");
        }
    }
}
