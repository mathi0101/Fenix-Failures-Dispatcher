using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Domain.DTOs;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using System.ComponentModel;

namespace RealFenixFailures.Application.Session;

public class RealisticSession : INotifyPropertyChanged {
    #region Fields

    private readonly FlightSession _session;
    private readonly UserAircraftDto _aircraft;
    private readonly RiskLevel riskLevel;

    private SimulatorAircraftStateSnapshot lastSimData = new();
    private List<FenixFailureDto> allFenixFailures = [];
    private List<FenixFailureDto> failedFailures = [];
    private List<FenixFailureDto> armedFailures = [];

    private ComplexFlightPhaseEnum currentflightPhase = ComplexFlightPhaseEnum.Unknown;
    #endregion

    #region INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName) {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    #endregion

    #region Constructor
    public RealisticSession(FlightSession session, UserAircraftDto aircraft) {
        _session = session;
        _aircraft = aircraft;
        riskLevel = session.RiskLevel;
    }
    #endregion

    #region Properties
    public FlightSession Session => _session;
    public UserAircraftDto Aircraft => _aircraft;
    public RiskLevel RiskLevel => riskLevel;

    /// <summary>UTC timestamp of the last failure triggered in this session, used to enforce cooldown.</summary>
    public DateTime? LastTriggeredAtUtc { get; private set; }

    /// <summary>Number of failures currently armed or already failed in Fenix, used to enforce the simultaneous-failure limit.</summary>
    public int ActiveFailureCount => ArmedFailures.Count + FailedFailures.Count;

    /// <summary>Fenix failure ids that are currently armed or failed, so the engine does not re-arm them.</summary>
    public IReadOnlyCollection<string> ActiveFenixFailureIds =>
        AllFenixFailures.Where(f => f.IsArmed || f.Failed).Select(f => f.FenixId).ToList();

    public ComplexFlightPhaseEnum CurrentFlightPhase {
        get => currentflightPhase;
        set {
            if (currentflightPhase != value) {
                currentflightPhase = value;
                OnPropertyChanged(nameof(CurrentFlightPhase));
            }
        }
    }
    public SimulatorAircraftStateSnapshot LastSimData {
        get => lastSimData;
        private set {
            lastSimData = value;
            OnPropertyChanged(nameof(LastSimData));
        }
    }

    public List<FailurePreset> AvailablePresets { get; internal init; } = [];
    public List<PresetFailureDefinition> ExecutedFailures { get; private set; } = [];
    public List<FailurePreset> ExecutedPresets => ExecutedFailures.Where(x => x.Preset != null).Select(f => f.Preset!).ToList();

    #region Fenix

    public List<FenixFailureDto> AllFenixFailures {
        get => allFenixFailures;
        private set {
            allFenixFailures = value;
            ArmedFailures = allFenixFailures.Where(f => !f.Failed && f.FailureCondition != null).ToList();
            FailedFailures = allFenixFailures.Where(f => f.Failed).ToList();
        }
    }
    public List<FenixFailureDto> ArmedFailures {
        get => armedFailures;
        private set {
            if (armedFailures.Count != value.Count) {
                armedFailures = value;
                OnPropertyChanged(nameof(ArmedFailures));
            } else if (armedFailures.Any(f => !value.Contains(f))) {
                armedFailures = value;
                OnPropertyChanged(nameof(ArmedFailures));
            }
        }
    }
    public List<FenixFailureDto> FailedFailures {
        get => failedFailures;
        private set {
            if (failedFailures.Count != value.Count) {
                failedFailures = value;
                OnPropertyChanged(nameof(FailedFailures));
            } else if (failedFailures.Any(f => !value.Contains(f))) {
                failedFailures = value;
                OnPropertyChanged(nameof(FailedFailures));
            }
        }
    }

    #endregion

    #endregion


    #region Public

    public void ProcessSimData(SimulatorAircraftStateSnapshot rawData, IReadOnlyList<FenixFailureDto> failures, ComplexFlightPhaseEnum currentPhase) {
        AllFenixFailures = failures.ToList();
        CurrentFlightPhase = currentPhase;
        LastSimData = rawData;
    }

    public void RecordTriggeredFailure(DateTime timestampUtc) {
        LastTriggeredAtUtc = timestampUtc;
        OnPropertyChanged(nameof(LastTriggeredAtUtc));
    }

    #endregion

}
