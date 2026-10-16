using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
                configure.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
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
            var dbContext = scope.ServiceProvider.GetRequiredService<RealFenixDbContext>();
            await dbContext.Database.MigrateAsync();

            var initializer = scope.ServiceProvider.GetRequiredService<IInitializerService>();
            await initializer.InitializeAsync(CancellationToken.None);
        }
        catch (Exception startupException) {
            Program.CloseSplash();
            CrashReporter.ReportFatal(
                startupException,
                "La aplicación no pudo iniciarse (error al inicializar servicios, conexión o base de datos). Se generó un registro de crash.",
                shutdown: true);
            return;
        }

        try {
            var newWindow = _host.Services.GetRequiredService<MainWindow>();
            _trayIconService = new TrayIconService(newWindow, RequestExit);
            _singleInstanceGuard.StartListening(() => _trayIconService.RestoreMainWindow());
            newWindow.Show();
        }
        catch (Exception startupException) {
            Program.CloseSplash();
            CrashReporter.ReportFatal(
                startupException,
                "La aplicación no pudo iniciarse (error al crear la ventana principal). Se generó un registro de crash.",
                shutdown: true);
            return;
        }

        base.OnStartup(e);
    }

    private void RequestExit() {
        foreach (System.Windows.Window window in Windows) {
            if (window is MainWindow mainWindow)
                mainWindow.IsExitAllowed = true;
        }

        Shutdown();
    }

    protected override async void OnExit(System.Windows.ExitEventArgs e) {
        _trayIconService?.Dispose();
        _singleInstanceGuard.Dispose();
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}
