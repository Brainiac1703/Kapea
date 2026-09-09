using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kapea.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ImportRunProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProfileId",
                table: "ImportRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProfileVersion",
                table: "ImportRuns",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProfileId",
                table: "ImportRuns");

            migrationBuilder.DropColumn(
                name: "ProfileVersion",
                table: "ImportRuns");
        }
    }
}
