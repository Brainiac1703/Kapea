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

    /// <summary>Fecha de apertura, en un informe cuya fila es una posición entera.</summary>
    OpenDate = 12,

    /// <summary>Precio de apertura, en un informe cuya fila es una posición entera.</summary>
    OpenPrice = 13,

    /// <summary>Fecha de cierre, en un informe de posiciones ya transmitidas.</summary>
    CloseDate = 14,

    /// <summary>Precio de cierre, en un informe de posiciones ya transmitidas.</summary>
    ClosePrice = 15,

    /// <summary>Lo que sale en un intercambio: cantidad y moneda.</summary>
    OriginAmount = 16,

    OriginCurrency = 17,

    /// <summary>Lo que entra en un intercambio: cantidad y moneda.</summary>
    DestinationAmount = 18,

    DestinationCurrency = 19,
}

/// <summary>
/// Qué representa cada fila del fichero.
/// </summary>
/// <remarks>
/// No todos los informes son una lista de apuntes. Los de posiciones ponen la compra y
/// la venta en la misma fila, y es un formato común: sin esto, cada bróker que exporte
/// así volvería a necesitar código propio.
/// </remarks>
public enum RowShape
{
    /// <summary>Un apunte por fila. Es lo que hace un extracto de efectivo.</summary>
    SingleMovement = 1,

    /// <summary>Una posición todavía abierta: solo su adquisición.</summary>
    OpenPosition = 2,

    /// <summary>Una posición ya cerrada: su adquisición y su transmisión.</summary>
    OpenAndClosePosition = 3,

    /// <summary>
    /// Un intercambio: algo que sale y algo que entra.
    /// </summary>
    /// <remarks>
    /// Es la forma de los extractos de los exchanges de cripto. Lo que la fila significa
    /// no lo dice un texto sino las dos monedas: pagar con euros es comprar, cobrar
    /// euros es vender, y cambiar una cripto por otra son las dos cosas a la vez.
    /// </remarks>
    ExchangePair = 4,
}

/// <summary>De dónde sale el importe de la operación.</summary>
public enum AmountSource
{
    /// <summary>De su columna, tal y como viene.</summary>
    Column = 1,

    /// <summary>De multiplicar cantidad por precio, cuando el informe no trae el total.</summary>
    QuantityTimesPrice = 2,
}

/// <summary>Cómo escribe los números el fichero.</summary>
public enum DecimalConvention
{
    /// <summary>Coma decimal y punto de millares: 1.234,56. Lo habitual en un extracto español.</summary>
    European = 1,

    /// <summary>Punto decimal: 1234.56. Lo habitual en una exportación en inglés.</summary>
    Invariant = 2,
}
