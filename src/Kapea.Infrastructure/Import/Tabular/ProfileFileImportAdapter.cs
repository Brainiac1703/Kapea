using Kapea.Application.Import;
using Kapea.Domain.Accounts;
using Kapea.Domain.Assets;
using Kapea.Domain.ImportProfiles;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Infrastructure.Import.Tabular;

/// <summary>
/// Lee un fichero aplicando un perfil.
/// </summary>
/// <remarks>
/// Es el sustituto de escribir un adaptador por plataforma. Lo que antes era código
/// —qué columna es la fecha, cómo vienen los números, qué significa «Stocks/ETF sale»—
/// aquí son datos que llegan en la versión del perfil.
///
/// No adivina nada. Una columna que el perfil no mapea no se usa, y un concepto sin
/// traducción entra como desconocido en lugar de suponerle un tipo: un movimiento mal
/// clasificado es una cifra mal calculada, y esto acaba en una declaración.
/// </remarks>
public sealed class ProfileFileImportAdapter
{
    public ImportReadResult Read(
        ImportProfile profile,
        ImportProfileVersion version,
        TabularContent content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(content);

        var columns = ResolveColumns(version, content.Headers);
        var values = new ProfileValueReader(version);
        var records = new List<ImportRecord>();
        var rejected = new List<RejectedRecord>();
        var warnings = new List<string>();
        var nonFinancial = 0;
        var unknownConcepts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (field, header) in version.Columns)
        {
            if (!columns.ContainsKey(field))
            {
                warnings.Add($"El fichero no trae la columna '{header}': los movimientos irán sin ese dato.");
            }
        }

        foreach (var row in content.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var concept = Cell(row, columns, ImportField.Concept);

            // Los apuntes sin efecto financiero se cuentan y se descartan. Rechazarlos
            // los mostraría como un problema a resolver cuando no hay nada que resolver.
            if (concept is { Length: > 0 } text
                && version.NonFinancialConcepts.Any(ignored =>
                    string.Equals(ignored, text, StringComparison.OrdinalIgnoreCase)))
            {
                nonFinancial++;
                continue;
            }

            try
            {
                records.AddRange(version.RowShape switch
                {
                    RowShape.SingleMovement => [Map(row, columns, version, values, unknownConcepts)],
                    RowShape.ExchangePair => MapExchange(row, columns, version, values, unknownConcepts),
                    _ => MapPosition(row, columns, version, values),
                });
            }
            catch (ProfileValueException exception)
            {
                rejected.Add(new RejectedRecord(
                    Cell(row, columns, ImportField.NaturalId), row.Number, row.Raw, exception.Message));
            }
        }

        if (unknownConcepts.Count > 0)
        {
            // Se avisa una vez por concepto y no por fila: cien filas del mismo concepto
            // sin traducir son un solo hueco del perfil, no cien problemas.
            warnings.Add(
                $"El perfil no traduce estos conceptos, y sus movimientos entran sin clasificar: {string.Join(", ", unknownConcepts.Order(StringComparer.OrdinalIgnoreCase))}.");
        }

