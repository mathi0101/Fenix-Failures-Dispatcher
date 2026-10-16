using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Domain.DTOs;
using RealFenixFailures.Domain.Interfaces;
using RealFenixFailures.Integrations.Fenix.Interfaces;
using RealFenixFailures.Integrations.Fenix.Mappers;
using RealFenixFailures.Integrations.Fenix.Models;

namespace RealFenixFailures.Integrations.Fenix.Services;

public class FenixApiFailureService : IFenixFailureApiDispatcher {
    private readonly IFenixApiClient _apiClient;
    private readonly IFenixStreamFailuresReaderService _jsonReader;
    private readonly IOptionsMonitor<FenixApiOptions> _optionsMonitor;
    private readonly ILogger<FenixApiFailureService> _logger;
    private readonly SemaphoreSlim _healthLock = new(1, 1);
    private readonly object _stateLock = new();

    private DateTimeOffset _lastHealthCheckAtUtc = DateTimeOffset.MinValue;
    private bool _lastHealthCheckResult;
    private bool _hasEverConnected;

    public FenixApiFailureService(IFenixApiClient apiClient, IFenixStreamFailuresReaderService jsonReader, IOptionsMonitor<FenixApiOptions> optionsMonitor, ILogger<FenixApiFailureService> logger) {
        _apiClient = apiClient;
        _jsonReader = jsonReader;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }


    #region Minimal

    public async Task<bool> ArmFailureAsync(FenixArmFailureRequest def, CancellationToken ct) {
        _logger.LogDebug("ArmFailure Request: {@def}", def);
        FenixSaveManualRequest rq = def.ToFenixSaveManualRequest();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_optionsMonitor.CurrentValue.HealthCheckTimeout));
        using var stream = await _apiClient.SendFailureAsync(rq, cts.Token);
        _logger.LogDebug("ArmFailure Response: {@stream}", stream);
        bool httpOk = stream is not null;
        UpdateHealthState(httpOk);
        return httpOk;

    }

    public async Task<bool> DisarmFailureAsync(FenixArmFailureRequest def, CancellationToken ct) {
        _logger.LogDebug("DisarmFailure Request: {@def}", def);
        FenixSaveManualRequest rq = def.ToFenixSaveManualRequest();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_optionsMonitor.CurrentValue.HealthCheckTimeout));
        using var stream = await _apiClient.SendFailureAsync(rq, cts.Token);
        _logger.LogDebug("DisarmFailure Response: {@stream}", stream);
        bool httpOk = stream is not null;
        UpdateHealthState(httpOk);
        return httpOk;
    }

    #endregion

    public async Task<AllFenixFailuresResponseDto> GetAllFailuresAsync(CancellationToken ct) {
        try {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_optionsMonitor.CurrentValue.HealthCheckTimeout));
            using (var stream = await _apiClient.GetManualFailuresAsync(cts.Token)) {
                if (stream is null) {
                    UpdateHealthState(false);
                    return new AllFenixFailuresResponseDto();
                }
                var result = await _jsonReader.ReadAsync(stream, ct);

                UpdateHealthState(result.MajorGroups.Count > 0);
                return result;
            }
        } catch (Exception) {
            throw;
        }

    }


    public async Task<bool> ResetAllFailuresAsync(CancellationToken ct) {
        var failures = await GetAllFailuresAsync(ct);
        var activeFailures = failures.GetFailuresList().Where(f => f.Failed || f.FailureCondition is not null).ToList();
        if (activeFailures.Count > 0) {
            _logger.LogInformation("Resetting {Count} active Fenix failures.", activeFailures.Count);
            foreach (var activeFailure in activeFailures) {
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_optionsMonitor.CurrentValue.HealthCheckTimeout));
                await _apiClient.SendFailureAsync(new FenixSaveManualRequest(activeFailure.FenixId, false, null), cts.Token);
            }
        }


        failures = await GetAllFailuresAsync(ct);
        return failures.GetFailuresList().All(f => !f.Failed);
    }

    public async Task<bool> IsApiAvailableAsync(CancellationToken ct) {
        var intervalSeconds = Math.Max(1, _optionsMonitor.CurrentValue.HealthCheckIntervalSeconds);
        var now = DateTimeOffset.UtcNow;

        if (now - _lastHealthCheckAtUtc < TimeSpan.FromSeconds(intervalSeconds)) {
            return _lastHealthCheckResult;
        }

        await _healthLock.WaitAsync(ct);
        try {
            now = DateTimeOffset.UtcNow;
            if (now - _lastHealthCheckAtUtc < TimeSpan.FromSeconds(intervalSeconds)) {
                return _lastHealthCheckResult;
            }
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_optionsMonitor.CurrentValue.HealthCheckTimeout));
            var isAlive = await _apiClient.IsApiAlive(cts.Token);
            UpdateHealthState(isAlive);
            return _lastHealthCheckResult;
        } finally {
            _healthLock.Release();
        }
    }

    private void UpdateHealthState(bool isAvailable) {
        lock (_stateLock) {
            var previous = _lastHealthCheckResult;
            _lastHealthCheckResult = isAvailable;
            _lastHealthCheckAtUtc = DateTimeOffset.UtcNow;

            if (isAvailable == previous) return;

            if (isAvailable) {
                _hasEverConnected = true;
                _logger.LogInformation("API Fenix conectada");
            } else if (_hasEverConnected) {
                _logger.LogInformation("API Fenix desconectada");
            }
        }
    }


}
