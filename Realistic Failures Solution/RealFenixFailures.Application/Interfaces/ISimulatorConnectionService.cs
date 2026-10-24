using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Domain.DTOs;
using RealFenixFailures.Domain.Entities;

namespace RealFenixFailures.Application.Interfaces;

public interface ISimulatorConnectionService {
    Task<ConnectionStatusDto> GetConnectionStatusAsync(CancellationToken ct);
    Task<bool> ExecuteFailureAsync(PresetFailureDefinition failureDefinition, CancellationToken ct);
    Task<bool> DisarmFailureAsync(string fenixFailureId, CancellationToken ct);
    Task<ServiceResult<IReadOnlyList<PresetFailureDefinition>>> ExecutePresetAsync(FailurePreset preset, CancellationToken ct);
    Task<bool> ResetAllFailuresAsync(CancellationToken ct);
    Task<ServiceResult<AllFenixFailuresResponseDto?>> GetCurrentFenixFailures(CancellationToken ct);
    Task<ServiceResult<SimulatorAircraftStateSnapshot>> GetSimulatorData(CancellationToken ct);
}
