using RealFenixFailures.Application.DTOs;
using RealFenixFailures.UI.ViewModels.Base;

namespace RealFenixFailures.UI.ViewModels.Realistic;

/// <summary>Tarjeta seleccionable de una aeronave en el Paso 1.</summary>
public sealed class UserAircraftItemViewModel : ObservableObject {
    private bool _isSelected;

    public UserAircraftItemViewModel(UserAircraftDto dto) {
        Id = dto.Id;
        Registration = dto.Registration;
        IcaoTypeCode = dto.IcaoTypeCode;
        TotalFlightHours = dto.TotalFlightHours;
        TotalFlights = dto.TotalFlights;
        CreatedAt = dto.CreatedAt;
        WearValues = dto.SystemsWear.Select(x => x.WearPercentage).ToArray();
        HasOpenSession = dto.FlightSessions.Any(s => s.FinishedAt is null);
    }

    public int Id { get; }
    public string Registration { get; }
    public string IcaoTypeCode { get; }
    public double TotalFlightHours { get; }
    public int TotalFlights { get; }
    public DateTime CreatedAt { get; }
    public double[] WearValues { get; }
    public bool HasOpenSession { get; }

    public string HoursDisplay => $"{TotalFlightHours:F1} h";
    public string FlightsDisplay => $"{TotalFlights} vuelos";

    public bool HasWearData => WearValues.Length > 0;

    public int AverageWear => WearValues.Length == 0 ? 0 : (int)Math.Round(WearValues.Average());

    public string WearAverageDisplay => HasWearData ? $"{AverageWear}%" : "Sin datos";

    public bool IsSelected {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
