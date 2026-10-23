using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Interfaces;
using RealFenixFailures.Domain.Interfaces.Repositories;
using RealFenixFailures.Domain.Services;

namespace RealFenixFailures.Application.Services;

/// <inheritdoc />
public sealed class SimbriefOfpService : ISimbriefOfpService {
    private readonly IOfpFetcher _fetcher;
    private readonly ISimBriefOfpReader _ofpReader;
    private readonly IFlightPlanRepository _flightPlanRepository;
    private readonly RealisticModeSettings _modeSettings;
    private readonly ILogger<SimbriefOfpService> _logger;

    public SimbriefOfpService(
        IOfpFetcher fetcher,
        ISimBriefOfpReader ofpReader,
        IFlightPlanRepository flightPlanRepository,
        IOptions<FailureEngineSettings> settings,
        ILogger<SimbriefOfpService> logger) {
        _fetcher = fetcher;
        _ofpReader = ofpReader;
        _flightPlanRepository = flightPlanRepository;
        _modeSettings = settings.Value.RealisticMode;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<FlightPlanDto> DownloadAsync(string aircraftIcaoTypeCode, string simbriefUserId, CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(aircraftIcaoTypeCode)) {
            throw new ArgumentException("Debe indicarse el tipo de aeronave para validar el OFP.", nameof(aircraftIcaoTypeCode));
        }
        if (string.IsNullOrWhiteSpace(simbriefUserId)) {
            throw new ArgumentException("Debe indicarse el userid de SimBrief.", nameof(simbriefUserId));
        }

        using var stream = await _fetcher.FetchAsync(simbriefUserId, ct);
        var plan = await _ofpReader.ReadAsync(stream, ct);

        return await PersistAndValidateAsync(aircraftIcaoTypeCode, plan, simbriefUserId, ct);
    }

    private async Task<FlightPlanDto> PersistAndValidateAsync(string expectedIcao, FlightPlan plan, string source, CancellationToken ct) {
        plan.ValidationState = ApplyValidation(expectedIcao, plan, out var message);
        plan.ValidationMessage = message;
        plan.Source = source;

        await _flightPlanRepository.AddAsync(plan, ct);

        _logger.LogInformation("OFP procesado. Origen={Origin} Destino={Dest} Estado={State} Motivo={Msg}",
            plan.OriginIcao, plan.DestinationIcao, plan.ValidationState, message);
        return ToDto(plan);
    }

    private ValidationState ApplyValidation(string expectedIcao, FlightPlan plan, out string message) {
        // 1) ICAO coincide con la aeronave registrada.
        if (!string.Equals(plan.AircraftIcaoType.Trim().ToUpperInvariant(), expectedIcao.Trim().ToUpperInvariant(), StringComparison.Ordinal)) {
            message = $"El OFP corresponde a {plan.AircraftIcaoType}, no a {expectedIcao.ToUpperInvariant()}.";
            return ValidationState.Invalid;
        }

        // 2) Campos mínimos presentes (origen, destino, duración estimada).
        if (string.IsNullOrWhiteSpace(plan.OriginIcao) || string.IsNullOrWhiteSpace(plan.DestinationIcao) || plan.EstimatedDurationMinutes <= 0) {
            message = "El OFP no tiene los datos mínimos (origen, destino, duración estimada).";
            return ValidationState.Invalid;
        }

        // 3) Ventana de tiempo (UTC): −PreStartWindowHours del offblock .. +PostArrivalWindowHours del aterrizaje estimado.
        var now = DateTime.UtcNow;
        var validFrom = plan.OffBlockUtc.AddHours(-_modeSettings.Ofp.PreStartWindowHours);
        var validUntil = plan.EstimatedArrivalUtc.AddHours(_modeSettings.Ofp.PostArrivalWindowHours);

        if (now < validFrom) {
            message = $"El plan de vuelo aún no es válido: podés iniciar desde el {validFrom:yyyy-MM-dd HH:mm 'UTC'} (offblock en {plan.OffBlockUtc:HH:mm 'UTC'}).";
            return ValidationState.Expired;
        }
        if (now > validUntil) {
            message = $"El plan de vuelo venció (válido hasta el {validUntil:yyyy-MM-dd HH:mm 'UTC'}).";
            return ValidationState.Expired;
        }

        message = $"{plan.OriginIcao} → {plan.DestinationIcao}, válido {validFrom:HH:mm}–{validUntil:HH:mm} UTC";
        return ValidationState.Valid;
    }

    private static FlightPlanDto ToDto(FlightPlan p) => new() {
        Id = p.Id,
        OriginIcao = p.OriginIcao,
        DestinationIcao = p.DestinationIcao,
        AircraftIcaoType = p.AircraftIcaoType,
        FlightNumber = p.FlightNumber,
        Callsign = p.Callsign,
        OffBlockUtc = p.OffBlockUtc,
        EstimatedArrivalUtc = p.EstimatedArrivalUtc,
        EstimatedDurationMinutes = p.EstimatedDurationMinutes,
        RouteDistanceNM = p.RouteDistanceNM,
        RouteText = p.RouteText,
        ValidationState = p.ValidationState,
        ValidationMessage = p.ValidationMessage,
        Source = p.Source,
        DownloadedAtUtc = p.DownloadedAtUtc,
    };
}
