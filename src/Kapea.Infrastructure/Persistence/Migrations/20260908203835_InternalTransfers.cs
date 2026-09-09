using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kapea.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InternalTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InternalTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OutgoingTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IncomingTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DestinationAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SentQuantity = table.Column<decimal>(type: "decimal(38,18)", precision: 38, scale: 18, nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "decimal(38,18)", precision: 38, scale: 18, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ProposedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternalTransfers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InternalTransfers_OutgoingTransactionId_IncomingTransactionId",
                table: "InternalTransfers",
                columns: new[] { "OutgoingTransactionId", "IncomingTransactionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InternalTransfers_UserId_Status",
                table: "InternalTransfers",
                columns: new[] { "UserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InternalTransfers");
        }
    }
}
