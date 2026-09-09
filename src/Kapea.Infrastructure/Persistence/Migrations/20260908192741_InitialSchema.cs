using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kapea.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Platform = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Alias = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    BaseCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CanonicalSymbol = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Class = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Isin = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CapitalIncomes",
                columns: table => new
                {
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    GrossCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReceivedAtTimeZoneId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    WithholdingAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    WithholdingCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapitalIncomes", x => x.TransactionId);
                });

            migrationBuilder.CreateTable(
                name: "DailyRates",
                columns: table => new
                {
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UnitsPerEuro = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyRates", x => new { x.Currency, x.Date });
                });

            migrationBuilder.CreateTable(
                name: "Lots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalQuantity = table.Column<decimal>(type: "decimal(38,18)", precision: 38, scale: 18, nullable: false),
                    RemainingQuantity = table.Column<decimal>(type: "decimal(38,18)", precision: 38, scale: 18, nullable: false),
                    AcquiredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AcquiredAtTimeZoneId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AcquisitionCostAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    AcquisitionCostCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RealizedResults",
                columns: table => new
                {
                    DisposalTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(38,18)", precision: 38, scale: 18, nullable: false),
                    AcquisitionCostAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    AcquisitionCostCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    DisposedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DisposedAtTimeZoneId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProceedsAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    ProceedsCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RealizedResults", x => x.DisposalTransactionId);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(38,18)", precision: 38, scale: 18, nullable: false),
                    Origin = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    ImportRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceNaturalId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceRowNumber = table.Column<int>(type: "int", nullable: true),
                    Fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AdjustmentReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RateCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    RateUnitsPerEuro = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: true),
                    RateRequestedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RateSource = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    FeeAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    FeeCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    GrossAmountAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    GrossAmountCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OccurredAtTimeZoneId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UnitPriceAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    UnitPriceCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    WithholdingAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    WithholdingCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RealizedResultLots",
                columns: table => new
                {
                    LotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisposalTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcquisitionTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(38,18)", precision: 38, scale: 18, nullable: false),
                    AcquiredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AcquiredAtTimeZoneId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AcquisitionCostAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    AcquisitionCostCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ProceedsAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    ProceedsCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RealizedResultLots", x => new { x.DisposalTransactionId, x.LotId });
                    table.ForeignKey(
                        name: "FK_RealizedResultLots_RealizedResults_DisposalTransactionId",
                        column: x => x.DisposalTransactionId,
                        principalTable: "RealizedResults",
                        principalColumn: "DisposalTransactionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_UserId",
                table: "Accounts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_CanonicalSymbol_Class",
                table: "Assets",
                columns: new[] { "CanonicalSymbol", "Class" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CapitalIncomes_UserId_AssetId",
                table: "CapitalIncomes",
                columns: new[] { "UserId", "AssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_Lots_UserId_AssetId",
                table: "Lots",
                columns: new[] { "UserId", "AssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_RealizedResults_UserId_AssetId",
                table: "RealizedResults",
                columns: new[] { "UserId", "AssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Fingerprint",
                table: "Transactions",
                column: "Fingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_UserId_AccountId",
                table: "Transactions",
                columns: new[] { "UserId", "AccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_UserId_AssetId",
                table: "Transactions",
                columns: new[] { "UserId", "AssetId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropTable(
                name: "Assets");

            migrationBuilder.DropTable(
                name: "CapitalIncomes");

            migrationBuilder.DropTable(
                name: "DailyRates");

            migrationBuilder.DropTable(
                name: "Lots");

            migrationBuilder.DropTable(
                name: "RealizedResultLots");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "RealizedResults");
        }
    }
}
