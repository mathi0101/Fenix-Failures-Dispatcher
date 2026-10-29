using Microsoft.Extensions.Logging;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Interfaces.Repositories;

namespace RealFenixFailures.Application.Services;

// Historial de versiones de la app en la base: registra una fila por transición de
// versión (primera ejecución, upgrade o downgrade). Corre PRIMERO en el pipeline de
// IInitializerService y queda como detector para la futura recarga de catálogos
// embebidos (failures.json / training_presets.json): última versión registrada !=
// versión actual → los datos semilla derivados del catálogo deben refrescarse.
public class AppVersionTracker : IInitializerService {
    private readonly IAppVersionHistoryRepository _repository;
    private readonly IAppVersionProvider _versionProvider;
    private readonly ILogger<AppVersionTracker> _logger;

    public AppVersionTracker(IAppVersionHistoryRepository repository, IAppVersionProvider versionProvider, ILogger<AppVersionTracker> logger) {
        _repository = repository;
        _versionProvider = versionProvider;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct) {
        var currentVersion = _versionProvider.CurrentVersion;
        var lastRecorded = await _repository.GetLatestVersionAsync(ct);
        if (lastRecorded == currentVersion)
            return;

        await _repository.AddAsync(new AppVersionHistory {
            AppVersion = currentVersion,
            DetectedAtUtc = DateTime.UtcNow
        }, ct);

        if (lastRecorded is null)
            _logger.LogInformation("Historial de versiones iniciado en la base: {Version}.", currentVersion);
        else
            _logger.LogInformation("Cambio de versión detectado: {Previous} -> {Current}.", lastRecorded, currentVersion);
    }
}
