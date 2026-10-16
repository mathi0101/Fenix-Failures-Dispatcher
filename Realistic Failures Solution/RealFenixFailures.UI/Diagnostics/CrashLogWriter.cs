using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace RealFenixFailures.UI.Diagnostics;

/// <summary>
/// Escribe a disco, en <c>logs/crash</c>, un registro de crash fatal con toda
/// la información relevante del proceso y el trace completo de la excepción
/// (incluidas excepciones internas anidadas y agregadas).
/// </summary>
public static class CrashLogWriter {
    private const string LogsFolder = "logs";
    private const string CrashSubfolder = "crash";

    /// <summary>
    /// Arma el texto completo del reporte de crash para una excepción.
    /// </summary>
    public static string BuildReport(Exception exception, string context) {
        var sb = new StringBuilder(4096);
        sb.AppendLine("==============================================================");
        sb.AppendLine(" REGISTRO DE CRASH — Real Fenix Failures");
        sb.AppendLine("==============================================================");
        sb.AppendLine($"Fecha (local)     : {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        sb.AppendLine($"Fecha (UTC)       : {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}");
        sb.AppendLine($"Contexto          : {context}");
        sb.AppendLine($"Versión app       : {GetAppVersion()}");
        sb.AppendLine($"Runtime .NET      : {RuntimeInformation.FrameworkDescription}");
        sb.AppendLine($"Sistema operativo : {RuntimeInformation.OSDescription} (x64: {Environment.Is64BitOperatingSystem})");
        sb.AppendLine($"Máquina           : {Environment.MachineName}");
        sb.AppendLine($"Usuario           : {Environment.UserName}");
        sb.AppendLine($"Proceso (PID)     : {Environment.ProcessId}");
        sb.AppendLine($"Proceso (ruta)    : {Environment.ProcessPath ?? "(no disponible)"}");
        sb.AppendLine($"Directorio base   : {AppContext.BaseDirectory}");
        sb.AppendLine($"Directorio actual : {Environment.CurrentDirectory}");
        sb.AppendLine($"Procesadores      : {Environment.ProcessorCount}");
        sb.AppendLine("--------------------------------------------------------------");
        AppendException(sb, exception, depth: 0);
        return sb.ToString();
    }

    /// <summary>
    /// Escribe el registro de crash en <c>logs/crash/crash-&lt;timestamp&gt;.log</c>
    /// y devuelve la ruta del archivo, o null si no se pudo escribir. Nunca lanza:
    /// un fallo del propio registro no debe enmascarar el manejo del error original.
    /// </summary>
    public static string? Write(Exception exception, string context) {
        try {
            var crashDirectory = Path.Combine(AppContext.BaseDirectory, LogsFolder, CrashSubfolder);
            Directory.CreateDirectory(crashDirectory);
            var path = Path.Combine(crashDirectory, $"crash-{DateTime.Now:yyyyMMdd-HHmmss-fff}.log");
            File.WriteAllText(path, BuildReport(exception, context), Encoding.UTF8);
            return path;
        }
        catch {
            return null;
        }
    }

    private static string GetAppVersion() {
        try {
            var assembly = Assembly.GetEntryAssembly();
            if (assembly is null)
                return "(no disponible)";

            var informational = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            return string.IsNullOrEmpty(informational)
                ? assembly.GetName().Version?.ToString() ?? "(no disponible)"
                : informational;
        }
        catch {
            return "(no disponible)";
        }
    }

    private static void AppendException(StringBuilder sb, Exception? exception, int depth) {
        if (exception is null)
            return;

        var indent = new string(' ', depth * 2);
        sb.AppendLine($"{indent}EXCEPCIÓN (nivel {depth})");
        sb.AppendLine($"{indent}  Tipo    : {exception.GetType().FullName}");
        sb.AppendLine($"{indent}  Mensaje : {exception.Message}");
        if (!string.IsNullOrEmpty(exception.Source))
            sb.AppendLine($"{indent}  Fuente  : {exception.Source}");
        if (exception.Data.Count > 0) {
            sb.AppendLine($"{indent}  Datos   :");
            foreach (var key in exception.Data.Keys)
                sb.AppendLine($"{indent}    {key} = {exception.Data[key]}");
        }
        sb.AppendLine($"{indent}  Stack trace:");
        sb.AppendLine($"{indent}{(string.IsNullOrEmpty(exception.StackTrace) ? "(sin stack trace)" : exception.StackTrace)}");
        sb.AppendLine();

        if (exception is AggregateException aggregate) {
            foreach (var inner in aggregate.InnerExceptions)
                AppendException(sb, inner, depth + 1);
        }
        else if (exception.InnerException is not null) {
            AppendException(sb, exception.InnerException, depth + 1);
        }
    }
}
