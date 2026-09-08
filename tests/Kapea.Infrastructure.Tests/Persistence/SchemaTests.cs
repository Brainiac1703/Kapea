using Kapea.Domain.ValueObjects;
using Microsoft.Data.SqlClient;

namespace Kapea.Infrastructure.Tests.Persistence;

[Collection(SqlServerCollection.Name)]
public class SchemaTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task The_schema_is_created_from_scratch_by_the_migration()
    {
        var tables = await QueryAsync(
            "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'",
            reader => reader.GetString(0));

        Assert.Contains("Assets", tables);
        Assert.Contains("Accounts", tables);
        Assert.Contains("Transactions", tables);
        Assert.Contains("Lots", tables);
        Assert.Contains("RealizedResults", tables);
        Assert.Contains("RealizedResultLots", tables);
        Assert.Contains("CapitalIncomes", tables);
        Assert.Contains("DailyRates", tables);
    }

    [Fact]
    public async Task No_monetary_or_quantity_column_uses_floating_point()
    {
        // Un float aquí no se nota hasta que el total de un ejercicio no cuadra con la
        // suma de sus líneas, y para entonces la declaración ya está presentada.
        var offenders = await QueryAsync(
            """
            SELECT TABLE_NAME + '.' + COLUMN_NAME + ' (' + DATA_TYPE + ')'
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE DATA_TYPE IN ('float', 'real', 'money', 'smallmoney')
            """,
            reader => reader.GetString(0));

        Assert.Empty(offenders);
    }

    [Theory]
    [InlineData("Transactions", "Quantity", 38, 18)]
    [InlineData("Transactions", "GrossAmountAmount", 28, 8)]
    [InlineData("Transactions", "FeeAmount", 28, 8)]
    [InlineData("Lots", "RemainingQuantity", 38, 18)]
    [InlineData("Lots", "AcquisitionCostAmount", 28, 8)]
    [InlineData("DailyRates", "UnitsPerEuro", 28, 12)]
    public async Task Decimal_columns_keep_the_precision_the_domain_needs(
        string table, string column, int precision, int scale)
    {
        var found = await QueryAsync(
            $"""
            SELECT CAST(NUMERIC_PRECISION AS varchar) + ',' + CAST(NUMERIC_SCALE AS varchar)
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = '{table}' AND COLUMN_NAME = '{column}'
            """,
            reader => reader.GetString(0));

        Assert.Equal([$"{precision},{scale}"], found);
    }

    [Fact]
    public async Task Every_portfolio_table_carries_the_owner()
    {
        foreach (var table in new[] { "Accounts", "Transactions", "Lots", "RealizedResults", "CapitalIncomes" })
        {
            var columns = await QueryAsync(
                $"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{table}'",
                reader => reader.GetString(0));

            Assert.Contains("UserId", columns);
        }
    }

    [Fact]
    public async Task The_lookups_the_engine_makes_are_indexed()
    {
        var indexed = await QueryAsync(
            """
            SELECT t.name + ':' + c.name
            FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            JOIN sys.tables t ON t.object_id = i.object_id
            WHERE i.is_primary_key = 0
            """,
            reader => reader.GetString(0));

        Assert.Contains("Transactions:AccountId", indexed);
        Assert.Contains("Transactions:AssetId", indexed);
        Assert.Contains("Transactions:Fingerprint", indexed);
        Assert.Contains("Lots:AssetId", indexed);
    }

    private async Task<List<T>> QueryAsync<T>(string sql, Func<SqlDataReader, T> read)
    {
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var rows = new List<T>();

        while (await reader.ReadAsync())
        {
            rows.Add(read(reader));
        }

        return rows;
    }
}
