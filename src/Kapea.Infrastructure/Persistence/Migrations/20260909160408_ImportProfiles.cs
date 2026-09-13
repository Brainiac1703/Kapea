using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kapea.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ImportProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceProfileId",
                table: "Transactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceProfileVersion",
                table: "Transactions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ImportProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Platform = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    BuiltIn = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportProfileVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Delimiter = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    DecimalConvention = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FixedCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Columns = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Concepts = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateFormats = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NonFinancialConcepts = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecognizedHeaders = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportProfileVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportProfileVersions_ImportProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "ImportProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportProfiles_Platform",
                table: "ImportProfiles",
                column: "Platform");

            migrationBuilder.CreateIndex(
                name: "IX_ImportProfileVersions_Number",
                table: "ImportProfileVersions",
                column: "Number");

            migrationBuilder.CreateIndex(
                name: "IX_ImportProfileVersions_ProfileId",
                table: "ImportProfileVersions",
                column: "ProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportProfileVersions");

            migrationBuilder.DropTable(
                name: "ImportProfiles");

            migrationBuilder.DropColumn(
                name: "SourceProfileId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "SourceProfileVersion",
                table: "Transactions");
        }
    }
}
