using System.Reflection;

namespace RealFenixFailures.UI.Services;

/// <summary>
/// Versión de la app (InformationalVersion del ejecutable, p. ej. 0.3.0-beta.1).
/// Los builds deterministas pueden anexar "+sha"; se conserva solo el semver.
/// </summary>
public static class AppVersionInfo {
    public static string Current { get; } = GetDisplayVersion();

    private static string GetDisplayVersion() {
        try {
            var assembly = Assembly.GetEntryAssembly();
            var informational = assembly?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            if (!string.IsNullOrEmpty(informational)) {
                var plus = informational.IndexOf('+');
                return plus > 0 ? informational[..plus] : informational;
            }
            return assembly?.GetName().Version?.ToString(3) ?? "1.0.0";
        }
        catch {
            return "1.0.0";
        }
    }
}
