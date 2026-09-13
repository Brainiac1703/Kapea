using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Strategies;

/// <summary>
/// Una señal ya emitida, guardada.
/// </summary>
/// <remarks>
/// No se recalcula al abrir la pantalla: una señal es un hecho con fecha, y conservarla
/// permite comparar después lo que el sistema dijo con lo que se hizo, que es la mitad
/// del valor del diario de decisiones.
///
/// Lleva la versión con la que salió porque las reglas se corrigen, y una señal antigua
/// tiene que seguir explicándose con las reglas que la produjeron.
/// </remarks>
public sealed class EmittedSignal
{
    private EmittedSignal()
    {
        Reason = null!;
    }

    private EmittedSignal(
        UserId userId,
        Guid strategyId,
        int strategyVersion,
        Guid assetId,
        DateOnly date,
        SignalDirection direction,
        Money priceInEuros,
        string reason)
    {
        UserId = userId;
        StrategyId = strategyId;
        StrategyVersion = strategyVersion;
        AssetId = assetId;
        Date = date;
        Direction = direction;
        PriceInEuros = priceInEuros;
        Reason = reason;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public UserId UserId { get; private set; }

    public Guid StrategyId { get; private set; }

    public int StrategyVersion { get; private set; }

    public Guid AssetId { get; private set; }

    public DateOnly Date { get; private set; }

    public SignalDirection Direction { get; private set; }

    public Money PriceInEuros { get; private set; }

    /// <summary>Qué condición se cumplió y con qué valores.</summary>
    public string Reason { get; private set; }

    public Money? Target { get; private set; }

    public Money? StopLoss { get; private set; }

    public static EmittedSignal From(UserId userId, Guid strategyId, int strategyVersion, Signal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);

        return new EmittedSignal(
            userId,
            strategyId,
            strategyVersion,
            signal.AssetId,
            signal.Date,
            signal.Direction,
            signal.PriceInEuros,
            signal.Reason)
        {
            Target = signal.Target,
            StopLoss = signal.StopLoss,
            Fingerprint = FingerprintOf(
                strategyId, strategyVersion, signal.AssetId, signal.Date, signal.Direction),
        };
    }

    /// <summary>
    /// Huella que identifica la señal para no guardarla dos veces.
    /// </summary>
    /// <remarks>
    /// El motor se ejecuta cada vez que hay precios nuevos y vuelve a producir las
    /// señales antiguas. Sin esta huella, cada vuelta duplicaría el histórico.
    ///
    /// Se guarda en lugar de calcularse al leer, por lo mismo que la de un movimiento
    /// importado: es lo que la base de datos necesita para rechazar el duplicado.
    /// </remarks>
    public string Fingerprint { get; private set; } = string.Empty;

    /// <summary>Cómo se compone la huella. No debe cambiar de forma nunca.</summary>
    public static string FingerprintOf(
        Guid strategyId,
        int strategyVersion,
        Guid assetId,
        DateOnly date,
        SignalDirection direction) =>
        $"{strategyId:N}|{strategyVersion}|{assetId:N}|{date:yyyy-MM-dd}|{direction}";
}
