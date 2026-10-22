using Microsoft.EntityFrameworkCore;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Infrastructure.Persistence;

namespace RealFenixFailures.Infrastructure.Services;

// Seed temporal SOLO de DEBUG. En una base recién creada siembra una aeronave
// de ejemplo (LV-DBG) con su historial: flight plans, sesiones voladas (algunas
// finalizadas, una sin finalizar), fallas disparadas en distintos estados del
// ciclo de vida y desgaste acumulado en varios sistemas. Evita cargar estos
// datos a mano cada vez que se regenera la base (se borra la .db local y
// EnsureCreated reconstruye el esquema). Corre solo si aún no existe la
// aeronave de ejemplo y debe ejecutarse DESPUÉS del catálogo de fallas
// (FK TriggeredFailure.FenixFailureId → FenixFailureDefinition), lo que
// garantiza el orden del pipeline IInitializerService (Application antes que
// Infrastructure). Todo el sembrado va en una única transacción.
public class DebugSampleDataSeeder : IDebugSampleDataSeeder {
    private const string DebugRegistration = "LV-DBG";

    private readonly RealFenixDbContext _dbContext;

    public DebugSampleDataSeeder(RealFenixDbContext dbContext) {
        _dbContext = dbContext;
    }

    public async Task InitializeAsync(CancellationToken ct) => await SeedIfEmptyAsync(ct);

