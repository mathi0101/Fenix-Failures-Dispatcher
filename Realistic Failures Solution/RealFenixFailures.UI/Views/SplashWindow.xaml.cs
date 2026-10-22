using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace RealFenixFailures.UI.Views;

/// <summary>
/// Splash de arranque: muestra la pieza gráfica original de la marca (PNG) y
/// anima su barra de progreso intercambiando en vivo 6 frames con la barra en
/// posiciones de avance (splash-frame-0.png vacía + splash-frame-1..5.png,
/// generados con la misma estética). La barra va de 0 a
/// full y se detiene en el último frame; NO está conectada al progreso real del
/// arranque (si la app termina antes, se corta donde iba; si tarda más, queda
/// llena). Los PNG están embebidos como recursos del ensamblado
/// (EmbeddedResource en el .csproj) y se cargan por
/// Assembly.GetManifestResourceStream porque la ventana se muestra antes de
/// crear la Application y no aplican los pack URIs de Application.
/// </summary>
public partial class SplashWindow : Window {
    private const string ResourcePrefix = "RealFenixFailures.UI.Assets.Branding.";

    private static readonly string[] FrameFiles = {
        "splash-frame-0.png",
        "splash-frame-1.png",
        "splash-frame-2.png",
        "splash-frame-3.png",
        "splash-frame-4.png",
        "splash-frame-5.png",
    };

    private const int FrameIntervalMs = 350;

    private readonly System.Collections.Generic.List<BitmapImage> _frames = new();
    private readonly DispatcherTimer _timer;
    private int _frameIndex;

    public SplashWindow() {
        InitializeComponent();

        var assembly = typeof(SplashWindow).Assembly;
        foreach (var file in FrameFiles) {
            using var stream = assembly.GetManifestResourceStream(ResourcePrefix + file)
                ?? throw new FileNotFoundException("No se encontró el frame embebido del splash: " + ResourcePrefix + file);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            _frames.Add(bitmap);
        }

        SplashImage.Source = _frames[0];

        _timer = new DispatcherTimer { Interval = System.TimeSpan.FromMilliseconds(FrameIntervalMs) };
        _timer.Tick += OnTick;
        _timer.Start();
        Closed += (_, _) => _timer.Stop();
    }

    private void OnTick(object? sender, System.EventArgs e) {
        // La barra avanza 0 -> full y se queda en el último frame hasta que se
        // cierre la ventana (no reinicia ni retrocede).
        if (_frameIndex >= _frames.Count - 1)
            return;

        _frameIndex++;
        SplashImage.Source = _frames[_frameIndex];
    }
}
