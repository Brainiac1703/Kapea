using System.Globalization;
using Kapea.Shared.Contracts;

namespace Kapea.Client.Models;

/// <summary>Para qué se abre el formulario de movimiento.</summary>
public enum ManualMovementMode
{
    /// <summary>Apuntar un movimiento nuevo, con nota opcional.</summary>
    New,

    /// <summary>Cambiar un movimiento apuntado a mano.</summary>
    Edit,

    /// <summary>Corregir un importado: se anula y se registra un ajuste, con motivo obligatorio.</summary>
    Correct,
}

/// <summary>El formulario de un movimiento, tal como lo rellena el usuario.</summary>
public sealed class ManualMovementModel
{
    /// <summary>Zona en la que se interpreta la fecha elegida. Es la de la aplicación, que es española.</summary>
    public const string TimeZoneId = "Europe/Madrid";

    /// <summary>Tipos que se pueden apuntar a mano. Sin clasificar no: quien lo apunta sabe qué es.</summary>
    public static readonly string[] Types =
        ["Buy", "Sell", "Deposit", "Withdrawal", "Dividend", "Interest", "Fee", "Reward", "Transfer"];

    /// <summary>Los tipos que mueven un activo y por eso piden activo y cantidad.</summary>
    private static readonly string[] WithAsset = ["Buy", "Sell", "Reward", "Transfer"];

    public ManualMovementMode Mode { get; init; }

    /// <summary>
    /// Cuentas entre las que elegir.
    /// </summary>
    /// <remarks>
    /// Van dentro del modelo porque el diálogo de Fluent UI sólo recibe su Content.
    /// </remarks>
    public IReadOnlyList<AccountResponse> AvailableAccounts { get; init; } = [];

    public Guid AccountId { get; set; }

    public string Type { get; set; } = "Buy";

    public string AssetSymbol { get; set; } = string.Empty;

    public string AssetClass { get; set; } = "Crypto";

    public decimal? Quantity { get; set; }

    public decimal? UnitPrice { get; set; }

    public decimal? GrossAmount { get; set; }

    public string Currency { get; set; } = "EUR";

    public decimal? Fee { get; set; }

    public DateTime? Date { get; set; } = DateTime.Today;

    /// <summary>Nota en un movimiento a mano; motivo en una corrección.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>La fecha con la que se abrió, para avisar también por su ejercicio si se mueve.</summary>
    public DateOnly? OriginalDate { get; init; }

    /// <summary>Por qué se rechazó el último intento, para reabrir el formulario con lo escrito.</summary>
    public string? Error { get; set; }

    public bool NeedsAsset => WithAsset.Contains(Type, StringComparer.Ordinal);

    public bool RequiresReason => Mode == ManualMovementMode.Correct;

    public bool IsComplete => Missing.Count == 0;

    /// <summary>Qué falta, con la clave de la etiqueta de cada campo.</summary>
    public IReadOnlyList<string> Missing
    {
        get
        {
            List<string> missing = [];

            if (AccountId == Guid.Empty)
            {
                missing.Add("Credentials_Account");
            }

            if (NeedsAsset && string.IsNullOrWhiteSpace(AssetSymbol))
            {
                missing.Add("Common_Asset");
            }

            if (NeedsAsset && Quantity is not > 0m)
            {
                missing.Add("Common_Quantity");
            }

            if (GrossAmount is null)
            {
                missing.Add("Common_Amount");
            }

            if (Date is null)
            {
                missing.Add("Common_Date");
            }

            if (RequiresReason && string.IsNullOrWhiteSpace(Text))
            {
                missing.Add("Movement_Reason");
            }

            return missing;
        }
    }

    public ManualMovementRequest ToRequest()
    {
        var day = Date ?? DateTime.Today;

        // Mediodía y no medianoche: un cambio de horario no puede mover el movimiento al
        // día anterior al convertirlo, que cambiaría el ejercicio de una operación del 1
        // de enero.
        var local = new DateTime(day.Year, day.Month, day.Day, 12, 0, 0, DateTimeKind.Unspecified);
        var zone = FindZone();
        var occurredAt = new DateTimeOffset(local, zone.GetUtcOffset(local));

        return new ManualMovementRequest(
            AccountId,
            Type,
            NeedsAsset || !string.IsNullOrWhiteSpace(AssetSymbol) ? AssetSymbol.Trim() : null,
            NeedsAsset ? AssetClass : null,
            NeedsAsset ? Quantity ?? 0m : 0m,
            UnitPrice,
            GrossAmount ?? 0m,
            Currency.Trim().ToUpperInvariant(),
            Fee ?? 0m,
            occurredAt,
            TimeZoneId,
            string.IsNullOrWhiteSpace(Text) ? null : Text.Trim());
    }

    /// <summary>El formulario relleno con los datos de un movimiento, para editarlo o corregirlo.</summary>
    public static ManualMovementModel From(
        TransactionResponse movement,
        ManualMovementMode mode,
        IReadOnlyList<AccountResponse> accounts)
    {
        ArgumentNullException.ThrowIfNull(movement);

        var day = movement.OccurredAt.ToOffset(FindZone().GetUtcOffset(movement.OccurredAt)).DateTime.Date;

        return new ManualMovementModel
        {
            Mode = mode,
            AvailableAccounts = accounts,
            AccountId = movement.AccountId,
            Type = Types.Contains(movement.Type, StringComparer.Ordinal) ? movement.Type : "Buy",
            AssetSymbol = movement.AssetSymbol ?? string.Empty,
            Quantity = movement.Quantity == 0m ? null : movement.Quantity,
            UnitPrice = movement.UnitPrice,
            GrossAmount = movement.GrossAmount,
            Currency = movement.Currency,
            Fee = movement.Fee == 0m ? null : movement.Fee,
            Date = day,
            OriginalDate = DateOnly.FromDateTime(day),
            Text = mode == ManualMovementMode.Edit ? movement.Note ?? string.Empty : string.Empty,
        };
    }

    /// <summary>
    /// La zona de Madrid, o UTC si el navegador no la conoce.
    /// </summary>
    /// <remarks>
    /// En WebAssembly la base de zonas horarias va recortada según la configuración de la
    /// compilación. Caer a UTC deja la fecha correcta al mediodía, que es lo que importa.
    /// </remarks>
    private static TimeZoneInfo FindZone() =>
        TimeZoneInfo.TryFindSystemTimeZoneById(TimeZoneId, out var zone) ? zone : TimeZoneInfo.Utc;
}

/// <summary>El motivo que se pide al anular.</summary>
public sealed class ReasonModel
{
    public string Reason { get; set; } = string.Empty;

    /// <summary>Qué se anula, para que el diálogo lo diga.</summary>
    public string Subject { get; init; } = string.Empty;
}

/// <summary>Cómo se enseña la procedencia de un movimiento.</summary>
public static class MovementOriginLabels
{
    /// <summary>Clave de texto de cada procedencia.</summary>
    public static string LabelKey(string? origin) => origin switch
    {
        MovementOrigins.Api => "Origin_Api",
        MovementOrigins.File => "Origin_File",
        MovementOrigins.Manual => "Origin_Manual",
        MovementOrigins.Adjustment => "Origin_Adjustment",
        _ => "Origin_Unknown",
    };

    public static bool IsImported(string? origin) => origin is MovementOrigins.Api or MovementOrigins.File;

    /// <summary>Un instante en la zona de quien mira, con día y hora.</summary>
    public static string Moment(DateTimeOffset instant) =>
        instant.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
}