    public async Task SeedIfEmptyAsync(CancellationToken ct) {
        // Idempotente: solo siembra en bases que todavía no tienen la aeronave de ejemplo.
        var exists = await _dbContext.UserAircrafts.AnyAsync(a => a.Registration == DebugRegistration, ct);
        if (exists)
            return;

        var now = DateTime.UtcNow;

        var aircraft = new UserAircraft {
            Registration = DebugRegistration,
            IcaoTypeCode = "A320",
            CreatedAt = now.AddDays(-45)
        };

        // Flight plans necesarios por la FK obligatoria de FlightSession.FlightPlanId.
        var plan1 = NewFlightPlan("SAEZ", "SBGR", "EZE-GRU", "ARG1452", now.AddDays(-30));
        var plan2 = NewFlightPlan("SCEL", "SAEZ", "SCL-EZE", "ARG1212", now.AddDays(-10));
        var plan3 = NewFlightPlan("SBGL", "SAEZ", "GIG-EZE", "ARG1251", now.AddDays(-3));

        var session1 = NewSession(aircraft, plan1, RiskLevel.High, now.AddDays(-30), now.AddDays(-30).AddMinutes(175));
        var session2 = NewSession(aircraft, plan2, RiskLevel.Moderate, now.AddDays(-10), now.AddDays(-10).AddMinutes(126));
        var session3 = NewSession(aircraft, plan3, RiskLevel.Low, now.AddDays(-3), now.AddDays(-3).AddMinutes(95));
        // Sesión abierta (sin FinishedAt): no cuenta para totales de horas/vuelos.
        var session4 = NewSession(aircraft, plan3, RiskLevel.Moderate, now.AddMinutes(-40), null);

        ((List<FlightSession>)aircraft.FlightSessions).AddRange([session1, session2, session3, session4]);

        // Desgaste acumulado coherente: sistemas con distinto nivel de uso.
        ((List<AircraftSystemWear>)aircraft.SystemWears).AddRange([
            NewWear(aircraft, 7, 22.0, now),    // Fuel
            NewWear(aircraft, 8, 31.5, now),    // Hydraulic power
            NewWear(aircraft, 11, 18.0, now),   // Landing gear
            NewWear(aircraft, 16, 9.0, now),    // APU
            NewWear(aircraft, 18, 15.0, now),   // Power plant
            NewWear(aircraft, 19, 6.0, now)     // Start faults
        ]);

        // Fallas disparadas en el historial, en distintos estados del ciclo de vida.
        // FenixFailureId debe existir en el catálogo (cargado antes por DatabaseSeeder).
        var failure1 = NewTriggeredFailure(session1, "F_FUEL_PUMP_LEFT_1", now.AddDays(-30).AddMinutes(55),
            "Baja presión de combustible, bomba izquierda", FailureSeverity.Major, FailureLifecycleState.ResolvedByPilot);
        var failure2 = NewTriggeredFailure(session1, "F_ENGINE_1", now.AddDays(-30).AddMinutes(120),
            "Oscilación del motor 1 (N1)", FailureSeverity.Critical, FailureLifecycleState.NotResolved);
        var failure3 = NewTriggeredFailure(session2, "F_HYD_LEAK_GREEN", now.AddDays(-10).AddMinutes(70),
            "Pérdida de fluido hidráulico, sistema verde", FailureSeverity.Major, FailureLifecycleState.AutoResolved);
        var failure4 = NewTriggeredFailure(session3, "F_BRAKE_ABCU", now.AddDays(-3).AddMinutes(15),
            "Fallas transitorias del sistema de frenos", FailureSeverity.Minor, FailureLifecycleState.Sent);

        session1.TriggeredFailures.Add(failure1);
        session1.TriggeredFailures.Add(failure2);
        session2.TriggeredFailures.Add(failure3);
        session3.TriggeredFailures.Add(failure4);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
        _dbContext.FlightPlans.AddRange(plan1, plan2, plan3);
        _dbContext.UserAircrafts.Add(aircraft);
        await _dbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private static FlightPlan NewFlightPlan(string origin, string dest, string route, string flightNumber, DateTime baseUtc) {
        return new FlightPlan {
            OriginIcao = origin,
            DestinationIcao = dest,
            AircraftIcaoType = "A320",
            FlightNumber = flightNumber,
            RouteText = route,
            Source = "Manual",
            OffBlockUtc = baseUtc,
            EstimatedArrivalUtc = baseUtc.AddHours(2).AddMinutes(15),
            EstimatedDurationMinutes = 135,
            DownloadedAtUtc = baseUtc.AddHours(-1),
            ValidFromUtc = baseUtc.AddHours(-1),
            ValidUntilUtc = baseUtc.AddHours(6),
            ValidationState = ValidationState.Valid
        };
    }

    private static FlightSession NewSession(UserAircraft aircraft, FlightPlan plan, RiskLevel risk, DateTime start, DateTime? finish) {
        return new FlightSession {
            FlightPlan = plan,
            UserAircraft = aircraft,
            RiskLevel = risk,
            StartedAt = start,
            FinishedAt = finish
        };
    }

    private static AircraftSystemWear NewWear(UserAircraft aircraft, int systemId, double pct, DateTime timestamp) {
        return new AircraftSystemWear {
            UserAircraft = aircraft,
            WearableSystemId = systemId,
            WearPercentage = pct,
            LastUpdatedAt = timestamp
        };
    }

    private static TriggeredFailure NewTriggeredFailure(FlightSession session, string fenixFailureId, DateTime triggeredAt,
        string name, FailureSeverity severity, FailureLifecycleState state) {
        var failure = new TriggeredFailure {
            FlightSessionId = session.Id, // FK real se asigna por EF Core en SaveChanges
            FlightSession = session,
            FenixFailureId = fenixFailureId,
            TriggeredAt = triggeredAt,
            FlightPhase = ComplexFlightPhaseEnum.Cruise,
            FailureName = name,
            Severity = severity,
            State = state,
            CanBeTemporary = false
        };

        if (state == FailureLifecycleState.ResolvedByPilot || state == FailureLifecycleState.AutoResolved) {
            failure.ResolvedAtUtc = triggeredAt.AddMinutes(18);
            failure.ResolutionSource = ResolutionSource.Pilot;
            failure.ResolvedPhase = ComplexFlightPhaseEnum.Descent;
        }

        return failure;
    }
}
