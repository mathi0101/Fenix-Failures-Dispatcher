using System.Threading;

namespace RealFenixFailures.UI.Services;

/// <summary>
/// Garantiza que solo exista un proceso de la aplicación corriendo a la vez.
/// Usa un Mutex con nombre fijo para detectar instancias existentes y un
/// EventWaitHandle hermano para avisarle a la instancia primaria que debe
/// mostrarse cuando se intenta abrir una segunda instancia.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable {
    private const string MutexName = @"Local\RealFenixFailures.UI.SingleInstance.6F2B9E3E-7E0B-4C49-9C1E-2C9D2F8E3B41";
    private const string ShowEventName = @"Local\RealFenixFailures.UI.SingleInstance.6F2B9E3E-7E0B-4C49-9C1E-2C9D2F8E3B41.ShowRequest";

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private RegisteredWaitHandle? _registeredWait;

    /// <summary>
    /// Intenta tomar posesión del Mutex de instancia única.
    /// Devuelve false si ya hay otra instancia corriendo.
    /// </summary>
    public bool TryAcquire() {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var isNewInstance);
        return isNewInstance;
    }

    /// <summary>
    /// Señala a la instancia primaria (ya corriendo) que debe mostrarse.
    /// Debe llamarse cuando TryAcquire devolvió false.
    /// </summary>
    public void SignalExistingInstance() {
        using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        showEvent.Set();
    }

    /// <summary>
    /// Arranca la escucha del evento de "mostrar" para la instancia primaria.
    /// </summary>
    public void StartListening(Action onShowRequested) {
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        _registeredWait = ThreadPool.RegisterWaitForSingleObject(
            _showEvent,
            (_, _) => onShowRequested(),
            state: null,
            timeout: Timeout.InfiniteTimeSpan,
            executeOnlyOnce: false);
    }

    public void Dispose() {
        _registeredWait?.Unregister(null);
        _showEvent?.Dispose();

        if (_mutex is not null) {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }
}
