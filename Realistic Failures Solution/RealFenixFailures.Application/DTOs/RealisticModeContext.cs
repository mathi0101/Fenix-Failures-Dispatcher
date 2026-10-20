using RealFenixFailures.Domain.Enums;

namespace RealFenixFailures.Application.DTOs;

public record RealisticModeContext(
    RiskLevel RiskLevel,
    UserAircraftDto Aircraft,
    int FlightPlanId
);
