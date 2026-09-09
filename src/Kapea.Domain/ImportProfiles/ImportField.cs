namespace Kapea.Domain.ImportProfiles;

/// <summary>
/// Campo del movimiento normalizado al que puede apuntar una columna del fichero.
/// </summary>
/// <remarks>
/// Es la lista cerrada de destinos posibles de un mapeo. Cerrada a propósito: son los
/// campos que el motor de importación sabe interpretar, y ofrecer otros en la pantalla
/// de mapeo prometería algo que después no se guarda en ninguna parte.
/// </remarks>
public enum ImportField
{
    /// <summary>Cuándo ocurrió. Sin esto no hay movimiento.</summary>
    Date = 1,

    /// <summary>Texto del origen que dice qué fue: «Compra», «Dividendo», «Stocks/ETF sale».</summary>
    Concept = 2,

    /// <summary>Importe de la operación. Sin esto no hay cifra que calcular.</summary>
    GrossAmount = 3,

    AssetSymbol = 4,

    Quantity = 5,

    UnitPrice = 6,

    Currency = 7,

    Fee = 8,

    Withholding = 9,

    /// <summary>Identificador del origen, si lo trae. Es la mejor huella de deduplicación.</summary>
    NaturalId = 10,

    SplitRatio = 11,
}

/// <summary>Cómo escribe los números el fichero.</summary>
public enum DecimalConvention
{
    /// <summary>Coma decimal y punto de millares: 1.234,56. Lo habitual en un extracto español.</summary>
    European = 1,

    /// <summary>Punto decimal: 1234.56. Lo habitual en una exportación en inglés.</summary>
    Invariant = 2,
}
