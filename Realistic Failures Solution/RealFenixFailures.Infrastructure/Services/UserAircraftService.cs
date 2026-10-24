using Microsoft.EntityFrameworkCore;
using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Infrastructure.Persistence;

namespace RealFenixFailures.Infrastructure.Services;

public class UserAircraftService : IUserAircraftService {
    private const int MaxRegistrationLength = 10;

    private static readonly string[] AllowedIcaoTypes = ["A319", "A320", "A321"];

    private readonly RealFenixDbContext _dbContext;

    public UserAircraftService(RealFenixDbContext dbContext) {
        _dbContext = dbContext;
    }

    // -------------------------------------------------------------------------
    // Create
    // -------------------------------------------------------------------------

    public async Task<UserAircraftDto> CreateAsync(CreateUserAircraftRequest request, CancellationToken ct) {
        var registration = request.Registration.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(registration) || registration.Length > MaxRegistrationLength) {
            throw new ArgumentException($"La matrícula debe tener entre 1 y {MaxRegistrationLength} caracteres.", nameof(request));
        }
        if (!AllowedIcaoTypes.Contains(request.IcaoTypeCode)) {
            throw new ArgumentException("El tipo ICAO debe ser uno de: A319, A320, A321.", nameof(request));
        }

        var aircraft = new UserAircraft {
            Registration = registration,
            IcaoTypeCode = request.IcaoTypeCode,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.UserAircrafts.Add(aircraft);
        await _dbContext.SaveChangesAsync(ct);

        return ToDto(aircraft);
    }

    // -------------------------------------------------------------------------
    // Delete
    // -------------------------------------------------------------------------

    public async Task DeleteAsync(int id, CancellationToken ct) {
        var aircraft = await _dbContext.UserAircrafts
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException($"UserAircraft {id} not found.");

        _dbContext.UserAircrafts.Remove(aircraft);
        await _dbContext.SaveChangesAsync(ct);
    }

    // -------------------------------------------------------------------------
    // Get
    // -------------------------------------------------------------------------

    public async Task<IReadOnlyList<UserAircraftDto>> GetAllAsync(CancellationToken ct) {
        var aircrafts = await _dbContext.UserAircrafts
            .AsNoTracking()
            .Include(x => x.FlightSessions)
            .Include(x => x.SystemWears)
                .ThenInclude(x => x.WearableSystem)
            .ToListAsync(ct);

        return aircrafts.Select(ToDto).ToArray();
    }

    public async Task<UserAircraftDto?> GetByIdAsync(int id, CancellationToken ct) {
        var aircraft = await _dbContext.UserAircrafts
            .Include(x => x.SystemWears)
                .ThenInclude(x=>x.WearableSystem)
            .Include(x => x.FlightSessions)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        return aircraft is null ? null : ToDto(aircraft);
    }

    public async Task<AircraftDashboardDto> GetDashboardAsync(int userAircraftId, CancellationToken ct) {
        var aircraft = await _dbContext.UserAircrafts
            .AsNoTracking()
            .Include(x => x.SystemWears)
                .ThenInclude(x => x.WearableSystem)
            .Include(x => x.FlightSessions)
                .ThenInclude(s => s.TriggeredFailures)
                    .ThenInclude(s => s.FenixFailure)
            .FirstOrDefaultAsync(x => x.Id == userAircraftId, ct)
            ?? throw new KeyNotFoundException($"UserAircraft {userAircraftId} not found.");

        //var sessions = await GetAircraftSessionsAsync(userAircraftId, ct);

        return new AircraftDashboardDto {
            Aircraft = ToDto(aircraft),
            SystemWears = aircraft.SystemWears
                .Select(ToSystemWearDto)
                .ToList(),
            TotalFailuresTriggered = aircraft.FlightSessions
                .SelectMany(s => s.TriggeredFailures)
                .Count()
        };
    }

    // -------------------------------------------------------------------------
    // Sessions
    // -------------------------------------------------------------------------

    public async Task<IReadOnlyList<FlightSessionDto>> GetAircraftSessionsAsync(int userAircraftId, CancellationToken ct) {
        var sessions = await _dbContext.FlightSessions
            .AsNoTracking()
            .Where(s => s.UserAircraftId == userAircraftId)
            .OrderByDescending(s => s.StartedAt)
            .Include(s => s.TriggeredFailures)
            .ThenInclude(s => s.FenixFailure)
            .Include(s => s.EventLogs)
            .Include(s => s.FlightPlan)
            .ToListAsync(ct);

        return sessions.Select(ToSessionDto).ToArray();
    }

    // -------------------------------------------------------------------------
    // System Wears
    // -------------------------------------------------------------------------

