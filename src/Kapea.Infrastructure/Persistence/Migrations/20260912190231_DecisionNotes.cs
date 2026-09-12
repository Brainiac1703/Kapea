using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kapea.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DecisionNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DecisionNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SignalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    WrittenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DecisionNotes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DecisionNotes_SignalId",
                table: "DecisionNotes",
                column: "SignalId");

            migrationBuilder.CreateIndex(
                name: "IX_DecisionNotes_TransactionId",
                table: "DecisionNotes",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_DecisionNotes_UserId_WrittenAt",
                table: "DecisionNotes",
                columns: new[] { "UserId", "WrittenAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DecisionNotes");
        }
    }
}
