// RealFenixFailures.TestUnits/Session/RealisticSessionEnginePersistenceTests.cs
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Application.Session;
using RealFenixFailures.Application.Services;
using RealFenixFailures.Domain.DTOs;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Interfaces;
using RealFenixFailures.Domain.Services;
using RealFenixFailures.Infrastructure.Persistence;
using RealFenixFailures.Infrastructure.Repositories;
using RealFenixFailures.TestUnits.Mocks;

namespace RealFenixFailures.TestUnits.Session;

/// <summary>
/// Integración del engine con repositorios REALES sobre un DbContext SQLite en memoria.
/// Cubre la persistencia de fallas de tierra sembradas en OnGate sin conflicto de tracking de EF
/// (regresión: BuildGroundCandidatesAsync reconciliaba navegaciones desde una instancia AsNoTracking,
/// envenenando el contexto de la sesión y rompiendo todo SaveChanges posterior).
/// </summary>
public class RealisticSessionEnginePersistenceTests {
    private readonly Mock<ISimulatorConnectionService> _simulator = new();
    private readonly Mock<IFlightEventDetector> _flightEventDetector = new();
    private readonly MockRandom _random = new();
    private readonly DateTime _now = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);
    private const string GroundFailureId = "F_ELEC_APU_GEN";

    private static RealFenixDbContext CreateContext() {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<RealFenixDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new RealFenixDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static (FenixFailureSystem System, FenixFailureGroup Group, FenixFailureDefinition Definition, FailurePreset Preset, UserAircraft Aircraft, FlightPlan FlightPlan) Seed(RealFenixDbContext db) {
        var now = DateTime.UtcNow;
        var system = new FenixFailureSystem { Name = "ELEC", ShortName = "ELEC", AtaId = 24 };
        var group = new FenixFailureGroup { Name = "APU", System = system };
        var definition = new FenixFailureDefinition {
            FenixFailureId = GroundFailureId, Name = "APU Generator",
            Group = group, Severity = FailureSeverity.Minor
        };
        var preset = new FailurePreset {
            Name = "P-GATE", Description = "d", TriggerDescription = "t",
            PresetTypeId = (int)PresetTypeEnum.RealisticMode, FlightPhase = ComplexFlightPhaseEnum.OnGate
        };
        var pfd = new PresetFailureDefinition {
            Preset = preset, FenixFailureId = definition.FenixFailureId,
            Probability = 1.0, FenixFailure = definition
        };
        preset.PresetFailureDefinitions.Add(pfd);
        var aircraft = new UserAircraft { Registration = "TEST-GATE-01", IcaoTypeCode = "A320", CreatedAt = DateTime.UtcNow };
        var flightPlan = new FlightPlan {
            OriginIcao = "SAEZ", DestinationIcao = "SBGR", AircraftIcaoType = "A320",
            Source = "SIMBRIEF", DownloadedAtUtc = now, ValidFromUtc = now.AddHours(-2), ValidUntilUtc = now.AddHours(2)
        };
        db.AddRange(system, group, definition, preset, aircraft, flightPlan);
        db.SaveChanges();
        return (system, group, definition, preset, aircraft, flightPlan);
    }

    private (RealisticSessionEngine Engine, RealFenixDbContext Db) CreateEngineWithRealRepos(RealFenixDbContext db, Func<DateTime>? utcNow = null) {
        var presetRepo = new PresetRepository(NullLogger<PresetRepository>.Instance, db);
        var defRepo = new FenixFailureDefinitionRepository(db);
        var flightSessionRepo = new FlightSessionRepository(db);
        var flightPlanRepo = new FlightPlanRepository(db);
        var triggeredRepo = new TriggeredFailureRepository(db);
        var presetService = new PresetService(presetRepo, new Mock<IPresetsLoader>().Object);
        var sessionService = new FlightSessionService(flightSessionRepo, triggeredRepo);

        var settings = new RealisticModeSettings {
            GroundFailureEnabled = true,
            EvaluationIntervalSeconds = 30,
            Physics = new RealisticPhysicsSettings { GroundSeedBaseProbability = 1.0 },
            Wear = new WearSettings { SessionSystemsCap = 100 },
            Profiles = {
                new RealisticProfileSettings { RiskLevel = (int)RiskLevel.Moderate, ProbabilityMultiplier = 1.0, WearMaxMultiplier = 3.0, GroundFailuresCap = 1 }
            }
        };
        var engine = new RealisticSessionEngine(
            _simulator.Object,
            NullLogger<RealisticSessionEngine>.Instance,
            sessionService,
            presetService,
            _flightEventDetector.Object,
            flightPlanRepo,
            triggeredRepo,
            settings,
            new RealisticFailurePolicy(new FailureRuleEvaluator(), utcNow ?? (() => _now), _random),
            defRepo,
            new AircraftSystemWearRepository(db));
        _flightEventDetector
            .Setup(f => f.ProcessSnapshot(It.IsAny<SimulatorAircraftStateSnapshot>()))
            .Returns(ComplexFlightPhaseEnum.OnGate);
        return (engine, db);
    }

    private void SetupSimulatorConnected() {
        _simulator
            .Setup(s => s.GetConnectionStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectionStatusDto(true, true, SimpleFlightPhaseEnum.OnGround));
        _simulator
            .Setup(s => s.GetSimulatorData(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<SimulatorAircraftStateSnapshot>.Ok(new SimulatorAircraftStateSnapshot {
                IsOnGround = true, AltitudeMSL = 100, AltitudeAGL = 0, HasSimulatorData = true
            }));
        _simulator
            .Setup(s => s.GetCurrentFenixFailures(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<AllFenixFailuresResponseDto?>.Ok(new AllFenixFailuresResponseDto()));
        _simulator
            .Setup(s => s.ExecuteFailureAsync(It.IsAny<PresetFailureDefinition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _simulator
            .Setup(s => s.DisarmFailureAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    [Fact]
    public async Task GroundSeed_OnGate_TriggeredFailurePersistsWithoutTrackingConflict() {
        var db = CreateContext();
        var seeded = Seed(db);
        SetupSimulatorConnected();
        var (engine, _) = CreateEngineWithRealRepos(db);
        _random.SetupNextDouble(0.0); // Bernoulli del único candidato: 0.0 < p=1.0 → entra

        var start = await engine.TryStartSession(new RealisticModeContext(
            RiskLevel.Moderate, new UserAircraftDto { Id = seeded.Aircraft.Id, Registration = seeded.Aircraft.Registration }, seeded.FlightPlan.Id),
            CancellationToken.None);

        Assert.True(start.Started, "no arrancó la sesión");
        Assert.Equal(1, engine.PreparedCount);

        var before = DateTime.UtcNow;
        var result = await engine.EvaluateAsync(CancellationToken.None);
        var after = DateTime.UtcNow;

        Assert.False(result.ShouldPauseSession);
        Assert.Single(result.TriggeredFailures);
        var persisted = await db.TriggeredFailures.ToListAsync();
        Assert.Single(persisted);
        Assert.Equal(GroundFailureId, persisted[0].FenixFailureId);
        Assert.Equal(FailureLifecycleState.Sent, persisted[0].State);
        Assert.InRange(persisted[0].TriggeredAt, before, after);
        Assert.Equal(persisted[0].TriggeredAt, result.TriggeredFailures[0].TriggeredAtUtc);
        Assert.NotNull(await db.SessionEventLogs.FirstOrDefaultAsync(e => e.EventCode == "FAILURE_TRIGGERED"));
    }

    [Fact]
    public async Task StopSession_AircraftWithoutWearRows_CreatesAndPersistsCatalogWear() {
        using var db = CreateContext();
        var seeded = Seed(db);
        var wearable = new AircraftWearableSystem {
            AtaId = 999, Name = "Test system", ShortName = "Test", WearPerFlightHour = 0.6
        };
        db.Add(wearable);
        db.SaveChanges();
        SetupSimulatorConnected();
        var clock = _now;
        var (engine, _) = CreateEngineWithRealRepos(db, () => clock);
        Assert.True((await engine.TryStartSession(new RealisticModeContext(RiskLevel.Moderate,
            new UserAircraftDto { Id = seeded.Aircraft.Id, Registration = seeded.Aircraft.Registration }, seeded.FlightPlan.Id),
            CancellationToken.None)).Started);
        // Anclaje de desgaste: primer encendido de motores una hora después del arranque.
        clock = clock.AddHours(1);
        _flightEventDetector
            .Setup(f => f.ProcessSnapshot(It.IsAny<SimulatorAircraftStateSnapshot>()))
            .Callback(() => _flightEventDetector.Raise(d => d.FlightEventDetected += null, _flightEventDetector.Object,
                new FlightEventDto(DateTime.UtcNow, FlightEventKind.EnginesStarted, "Motores encendidos")))
            .Returns(ComplexFlightPhaseEnum.Takeoff);
        await engine.EvaluateAsync(CancellationToken.None);
        clock = clock.AddHours(2);

        await engine.StopSession(CancellationToken.None);
        db.ChangeTracker.Clear();

        var wear = await db.AircraftSystemWears.SingleAsync(w => w.UserAircraftId == seeded.Aircraft.Id && w.WearableSystemId == wearable.Id);
        Assert.Equal(1.2, wear.WearPercentage, 6);
        var delta = await db.Set<SessionSystemWearDelta>().SingleAsync(d => d.AtaId == 999);
        Assert.Equal(1.2, delta.DeltaPercentage, 6);
        Assert.Equal(1.2, delta.TotalPercentage, 6);
    }

    [Fact]
    public async Task StopSession_AccumulatesWearFromEnginesStart() {
        // Espec §3.2: al cerrar sesión, cada sistema suma WearPerFlightHour × horas de vuelo desde el
        // primer encendido de motores (tiempo de escenario), con clamp 0–100. La duración del OFP no participa.
        var db = CreateContext();
        var seeded = Seed(db);
        var wearable = new AircraftWearableSystem {
            AtaId = 24, Name = "Electrical power", ShortName = "Elec", DisplayOrder = 1, WearPerFlightHour = 0.6
        };
        db.Add(wearable);
        db.SaveChanges();
        db.Add(new AircraftSystemWear {
            UserAircraftId = seeded.Aircraft.Id, WearableSystemId = wearable.Id,
            WearPercentage = 50, LastUpdatedAt = _now
        });
        db.SaveChanges();

        SetupSimulatorConnected();
        var clock = DateTime.UtcNow;
        var (engine, _) = CreateEngineWithRealRepos(db, () => clock);
        var start = await engine.TryStartSession(new RealisticModeContext(
            RiskLevel.Moderate, new UserAircraftDto { Id = seeded.Aircraft.Id, Registration = seeded.Aircraft.Registration }, seeded.FlightPlan.Id),
            CancellationToken.None);
        Assert.True(start.Started);

        // Una hora de escenario pre-motores no acumula: el anclaje es el ENGINES_STARTED.
        clock = clock.AddHours(1);
        _flightEventDetector
            .Setup(f => f.ProcessSnapshot(It.IsAny<SimulatorAircraftStateSnapshot>()))
            .Callback(() => _flightEventDetector.Raise(d => d.FlightEventDetected += null, _flightEventDetector.Object,
                new FlightEventDto(DateTime.UtcNow, FlightEventKind.EnginesStarted, "Motores encendidos")))
            .Returns(ComplexFlightPhaseEnum.Takeoff);
        await engine.EvaluateAsync(CancellationToken.None);

        // Dos horas de escenario desde el encendido acumulan desgaste sin desplazar las fechas UTC del historial.
        var beforeStop = DateTime.UtcNow;
        clock = clock.AddHours(2);
        await engine.StopSession(CancellationToken.None);
        var afterStop = DateTime.UtcNow;

        var wear = await db.AircraftSystemWears.AsNoTracking().SingleAsync(w => w.UserAircraftId == seeded.Aircraft.Id && w.WearableSystemId == wearable.Id);
        Assert.InRange(wear.WearPercentage, 51.2, 51.21);
        Assert.InRange(wear.LastUpdatedAt, beforeStop, afterStop);
        var session = await db.FlightSessions.AsNoTracking().SingleAsync();
        Assert.InRange(session.FinishedAt!.Value, beforeStop, afterStop);
    }

    [Fact]
    public async Task StopSession_WithoutEnginesStarted_DoesNotAccumulateWear() {
        var db = CreateContext();
        var seeded = Seed(db);
        var wearable = new AircraftWearableSystem {
            AtaId = 24, Name = "Electrical power", ShortName = "Elec", DisplayOrder = 1, WearPerFlightHour = 0.6
        };
        db.Add(wearable);
        db.SaveChanges();
        db.Add(new AircraftSystemWear {
            UserAircraftId = seeded.Aircraft.Id, WearableSystemId = wearable.Id,
            WearPercentage = 50, LastUpdatedAt = _now
        });
        db.SaveChanges();

        SetupSimulatorConnected();
        var clock = DateTime.UtcNow;
        var (engine, _) = CreateEngineWithRealRepos(db, () => clock);
        var start = await engine.TryStartSession(new RealisticModeContext(
            RiskLevel.Moderate, new UserAircraftDto { Id = seeded.Aircraft.Id, Registration = seeded.Aircraft.Registration }, seeded.FlightPlan.Id),
            CancellationToken.None);
        Assert.True(start.Started);

        // Dos horas de escenario sin encendido de motores (cierre en gate): no acumula desgaste.
        clock = clock.AddHours(2);
        await engine.StopSession(CancellationToken.None);

        var wear = await db.AircraftSystemWears.AsNoTracking().SingleAsync(w => w.UserAircraftId == seeded.Aircraft.Id && w.WearableSystemId == wearable.Id);
        Assert.Equal(50, wear.WearPercentage);
        Assert.False(db.Set<SessionSystemWearDelta>().Any(d => d.AtaId == wearable.AtaId));
    }
}