    public async Task<IReadOnlyList<AircraftSystemWearDto>> GetSystemWearsAsync(int userAircraftId, CancellationToken ct) {
        return await _dbContext.AircraftSystemWears
            .Where(w => w.UserAircraftId == userAircraftId)
            .Select(x => ToSystemWearDto(x))
            .ToListAsync(ct);
    }

    // -------------------------------------------------------------------------
    // Mappers
    // -------------------------------------------------------------------------

    private static UserAircraftDto ToDto(UserAircraft aircraft) => new() {
        Id = aircraft.Id,
        Registration = aircraft.Registration,
        IcaoTypeCode = aircraft.IcaoTypeCode,
        TotalFlightHours = aircraft.FlightSessions
            .Where(s => s.FinishedAt != null)
            .Sum(s => (s.FinishedAt!.Value - s.StartedAt).TotalHours),
        TotalFlights = aircraft.FlightSessions.Count(s => s.FinishedAt != null),
        CreatedAt = aircraft.CreatedAt,
        SystemsWear = aircraft.SystemWears.Select(ToSystemWearDto).ToArray(),
        FlightSessions = aircraft.FlightSessions.Select(ToSessionDto).ToArray()
    };

    private static AircraftSystemWearDto ToSystemWearDto(AircraftSystemWear w) => new() {
        UserAircraftId = w.UserAircraftId,
        WearPercentage = w.WearPercentage,
        LastUpdatedAt = w.LastUpdatedAt,
        SystemWear = new SystemWearDto() {
            Id = w.WearableSystemId,
            Name = w.WearableSystem!.Name,
            ShortName = w.WearableSystem!.ShortName,
            DisplayOrder = w.WearableSystem!.DisplayOrder,
            AtaId = w.WearableSystem!.AtaId
        },
    };

    private static FlightSessionDto ToSessionDto(FlightSession f) => new() {
        Id = f.Id,
        StartedAt = f.StartedAt,
        RiskLevel = f.RiskLevel,
        FinishedAt = f.FinishedAt,
        OriginIcao = f.FlightPlan?.OriginIcao,
        DestinationIcao = f.FlightPlan?.DestinationIcao,
        OffBlockUtc = f.FlightPlan?.OffBlockUtc,
        TriggeredFailures = f.TriggeredFailures.Select(f => new TriggeredFailureDto {
            Id = f.Id,
            FenixFailureId = f.FenixFailureId,
            FailureName = f.FailureName ?? f.FenixFailure?.Name ?? f.FenixFailureId,
            TriggeredAt = f.TriggeredAt,
            FlightPhase = f.FlightPhase,
            Severity = f.Severity,
            PresetId = f.PresetId,
            PresetName = f.PresetName,
            Probability = f.Probability,
            ProbabilityGroup = f.ProbabilityGroup,
            Ias = f.Ias,
            AboveAltitude = f.AboveAltitude,
            BelowAltitude = f.BelowAltitude,
            Time = f.Time,
            AfterEvent = f.AfterEvent,
            AfterEventSeconds = f.AfterEventSeconds
        }).ToArray(),
        EventLogs = f.EventLogs.Select(ToEventLogDto).ToArray(),
        Result = f.FinishedAt is null || f.TriggeredFailures.Count == 0 ? null : new ResultSummaryDto {
            TotalFailures = f.TriggeredFailures.Count,
            ActiveAtEnd = f.TriggeredFailures.Count(x => x.State is FailureLifecycleState.Sent or FailureLifecycleState.Queued or FailureLifecycleState.Active or FailureLifecycleState.Preparing),
            ResolvedByPilot = f.TriggeredFailures.Count(x => x.State == FailureLifecycleState.ResolvedByPilot),
            AutoResolved = f.TriggeredFailures.Count(x => x.State == FailureLifecycleState.AutoResolved),
            Rejected = f.TriggeredFailures.Count(x => x.State == FailureLifecycleState.Rejected),
            Minor = f.TriggeredFailures.Count(x => x.Severity == FailureSeverity.Minor),
            Major = f.TriggeredFailures.Count(x => x.Severity == FailureSeverity.Major),
            Critical = f.TriggeredFailures.Count(x => x.Severity == FailureSeverity.Critical)
        }
    };

    private static SessionEventLogDto ToEventLogDto(SessionEventLog e) => new() {
        Id = e.Id,
        OccurredAt = e.OccurredAt,
        EventCode = e.EventCode,
        Category = e.Category,
        Description = e.Description,
        FlightPhase = e.FlightPhase,
        TriggeredFailureId = e.TriggeredFailureId
    };
}
