using Microsoft.Extensions.Logging;
using RealFenixFailures.Domain.Services;
using RealFenixFailures.Integrations.Fenix.Interfaces;
using RealFenixFailures.Integrations.Fenix.Models;
using System.Text.Json;

namespace RealFenixFailures.Integrations.Fenix.Services;

/// <summary>
/// Mock v2 de la API de Fenix: sirve el MISMO catálogo de fallas que usa el motor
/// (<c>RealFenixFailures.Domain/Resources/failures.json</c>, embebido en Domain), con estado
/// mutable de "failed" persistente entre lecturas. Así las inyecciones del motor con IDs reales
/// del catálogo se reflejan en las lecturas del estado, igual que con la API real.
/// </summary>
public class MockFenixApiClient : IFenixApiClient {
    private static readonly JsonSerializerOptions CatalogOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed record CatalogDocument(IReadOnlyList<CatalogAta> Atas);
    private sealed record CatalogAta(int Id, string Title, string ShortTitle, IReadOnlyList<CatalogGroup> Groups);
    private sealed record CatalogGroup(string GroupName, IReadOnlyList<CatalogFailure> Failures);
    private sealed record CatalogFailure(string Id, string Title, int Severity, bool Repairable, bool Failed);

    private readonly ILogger<MockFenixApiClient> _logger;
    private readonly CatalogDocument _catalog;
    private readonly Dictionary<string, bool> _failureState = new();
    private readonly ReaderWriterLockSlim _lock = new();

    public MockFenixApiClient(ILogger<MockFenixApiClient> logger) {
        _logger = logger;
        _catalog = LoadCatalog();
        foreach (var ata in _catalog.Atas) {
            foreach (var group in ata.Groups) {
                foreach (var failure in group.Failures) {
                    _failureState[failure.Id] = failure.Failed;
                }
            }
        }

        _logger.LogInformation("MockFenixApiClient v2 inicializado con {Count} fallas del catálogo real (failures.json)", _failureState.Count);
    }

    private static CatalogDocument LoadCatalog() {
        try {
            using var stream = EmbeddedJsonLoader.LoadFromEmbeddedJson("failures.json")
                ?? throw new FileNotFoundException("Recurso embebido failures.json no encontrado");
            return JsonSerializer.Deserialize<CatalogDocument>(stream, CatalogOptions)
                ?? throw new InvalidOperationException("failures.json deserializó a null");
        } catch (Exception ex) {
            throw new InvalidOperationException("MockFenixApiClient: no se pudo cargar el catálogo embebido failures.json", ex);
        }
    }

    public async Task<bool> IsApiAlive(CancellationToken ct) {
        await Task.Delay(10, ct);
        _logger.LogDebug("MockFenixApiClient: IsApiAlive returning true");
        return true;
    }

    public async Task<Stream?> GetManualFailuresAsync(CancellationToken ct) {
        _lock.EnterReadLock();
        try {
            var blocks = _catalog.Atas.Select(ata => new FenixAtaBlock(
                Id: ata.Id,
                Title: ata.Title,
                ShortTitle: ata.ShortTitle,
                Groups: ata.Groups.Select(group => new FenixFailureGroup(
                    GroupName: group.GroupName,
                    Failures: group.Failures.Select(failure => new FenixManualFailure(
                        Id: failure.Id,
                        Title: failure.Title,
                        FailureCondition: null,
                        Failed: _failureState.GetValueOrDefault(failure.Id, failure.Failed),
                        Severity: failure.Severity,
                        Repairable: failure.Repairable
                    )).ToList()
                )).ToList()
            )).ToList();

            var response = new FenixManualFailuresResponse(blocks);
            var stream = new MemoryStream();
            await JsonSerializer.SerializeAsync(stream, response, cancellationToken: ct);
            stream.Position = 0;

            _logger.LogDebug("MockFenixApiClient: Returning failures stream with {Count} active failures",
                _failureState.Count(f => f.Value));

            return stream;
        } finally {
            _lock.ExitReadLock();
        }
    }

    public async Task<Stream?> SendFailureAsync(FenixSaveManualRequest rq, CancellationToken ct) {
        _lock.EnterWriteLock();
        try {
            if (!_failureState.ContainsKey(rq.Id)) {
                _logger.LogWarning("MockFenixApiClient: failure {FailureId} no está en el catálogo; se registra igual", rq.Id);
            }

            _failureState[rq.Id] = rq.Failed;
            _logger.LogInformation("MockFenixApiClient: Set failure {FailureId} to {State}",
                rq.Id, rq.Failed ? "ACTIVE" : "INACTIVE");

            var stream = new MemoryStream();
            await JsonSerializer.SerializeAsync(stream, new { success = true }, cancellationToken: ct);
            stream.Position = 0;

            return stream;
        } finally {
            _lock.ExitWriteLock();
        }
    }
}