        return new ImportReadResult(records, rejected, nonFinancial, warnings);
    }

    /// <summary>
    /// Empareja los campos del perfil con las posiciones reales del fichero.
    /// </summary>
    /// <remarks>
    /// Por nombre de cabecera y no por posición: un bróker que inserta una columna en
    /// medio movería todas las demás, y por posición se importaría todo desplazado sin
    /// que nada fallara.
    /// </remarks>
    private static Dictionary<ImportField, int> ResolveColumns(
        ImportProfileVersion version,
        IReadOnlyList<string> headers)
    {
        var positions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < headers.Count; index++)
        {
            var header = headers[index]?.Trim();

            if (!string.IsNullOrEmpty(header) && !positions.ContainsKey(header))
            {
                positions[header] = index;
            }
        }

        var resolved = new Dictionary<ImportField, int>();

        foreach (var (field, header) in version.Columns)
        {
            if (positions.TryGetValue(header, out var index))
            {
                resolved[field] = index;
            }
        }

        return resolved;
    }

    /// <summary>
    /// Convierte una fila que es un intercambio en sus movimientos.
    /// </summary>
    /// <remarks>
    /// Lo que significa la fila no lo dice un texto sino las dos monedas: pagar con
    /// euros es comprar, cobrar euros es vender, y cambiar una cripto por otra son las
    /// dos cosas a la vez. El concepto solo manda cuando dice algo que el par no puede
    /// decir, como que esto fue una recompensa y no una compra.
    ///
    /// Un intercambio entre dos activos no tiene importe en euros que leer, así que
    /// entra sin valorar. Inventarlo con el cambio del día siguiente sería una cifra
    /// fiscal fabricada.
    /// </remarks>
    private static IEnumerable<ImportRecord> MapExchange(
        TabularRow row,
        IReadOnlyDictionary<ImportField, int> columns,
        ImportProfileVersion version,
        ProfileValueReader values,
        HashSet<string> unknownConcepts)
    {
        var concept = Cell(row, columns, ImportField.Concept);
        var declared = ResolveType(concept, version, unknownConcepts, silent: true);

        var date = values.Date(Cell(row, columns, ImportField.Date));

        var inCurrency = Cell(row, columns, ImportField.DestinationCurrency);
        var outCurrency = Cell(row, columns, ImportField.OriginCurrency);
        var inAmount = values.OptionalDecimal(Cell(row, columns, ImportField.DestinationAmount)) ?? 0m;
        var outAmount = values.OptionalDecimal(Cell(row, columns, ImportField.OriginAmount)) ?? 0m;
        var fee = values.OptionalDecimal(Cell(row, columns, ImportField.Fee)) ?? 0m;
        var reference = Cell(row, columns, ImportField.NaturalId);

        // Un concepto que no es compraventa manda sobre el par: una recompensa entrega
        // unidades igual que una compra, y confundirlas inventaría un coste que nadie pagó.
        if (declared is not (TransactionType.Unknown or TransactionType.Buy or TransactionType.Sell))
        {
            var moved = inAmount != 0m ? inAmount : outAmount;

            // Si lo que se mueve es dinero, esa cantidad es el importe. Si es un activo,
            // la fila no dice cuánto valía y entra sin valorar.
            yield return Exchange(
                declared,
                inCurrency ?? outCurrency,
                moved,
                version.IsFiat(inCurrency ?? outCurrency) ? moved : 0m,
                reference);

            yield break;
        }

        var paying = version.IsFiat(outCurrency);
        var charging = version.IsFiat(inCurrency);

        if (paying && !charging)
        {
            yield return Exchange(TransactionType.Buy, inCurrency, inAmount, outAmount, reference);

            yield break;
        }

        if (charging && !paying)
        {
            yield return Exchange(TransactionType.Sell, outCurrency, outAmount, inAmount, reference);

            yield break;
        }

        if (!paying && !charging && outCurrency is { Length: > 0 } && inCurrency is { Length: > 0 })
        {
            // Cada pata necesita su propio identificador: con el mismo, la segunda se
            // descartaría como duplicado de la primera.
            yield return Exchange(TransactionType.Sell, outCurrency, outAmount, euros: 0m, Suffix(reference, "out"));
            yield return Exchange(TransactionType.Buy, inCurrency, inAmount, euros: 0m, Suffix(reference, "in"));

            yield break;
        }

        // Dinero contra dinero, o una sola pata: no hay activo que mover.
        yield return Exchange(
            declared == TransactionType.Unknown ? TransactionType.Deposit : declared,
            inCurrency ?? outCurrency,
            0m,
            inAmount != 0m ? inAmount : outAmount,
            reference);

        ImportRecord Exchange(TransactionType type, string? asset, decimal quantity, decimal euros, string? naturalId)
        {
            var isMoney = version.IsFiat(asset);

            return new ImportRecord(
                NaturalId: naturalId,
                RowNumber: row.Number,
                Type: type,
                AssetSymbol: isMoney ? null : asset?.Trim().ToUpperInvariant(),
                AssetClass: isMoney ? null : AssetClassOf(version),
                Quantity: isMoney ? 0m : Math.Abs(quantity),
                UnitPrice: null,
                GrossAmount: Gross(version, type, Math.Abs(euros), fee),
                Currency: Currency.FromCode(
                    isMoney && asset is { Length: > 0 } ? asset.Trim() : version.FixedCurrency ?? "EUR"),
                Fee: Math.Abs(fee),
                Withholding: null,
                OccurredAt: null,
                NaiveOccurredAt: date,
                SourceTimeZoneId: version.TimeZoneId,
                SplitRatio: null,
                RawContent: row.Raw);
        }
    }


    /// <summary>
    /// Reconstruye el importe bruto cuando el extracto da el dinero que se movió.
    /// </summary>
    /// <remarks>
    /// El cálculo resta la comisión a lo que se ingresa y se la suma a lo que se paga,
    /// así que un importe que ya la lleva descontada la contaría dos veces: la venta
    /// rendiría menos de lo que rindió y el saldo quedaría corto.
    /// </remarks>
    private static decimal Gross(ImportProfileVersion version, TransactionType type, decimal amount, decimal fee)
    {
        if (!version.AmountIsNetOfFee || fee == 0m || amount == 0m)
        {
            return amount;
        }

        return type switch
        {
            TransactionType.Sell or TransactionType.Withdrawal => amount + Math.Abs(fee),
            TransactionType.Buy or TransactionType.Deposit => amount - Math.Abs(fee),
            _ => amount,
        };
    }

    private static string? Suffix(string? reference, string leg) =>
        reference is { Length: > 0 } ? $"{reference}:{leg}" : null;

    /// <summary>
    /// Convierte una fila que es una posición entera en sus movimientos.
    /// </summary>
    /// <remarks>
    /// Una posición cerrada son dos movimientos, no uno: la adquisición y la
    /// transmisión. Guardarla como uno solo perdería la fecha de compra, que es lo que
    /// decide de qué ejercicio es el resultado y qué lote consume el cálculo FIFO.
    ///
    /// La comisión va entera a la adquisición. Repartirla entre las dos patas cambiaría
    /// el coste de adquisición, y con él el resultado que se declara.
    /// </remarks>
    private static IEnumerable<ImportRecord> MapPosition(
        TabularRow row,
        IReadOnlyDictionary<ImportField, int> columns,
        ImportProfileVersion version,
        ProfileValueReader values)
    {
        var symbol = Cell(row, columns, ImportField.AssetSymbol);

        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ProfileValueException("La posición no indica ningún activo.");
        }

        var quantity = values.Decimal(Cell(row, columns, ImportField.Quantity), "cantidad");

        if (quantity <= 0m)
        {
            throw new ProfileValueException($"La cantidad '{quantity}' de una posición tiene que ser positiva.");
        }

        var currency = ResolveCurrency(row, columns, version);
        var openedAt = values.Date(Cell(row, columns, ImportField.OpenDate));
        var openPrice = values.Decimal(Cell(row, columns, ImportField.OpenPrice), "precio de apertura");
        var fee = Math.Abs(values.OptionalDecimal(Cell(row, columns, ImportField.Fee)) ?? 0m);
        var reference = Cell(row, columns, ImportField.NaturalId);

        yield return Leg(TransactionType.Buy, openedAt, openPrice, "open", fee);

        if (version.RowShape == RowShape.OpenPosition)
        {
            yield break;
        }

        yield return Leg(
            TransactionType.Sell,
            values.Date(Cell(row, columns, ImportField.CloseDate)),
            values.Decimal(Cell(row, columns, ImportField.ClosePrice), "precio de cierre"),
            "close",
            legFee: 0m);

        ImportRecord Leg(TransactionType type, DateTime moment, decimal price, string leg, decimal legFee) =>
            new(
                // Cada pata necesita su propio identificador: con el mismo, la segunda
                // se descartaría como duplicado de la primera.
                NaturalId: reference is null ? null : $"{reference}:{leg}",
                RowNumber: row.Number,
                Type: type,
                AssetSymbol: symbol.Trim().ToUpperInvariant(),
                AssetClass: AssetClassOf(version),
                Quantity: quantity,
                UnitPrice: price,
                GrossAmount: quantity * price,
                Currency: currency,
                Fee: legFee,
                Withholding: null,
                OccurredAt: null,
                NaiveOccurredAt: moment,
                SourceTimeZoneId: version.TimeZoneId,
                SplitRatio: null,
                RawContent: row.Raw);
    }

    private static AssetClass? AssetClassOf(ImportProfileVersion version) =>
        version.FixedAssetClass is { Length: > 0 } declared
            && Enum.TryParse<AssetClass>(declared, ignoreCase: true, out var assetClass)
            ? assetClass
            : null;

    private static Currency ResolveCurrency(
        TabularRow row,
        IReadOnlyDictionary<ImportField, int> columns,
        ImportProfileVersion version)
    {
        var code = Cell(row, columns, ImportField.Currency) ?? version.FixedCurrency;

        return string.IsNullOrWhiteSpace(code)
            ? throw new ProfileValueException("La fila no trae divisa y el perfil tampoco fija ninguna.")
            : Currency.FromCode(code.Trim());
    }

    private static ImportRecord Map(
        TabularRow row,
        IReadOnlyDictionary<ImportField, int> columns,
        ImportProfileVersion version,
        ProfileValueReader values,
        HashSet<string> unknownConcepts)
    {
        var concept = Cell(row, columns, ImportField.Concept);
        var type = ResolveType(concept, version, unknownConcepts);

        var currency = ResolveCurrency(row, columns, version);
        var date = values.Date(Cell(row, columns, ImportField.Date));
        var quantity = values.OptionalDecimal(Cell(row, columns, ImportField.Quantity)) ?? 0m;
        var unitPrice = values.OptionalDecimal(Cell(row, columns, ImportField.UnitPrice));

        // Hay informes que no traen el total, solo cantidad y precio. Calcularlo aquí
        // evita tener que preparar el fichero a mano antes de importarlo.
        var amount = version.AmountSource == AmountSource.QuantityTimesPrice
            ? quantity * (unitPrice ?? throw new ProfileValueException("La fila no trae precio unitario con el que calcular el importe."))
            : values.Decimal(Cell(row, columns, ImportField.GrossAmount), "importe");

        if (version.AmountIsAlwaysPositive)
        {
            amount = Math.Abs(amount);
        }

        var fee = values.OptionalDecimal(Cell(row, columns, ImportField.Fee)) ?? 0m;

        return new ImportRecord(
            NaturalId: Cell(row, columns, ImportField.NaturalId),
            RowNumber: row.Number,
            Type: type,
            AssetSymbol: Cell(row, columns, ImportField.AssetSymbol)?.ToUpperInvariant(),
            AssetClass: AssetClassOf(version),
            Quantity: quantity,
            UnitPrice: unitPrice,
            GrossAmount: Gross(version, type, amount, fee),
            Currency: currency,
            Fee: fee,
            Withholding: values.OptionalDecimal(Cell(row, columns, ImportField.Withholding)),
            OccurredAt: null,
            NaiveOccurredAt: date,
            SourceTimeZoneId: version.TimeZoneId,
            SplitRatio: values.OptionalDecimal(Cell(row, columns, ImportField.SplitRatio)),
            RawContent: row.Raw);
    }

    private static TransactionType ResolveType(
        string? concept,
        ImportProfileVersion version,
        HashSet<string> unknownConcepts,
        bool silent = false)
    {
        if (string.IsNullOrWhiteSpace(concept))
        {
            return TransactionType.Unknown;
        }

        var text = concept.Trim();

        if (version.Concepts.TryGetValue(text, out var type))
        {
            return type;
        }

        // Sin traducción entra como desconocido, no bloquea el fichero. La revisión
        // recoge después estos movimientos, y el perfil se corrige una vez.
        //
        // En un intercambio no se avisa: ahí el par de monedas dice lo que la fila es, y
        // que el concepto no esté traducido no significa que falte nada.
        if (!silent)
        {
            unknownConcepts.Add(text);
        }

        return TransactionType.Unknown;
    }

    private static string? Cell(
        TabularRow row,
        IReadOnlyDictionary<ImportField, int> columns,
        ImportField field)
    {
        if (!columns.TryGetValue(field, out var index))
        {
            return null;
        }

        var value = row.Cell(index)?.Trim();

        return string.IsNullOrEmpty(value) ? null : value;
    }
}
