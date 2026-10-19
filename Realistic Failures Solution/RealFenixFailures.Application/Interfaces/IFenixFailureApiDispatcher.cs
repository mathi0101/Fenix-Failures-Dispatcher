using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Domain.DTOs;

namespace RealFenixFailures.Application.Interfaces;

public interface IFenixFailureApiDispatcher {
    Task<bool> IsApiAvailableAsync(CancellationToken ct);
    Task<AllFenixFailuresResponseDto?> GetAllFailuresAsync(CancellationToken ct);
    Task<bool> ArmFailureAsync(FenixArmFailureRequest def, CancellationToken ct);
    Task<bool> DisarmFailureAsync(FenixArmFailureRequest def, CancellationToken ct);
    Task<bool> ResetAllFailuresAsync(CancellationToken ct);
}
