using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Infrastructure.Persistence;
using RealFenixFailures.UI.DependencyInjection;
using RealFenixFailures.UI.Diagnostics;
using RealFenixFailures.UI.Services;
using RealFenixFailures.UI.Views;

namespace RealFenixFailures.UI;

public partial class App : System.Windows.Application {
    private readonly IHost _host;
    private readonly SingleInstanceGuard _singleInstanceGuard = new();
    private TrayIconService? _trayIconService;

    public App() {
        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(configure => {
                // Resolver desde el ejecutable, incluso al abrir desde un acceso directo.
                // Release nunca carga la configuración de desarrollo.
                configure.Sources.Clear();
                configure.SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
#if DEBUG
                configure.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
#endif
                configure.AddEnvironmentVariables();
            })
            .ConfigureRealFenixFailures()
            .Build();
    }

    protected override async void OnStartup(System.Windows.StartupEventArgs e) {
        if (Program.StartupFailed)
            return;

        CrashReporter.AttachGlobalHandlers(this);

        if (!_singleInstanceGuard.TryAcquire()) {
            _singleInstanceGuard.SignalExistingInstance();
            Program.CloseSplash();
            Shutdown();
            return;
        }

        try {
            await _host.StartAsync();

            using var scope = _host.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<ILogger<App>>()
                .LogInformation("Real Fenix Failures v{Version} iniciando (SO: {OS}).",
                    AppVersionInfo.Current, Environment.OSVersion.VersionString);
            var dbContext = scope.ServiceProvider.GetRequiredService<RealFenixDbContext>();
            await dbContext.Database.MigrateAsync();

            var initializers = scope.ServiceProvider.GetServices<IInitializerService>();
            foreach (var initializer in initializers)
                await initializer.InitializeAsync(CancellationToken.None);
        }
        catch (Exception startupException) {
            Program.CloseSplash();
            CrashReporter.ReportFatal(
                startupException,
                "La aplicación no pudo iniciarse (error al inicializar servicios, conexión o base de datos). Se generó un registro de crash.");
            return;
        }

        try {
            var newWindow = _host.Services.GetRequiredService<MainWindow>();
            _trayIconService = new TrayIconService(newWindow, RequestExit);
            newWindow.TrayIcon = _trayIconService;
            _singleInstanceGuard.StartListening(() => _trayIconService.RestoreMainWindow());
            newWindow.Show();
            Program.CloseSplash();
        }
        catch (Exception startupException) {
            Program.CloseSplash();
            CrashReporter.ReportFatal(
                startupException,
                "La aplicación no pudo iniciarse (error al crear la ventana principal). Se generó un registro de crash.");
            return;
        }

        base.OnStartup(e);
    }

    private void RequestExit() {
        foreach (System.Windows.Window window in Windows) {
            if (window is MainWindow mainWindow) {
                mainWindow.TryRequestFullExit();
                return;
            }
        }

        Shutdown();
    }

    protected override async void OnExit(System.Windows.ExitEventArgs e) {
        _trayIconService?.Dispose();
        _singleInstanceGuard.Dispose();
        // El constructor del host pudo fallar antes de asignar _host. El reporte fatal
        // también cierra esa Application parcial, sin provocar otro error durante OnExit.
        if (Program.StartupFailed) {
            _host?.Dispose();
            base.OnExit(e);
            return;
        }
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}
