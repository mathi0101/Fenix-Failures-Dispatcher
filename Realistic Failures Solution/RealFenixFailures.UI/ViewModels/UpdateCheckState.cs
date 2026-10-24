namespace RealFenixFailures.UI.ViewModels;

/// <summary>Estados del chequeo manual de actualizaciones (pestaña Actualizaciones).</summary>
public enum UpdateCheckState {
    /// <summary>Sin chequeo en curso ni resultado (estado inicial).</summary>
    Idle,
    /// <summary>Consultando GitHub Releases.</summary>
    Checking,
    /// <summary>La versión instalada es la más reciente publicada.</summary>
    UpToDate,
    /// <summary>Existe una release más nueva que la instalada.</summary>
    NewVersionAvailable,
    /// <summary>El repo aún no tiene releases publicadas.</summary>
    NoReleases,
    /// <summary>No se pudo completar el chequeo (conexión, timeout, etc.).</summary>
    Error
}
