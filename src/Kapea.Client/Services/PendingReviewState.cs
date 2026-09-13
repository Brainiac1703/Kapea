using Kapea.Shared.Contracts;

namespace Kapea.Client.Services;

/// <summary>
/// Cuánto queda por revisar, compartido entre el menú y la pantalla de revisión.
/// </summary>
/// <remarks>
/// Uno para toda la aplicación, igual que la sesión: si cada componente guardara su
/// propio recuento, resolver un pendiente en la pantalla de revisión dejaría el menú
/// enseñando el número viejo hasta la siguiente navegación.
/// </remarks>
public sealed class PendingReviewState(KapeaApiClient api)
{
    public int Count { get; private set; }

    public event Action? Changed;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        PendingReviewResponse pending;

        try
        {
            pending = await api.GetPendingReviewAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is KapeaApiException or HttpRequestException)
        {
            // Un aviso que no se puede calcular no merece un mensaje de error en cada
            // navegación. Se queda con el último número conocido, que sigue siendo
            // mejor pista que ninguna.
            return;
        }

        if (pending.Total == Count)
        {
            return;
        }

        Count = pending.Total;
        Changed?.Invoke();
    }
}
