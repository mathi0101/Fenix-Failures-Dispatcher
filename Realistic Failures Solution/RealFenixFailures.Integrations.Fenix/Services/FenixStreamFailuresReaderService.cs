using Microsoft.Extensions.Logging;
using RealFenixFailures.Domain.DTOs;
using RealFenixFailures.Domain.Interfaces;
using RealFenixFailures.Integrations.Fenix.Mappers;
using RealFenixFailures.Integrations.Fenix.Models;
using System.Text.Json;

namespace RealFenixFailures.Integrations.Fenix.Services;

public class FenixStreamFailuresReaderService : IFenixStreamFailuresReaderService {
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly ILogger<FenixStreamFailuresReaderService> _logger;

    public FenixStreamFailuresReaderService(ILogger<FenixStreamFailuresReaderService> logger) {
        _logger = logger;
    }


    public async Task<AllFenixFailuresResponseDto> ReadAsync(Stream stream, CancellationToken ct) {
        try {
            var payload = await JsonSerializer.DeserializeAsync<FenixManualFailuresResponse>(
            stream,
            JsonOptions,
            ct);

            if (payload is null) {
                return new AllFenixFailuresResponseDto();
            }

            return FenixMappers.FenixJsonFailuresToDto(payload);
        } catch (JsonException ex) {
            _logger.LogError(ex, "La respuesta de fallas de Fenix no contiene JSON válido.");
            throw;
        }

    }


}
