using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kapea.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PriceHistoryReach : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PriceHistoryReaches",
                columns: table => new
                {
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestedTo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestedWith = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceHistoryReaches", x => x.AssetId);
                });

            // Lo que ya está descargado se da por pedido: es exactamente el tramo que se
            // pidió la última vez. Sin esto, la primera ejecución volvería a pedir la
            // serie entera de cada activo para descubrir que ya la tiene.
            migrationBuilder.Sql(PriceHistoryReachSeed.Sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PriceHistoryReaches");
        }
    }
}
