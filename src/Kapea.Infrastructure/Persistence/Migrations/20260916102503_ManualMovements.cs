using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kapea.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ManualMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DistinctFrom",
                table: "Transactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "Transactions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RegisteredAt",
                table: "Transactions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RevisedAt",
                table: "Transactions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "Transactions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VoidedAt",
                table: "Transactions",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Los movimientos apuntados a mano se borran antes: sin estas columnas pierden
            // cuándo se apuntaron, y con el origen «Manual» serían filas que la versión
            // anterior no sabe leer.
            migrationBuilder.Sql("DELETE FROM Transactions WHERE Origin = 'Manual';");

            migrationBuilder.DropColumn(
                name: "DistinctFrom",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "RegisteredAt",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "RevisedAt",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "VoidedAt",
                table: "Transactions");
        }
    }
}
