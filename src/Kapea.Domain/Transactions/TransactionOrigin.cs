namespace Kapea.Domain.Transactions;

/// <summary>De dónde sale un movimiento. Un ajuste manual no se confunde nunca con un dato importado.</summary>
public enum TransactionOrigin
{
    Imported = 1,

    /// <summary>Corrección sobre datos importados, con motivo obligatorio. No se edita.</summary>
    ManualAdjustment = 2,

    /// <summary>
    /// Apuntado a mano como dato normal de una cuenta, con nota opcional.
    /// </summary>
    /// <remarks>
    /// No es una corrección: es lo que se registra cuando el movimiento no llega por
    /// ninguna otra vía. Por eso no exige motivo y, al no tener registro de origen que
    /// conservar, se puede editar.
    /// </remarks>
    Manual = 3,
}
