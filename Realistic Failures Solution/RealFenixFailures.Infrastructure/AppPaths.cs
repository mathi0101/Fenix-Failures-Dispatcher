using System.IO;

namespace RealFenixFailures.Infrastructure;

/// <summary>
/// Ubicación única de los DATOS del usuario en %AppData%\RealFenixFailures
/// (BD SQLite, preferencias, logs). La carpeta del ejecutable es solo "programa":
/// los datos sobreviven a reinstalaciones, funcionan si la app vive en una
/// carpeta protegida (Program Files) o sincronizada (OneDrive), y se pueden
/// borrar/usurpar sin afectar la instalación.
/// La carpeta base ya la usaba UserPreferencesService (userprefs.json); este
/// centralizador reúne ahí también la BD y los logs.
/// </summary>
public static class AppPaths {

    public const string AppFolderName = "RealFenixFailures";

    public static string AppDataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppFolderName);

    /// <summary>Ruta del archivo SQLite de la aplicación.</summary>
    public static string DatabaseFile => Path.Combine(AppDataDirectory, "realfenixfailures.db");

    /// <summary>Connection string de SQLite apuntando a <see cref="DatabaseFile"/>.</summary>
    public static string DatabaseConnectionString => $"Data Source={DatabaseFile}";

    /// <summary>Carpeta de los logs rodantes (Serilog).</summary>
    public static string LogsDirectory => Path.Combine(AppDataDirectory, "logs");

    /// <summary>Garantiza que existan las carpetas de datos (idempotente).</summary>
    public static void EnsureCreated() {
        Directory.CreateDirectory(AppDataDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }
}
