using Microsoft.Extensions.Logging;
using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Application.Session;
using RealFenixFailures.Domain.Interfaces;
using System.ComponentModel;

namespace RealFenixFailures.Application.Services;

/// <summary>
/// Owns the realistic-session lifecycle: start/stop/pause/resume, the periodic evaluation timer,
/// and event dispatch. All failure-evaluation logic is delegated to a <see cref="RealisticSessionEngine"/>
/// instance that is created when a session starts and discarded when it stops.
/// </summary>
public class RealisticSessionManager : IRealisticSessionManager {
    #region Fields
    private readonly ILogger<RealisticSessionManager> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ISimulatorConnectionService _simulator;
    private readonly IPresetService _presetService;
    private readonly IFlightSessionService _sessionService;
    private readonly IFailureTrigger _failureTrigger;
    private readonly IFlightEventDetector _flightEventDetector;

    private RealisticSessionEngine? _engine;
    private PeriodicTimer? _evaluationTimer;
    private CancellationTokenSource? _evaluationCts;
    private const int EvaluationIntervalSeconds = 30;
    #endregion

    #region Events
    public event EventHandler<FailureTriggeredEventArgs>? FailureTriggered;
    public event EventHandler<SessionErrorEventArgs>? SessionError;
    #region INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName) {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
    #endregion

    #region Properties
    public RealisticSession? SessionState => _engine?.Session;
    #endregion

    #region Constructor
    public RealisticSessionManager(
        ILogger<RealisticSessionManager> logger,
        ILoggerFactory loggerFactory,
        ISimulatorConnectionService simulatorConnectionService,
        IPresetService presetService,
        IFlightSessionService sessionService,
        IFailureTrigger failureTrigger,
        IFlightEventDetector flightEventDetector) {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _simulator = simulatorConnectionService;
        _presetService = presetService;
        _sessionService = sessionService;
        _failureTrigger = failureTrigger;
        _flightEventDetector = flightEventDetector;
    }
    #endregion

    #region Session Commands
    public async Task<ServiceResult<string>> StartNewSessionAsync(RealisticModeContext context, CancellationToken ct) {
        if (SessionState != null) throw new InvalidOperationException();
        try {
            // Create the evaluation engine for this session. It owns all evaluation logic and mutates
            // the shared RealisticSessionState instance directly.
            _engine = new RealisticSessionEngine(
                _simulator,
                _loggerFactory.CreateLogger<RealisticSessionEngine>(),
                _sessionService,
                _presetService,
                _failureTrigger,
                _flightEventDetector);

            var result = await _engine.TryStartSession(context, ct);
            if (result.Started == false) {
                return ServiceResult<string>.Fail(new InvalidOperationException(), result.Text);
            }
            _evaluationCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _evaluationTimer = new PeriodicTimer(TimeSpan.FromSeconds(EvaluationIntervalSeconds));
            _ = Task.Run(() => RunEvaluationLoopAsync(_evaluationCts.Token), _evaluationCts.Token);
            _logger.LogInformation("Realistic session started for aircraft {Registration}", context.Aircraft.Registration);
            return ServiceResult<string>.Ok($"Realistic session started for aircraft {context.Aircraft.Registration}");
        } catch (Exception ex) {
            _logger.LogError(ex, "Error starting realistic session");
            SessionError?.Invoke(this, new SessionErrorEventArgs { Message = "Failed to start realistic session", Exception = ex });
            return ServiceResult<string>.Fail(ex, "Failed to start realistic session");
        }
    }

    public async Task StopAsync(CancellationToken ct) {
        if (_engine == null) return;
        _evaluationCts?.Cancel();
        _evaluationTimer?.Dispose();
        _evaluationTimer = null;
        await _engine!.StopSession(ct);
        _engine = null;
        _logger.LogInformation("Realistic session stopped");
        return;
    }

    public Task PauseAsync(CancellationToken ct) {
        _evaluationCts?.Cancel();
        _logger.LogInformation("Realistic session paused");
        return Task.CompletedTask;
    }

    public async Task ResumeAsync(CancellationToken ct) {
        try {
            _evaluationCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _evaluationTimer = new PeriodicTimer(TimeSpan.FromSeconds(EvaluationIntervalSeconds));
            _ = Task.Run(() => RunEvaluationLoopAsync(_evaluationCts.Token), _evaluationCts.Token);
            _logger.LogInformation("Realistic session resumed");
        } catch (Exception ex) {
            _logger.LogError(ex, "Error resuming realistic session");
            SessionError?.Invoke(this, new SessionErrorEventArgs { Message = "Failed to resume session", Exception = ex });
        }
    }
    #endregion

    #region Loop Session Timer
    private async Task RunEvaluationLoopAsync(CancellationToken ct) {
        if (_evaluationTimer == null) return;
        try {
            while (await _evaluationTimer.WaitForNextTickAsync(ct) && !ct.IsCancellationRequested) {
                await EvaluateAndExecuteAsync(ct);
            }
        } catch (OperationCanceledException) {
            _logger.LogInformation("Evaluation loop canceled");
        } catch (Exception ex) {
            _logger.LogError(ex, "Unexpected error in evaluation loop");
        }
    }
    #endregion

    #region Main Timer Tick Session Evaluator
    private async Task EvaluateAndExecuteAsync(CancellationToken ct) {
        if (SessionState == null || _engine == null) return;
        try {
            var result = await _engine.EvaluateAsync(ct);

            if (result.ShouldPauseSession) {
                await PauseAsync(CancellationToken.None);
                return;
            }

            foreach (var triggered in result.TriggeredFailures) {
                FailureTriggered?.Invoke(this, new FailureTriggeredEventArgs {
                    FenixFailureId = triggered.FenixFailureId,
                    Description = triggered.Description,
                    TriggeredAtUtc = triggered.TriggeredAtUtc
                });
            }
        } catch (Exception ex) {
            _logger.LogError(ex, "Error during evaluation");
            SessionError?.Invoke(this, new SessionErrorEventArgs { Message = "Error during evaluation", Exception = ex });
        }
    }
    #endregion

    #region Privates

    #endregion
}
