using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kapea.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Deja en el catálogo el símbolo canónico de los activos que entraron con el nombre
    /// propio de Kraken.
    /// </summary>
    /// <remarks>
    /// Kraken arrastra alias históricos (XXDG por Dogecoin, XXBT por Bitcoin) y los que
    /// se importaron antes de que la traducción los cubriera quedaron catalogados con el
    /// alias. Ningún proveedor de precios conoce ese nombre, así que la posición se
    /// mostraba sin valorar sin que nada fallara, y una importación posterior habría
    /// creado una segunda entrada del mismo activo con su propia cola FIFO.
    ///
    /// Si el activo canónico ya existe se juntan los dos: los movimientos, lotes y
    /// resultados del alias pasan a apuntar al bueno y el alias desaparece.
    /// </remarks>
    public partial class CanonicalKrakenSymbols : Migration
    {
        private static readonly (string Alias, string Canonical)[] Renames =
        [
            ("XXDG", "DOGE"),
            ("XDG", "DOGE"),
            ("XXBT", "BTC"),
            ("XBT", "BTC"),
            ("XETH", "ETH"),
            ("XXRP", "XRP"),
            ("XLTC", "LTC"),
            ("XXLM", "XLM"),
            ("XXMR", "XMR"),
            ("XZEC", "ZEC"),
            ("XETC", "ETC"),
            ("XREP", "REP"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            foreach (var (alias, canonical) in Renames)
            {
                migrationBuilder.Sql($"""
                    DECLARE @alias uniqueidentifier = (SELECT Id FROM Assets WHERE CanonicalSymbol = '{alias}');
                    DECLARE @canonical uniqueidentifier = (SELECT Id FROM Assets WHERE CanonicalSymbol = '{canonical}');

                    IF @alias IS NOT NULL AND @canonical IS NULL
                    BEGIN
                        UPDATE Assets
                        SET CanonicalSymbol = '{canonical}',
                            DisplayName = CASE WHEN DisplayName = '{alias}' THEN '{canonical}' ELSE DisplayName END
                        WHERE Id = @alias;
                    END

                    IF @alias IS NOT NULL AND @canonical IS NOT NULL
                    BEGIN
                        UPDATE Transactions SET AssetId = @canonical WHERE AssetId = @alias;
                        UPDATE Lots SET AssetId = @canonical WHERE AssetId = @alias;
                        UPDATE CapitalIncomes SET AssetId = @canonical WHERE AssetId = @alias;
                        UPDATE RealizedResults SET AssetId = @canonical WHERE AssetId = @alias;
                        UPDATE InternalTransfers SET AssetId = @canonical WHERE AssetId = @alias;
                        DELETE FROM Assets WHERE Id = @alias;
                    END
                    """);
            }
        }

        /// <summary>No se deshace.</summary>
        /// <remarks>
        /// El alias es el nombre equivocado, y volver a ponerlo dejaría otra vez la
        /// posición sin valorar. Los activos que se juntaron ya no se pueden separar.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
