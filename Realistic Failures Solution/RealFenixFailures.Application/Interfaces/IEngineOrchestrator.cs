using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Domain.Enums;
using System.ComponentModel;

namespace RealFenixFailures.Application.Interfaces;

public interface IEngineOrchestrator : INotifyPropertyChanged {
    bool IsEngineActive { get; }
    UserAppMode CurrentMode { get; }

    ConnectionStatusDto ConnectionStatus { get; }

    /// Fase de vuelo actual conocida por el detector transversal (<see cref="IFlightEventDetector"/>).
    /// Se refresca en cada tick del timer automático, incluso cuando la transición de fase no genera
    /// un evento de log (p. ej. la primera clasificación tras Unknown).
    ComplexFlightPhaseEnum CurrentFlightPhase { get; }

    /// Metodos para controlar la actualizacion automática
    Task StartAutomaticTimerAsync(CancellationToken ct);
    Task UpdateConnection(CancellationToken ct);
    Task StopAutomaticTimerAsync(CancellationToken ct);
    bool IsTimerRunning { get; }

    // Métodos para controlar el motor de fallas
    Task StartRealisticModeAsync(int aircraftId, RiskLevel risk, int flightPlanId, CancellationToken ct);
    Task StartTrainingPresetAsync(int presetId, CancellationToken ct);
    Task StartCustomModeAsync(int presetId, bool activateImmediately, CancellationToken ct);

    Task StopCurrentModeAsync(CancellationToken ct);

    // Método para obtener el historial de fallas recientes
    Task<List<FailureTriggerLogDto>> GetRecentFailuresAsync(CancellationToken ct);

    // Reloj de simulación (mocks + motor comparten el mismo tiempo escalado)
    /// <summary>Multiplicador de velocidad del tiempo simulado (1 = tiempo real).</summary>
    double SimulatedTimeScale { get; }
    /// <summary>Cambia el multiplicador de velocidad del tiempo simulado en vivo.</summary>
    void SetSimulatedTimeScale(double scale);
    /// <summary>Salta el tiempo de escenario hacia adelante (p. ej. ir directo al descenso).</summary>
    void AdvanceSimulatedTime(TimeSpan scenarioTime);
}