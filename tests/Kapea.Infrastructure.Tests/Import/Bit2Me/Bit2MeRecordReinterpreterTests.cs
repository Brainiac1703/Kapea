using Kapea.Domain.Transactions;
using Kapea.Infrastructure.Import.Bit2Me;

namespace Kapea.Infrastructure.Tests.Import.Bit2Me;

/// <summary>
/// Comprueba que un movimiento ya guardado se puede volver a leer con las reglas de hoy.
/// </summary>
/// <remarks>
/// El texto original viaja con cada movimiento, así que reinterpretarlo no exige pedir
/// nada a la plataforma. Es lo que permite aprovechar un adaptador mejorado sobre lo que
/// ya entró sin clasificar.
/// </remarks>
public class Bit2MeRecordReinterpreterTests
{
    [Fact]
    public void A_withdrawal_towards_Earn_is_re_read_as_a_transfer()
    {
        var record = new Bit2MeRecordReinterpreter().Reinterpret("""
            {
              "id": "we-0001",
              "date": "2025-10-17T09:45:13.468Z",
              "completedAt": "2025-10-17T09:45:13.468Z",
              "type": "withdrawal",
              "subtype": "earn",
              "status": "completed",
              "origin": { "currency": "ETH", "amount": "0.50000000" },
              "denomination": { "amount": "1200.00", "currency": "EUR" }
            }
            """);

        Assert.NotNull(record);
        Assert.Equal(TransactionType.Transfer, record!.Type);
        Assert.Equal("ETH", record.AssetSymbol);
    }

    [Fact]
    public void A_plain_deposit_is_re_read_as_a_deposit()
    {
        var record = new Bit2MeRecordReinterpreter().Reinterpret("""
            {
              "id": "we-0004",
              "date": "2026-02-11T00:12:42.888Z",
              "completedAt": "2026-02-11T00:12:42.888Z",
              "type": "deposit",
              "subtype": "funding",
              "status": "completed",
              "destination": { "currency": "EUR", "amount": "500.00" },
              "denomination": { "amount": "500.00", "currency": "EUR" }
            }
            """);

        Assert.Equal(TransactionType.Deposit, record!.Type);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no es json")]
    [InlineData("[1,2,3]")]
    [InlineData("""{"algo":"que no es una transaccion"}""")]
    public void Anything_that_is_not_a_wallet_transaction_is_left_alone(string content)
    {
        // Un texto que no se sabe leer no se fuerza a encajar: quedarse sin clasificar
        // es mejor que inventarle un significado.
        Assert.Null(new Bit2MeRecordReinterpreter().Reinterpret(content));
    }
}
