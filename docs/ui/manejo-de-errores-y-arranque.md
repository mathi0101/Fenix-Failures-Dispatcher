# UI: manejo global de errores y arranque con splash

Documento del comportamiento de `RealFenixFailures.UI` respecto a: (1) capturar errores inesperados controlados/no controlados, registrarlos en `%AppData%\RealFenixFailures\logs\crash` y mostrar el mismo diálogo de diagnóstico, y (2) mostrar un splash durante el arranque. La presentación vive en `Diagnostics/`; las demás capas siguen usando `ILogger` sin depender de WPF.

## Problema que resuelve

Antes de este cambio, una excepción no controlada (en el hilo de interfaz, en un hilo de fondo, o durante el arranque) terminaba el proceso en silencio **sin dejar rastro**: ni cartel, ni log. Con este cambio:

- Los errores inesperados quedan registrados en un archivo propio con la información del proceso y el **trace completo** (incluidas excepciones internas y `AggregateException`). Si el disco/permisos impiden escribir, se muestra el detalle igualmente y se avisa que no se pudo guardar.
- Todos usan el mismo cartel: **ERROR** permite continuar al aceptar; **FATAL** cierra la aplicación después de confirmar/cerrar el diálogo.
- Si la app intenta abrirse y no puede, pasa exactamente lo mismo (log + cartel) en lugar de morir sin explicación.
- Durante el arranque se muestra un splash sin bordes para que se vea que la app está cargando (y no "trancada").

## Handlers globales — `Diagnostics/CrashReporter.cs`

`AttachProcessHandlers()` se conecta antes del splash en `Program.Main`; `AttachGlobalHandlers(app)` agrega el handler del dispatcher al comenzar `App.OnStartup`. Ambos son idempotentes:

| Evento | Cuándo dispara | Comportamiento |
|---|---|---|
| `DispatcherUnhandledException` | Excepción no controlada en el hilo de interfaz (incluye handlers `async void`) | FATAL: log + cartel + `Shutdown(1)` al confirmar |
| `AppDomain.UnhandledException` | Excepción no controlada fatal en **cualquier** hilo | Log + cartel *best-effort* (se muestra en un hilo STA dedicado si hace falta). El proceso termina igual al retornar el handler (comportamiento del CLR) |
| `TaskScheduler.UnobservedTaskException` | Tarea fire-and-forget que falló sin observador | ERROR: se marca observada, genera reporte y muestra el cartel; permite continuar |

## Errores capturados en las capas — `UnexpectedErrorSink` (2026-10-24)

- Corrección de health checks (2026-10-24): las sondas de SimConnect/Fenix y los intentos fallidos de conexión de SimConnect registran únicamente `Debug` y devuelven indisponibilidad. No generan reportes ni modales; tampoco al habilitar Debug. Igual criterio para los rechazos HTTP y las lecturas/reset sin stream de Fenix. Para ver ese detalle en el log diario, configurar `Serilog:MinimumLevel:Default` como `Debug` en appsettings, o un override para la categoría de integración. Las transiciones de estado conservan Information. Los errores inesperados fuera de la sonda siguen usando el reporte común.

- El host registra un sink Serilog de UI junto a los sinks de archivo. Recibe excepciones de `Warning`/`Error` de categorías `RealFenixFailures.*` y las reporta como recuperables; cualquier `Fatal`/`LogCritical` se reporta como fatal, incluso sin excepción adjunta (se conserva el mensaje como diagnóstico).
- No se abren diálogos por warnings operativos sin excepción, rechazos normales de inicio, cancelaciones, HTTP/timeout/socket ni el COM `E_FAIL` de SimConnect ya reconocido como simulador no disponible. Otros COM inesperados sí se reportan. Los errores de frameworks no fatales se dejan al servicio que los captura, evitando reportes duplicados de EF. Un nivel Fatal siempre prevalece sobre esos filtros.
- Auditoría aplicada: se conservan las excepciones completas en `SimulatorConnectionService` y en el fatal de `EvaluateAsync`; compensaciones imposibles y terminación fatal del manager usan `LogCritical`. La muerte del timer automático es fatal porque deja de monitorear conexiones. JSON inválido de Fenix es Error recuperable (la lectura falla, no se declara fatal toda la aplicación). Los fallos antes silenciosos de persistencia de eventos/desgaste diferido ahora se loguean con excepción. El loop de requests y el cierre de SimConnect conservan errores inesperados; cancelaciones/desconexiones normales siguen filtradas.
- Para nuevas capturas: pasar **la excepción como primer argumento** a `LogWarning(ex, ...)`, `LogError(ex, ...)` o `LogCritical(ex, ...)`. Un mensaje con `ex.Message` no conserva stack trace ni activa el reporte recuperable. No usar Fatal si el flujo puede recuperarse.
- Versión de la app en el log diario (2026-10-25): el template default de Serilog incluye `v{AppVersion}` en cada línea (property enriquecida en `HostBuilderExtensions` desde `AppVersionInfo.Current`, la InformationalVersion recortada del `+sha`), y `App.OnStartup` registra la línea de inicio «Real Fenix Failures v{Version} iniciando (SO: …)». Así, cualquier tramo de log que reporte un usuario identifica la build. Un template custom en `Serilog:WriteTo:0:Args:outputTemplate` reemplaza ese formato.
- `CrashReportCoordinator` escribe el reporte y difiere la presentación hasta que termine el catch/finally del llamador. Serializa los diálogos, evita reentradas y deduplica la misma excepción base por referencia entre capas. Una escalada a Fatal se conserva; si llega con un diálogo recuperable abierto, después de aceptarlo se confirma el fatal antes de cerrar. Excepción terminal: AppDomain presenta sincrónicamente (con STA dedicado si hace falta), porque el CLR termina al retornar y no se puede depender de la cola del dispatcher; puede interrumpir un reporte recuperable abierto.
- `ReportFatal(ex, contexto)` siempre cierra después del diálogo; `ReportUnexpected(ex, contexto)` permite continuar por defecto. El cierre fatal habilita `MainWindow.IsExitAllowed`, evitando el modal normal de salida/bandeja.

### Sentido de `Program.StartupFailed`

Caso raro pero verificado en la práctica: si el constructor de `App` falla (p. ej. `appsettings.json` faltante), `System.Windows.Application.Current` ya quedó seteado por el ctor base y WPF puede llegar a invocar `OnStartup` desde el pump de mensajes del diálogo de error — con `_host` null. El flag `Program.StartupFailed` hace que `OnStartup` retorne de inmediato en ese caso y se evite un segundo reporte de crash espurio (con stack trace de `NullReferenceException` que confundía).

## Archivo de crash — `Diagnostics/CrashLogWriter.cs`

- Ruta: `%AppData%\RealFenixFailures\logs\crash\crash-<UTC>-<GUID>.log` para Fatal, `error-<UTC>-<GUID>.log` para recuperables. `AppPaths.LogsDirectory` es la misma carpeta padre de los logs diarios de Serilog; ya no se escribe junto al ejecutable. El GUID evita colisiones entre errores simultáneos.
- Contenido: fecha UTC, severidad, contexto del error, versión, runtime .NET, sistema operativo, máquina, usuario, PID, ruta del proceso, directorios base/actual, procesadores, y tipo/mensaje/fuente/datos/**stack trace completo por nivel** de la excepción.
- `Write()` **nunca lanza**: un fallo del propio registro (disco lleno, permisos) no debe enmascarar el manejo del error original; devuelve `null` y el cartel se muestra igual.

## Cartel de error — `Diagnostics/FatalErrorDialog.xaml(.cs)`

- Ventana **autocontenida** (colores/estilos inline, no depende de los recursos de la app) para poder mostrarse incluso sin un `Application` corriendo (crash previo al host WPF).
- Muestra mensaje, severidad visual (ámbar/rojo), ruta del archivo, botón **"Ver registro de crash"**, expander **"Detalle técnico"** seleccionable/copiable y acción **Aceptar** (recuperable) o **Cerrar aplicación** (fatal). Explica explícitamente si se puede continuar. Tiene scroll/límite de altura para pantallas pequeñas. Si abrir el archivo falla, se informa dentro del mismo diálogo, sin crear un segundo crash recursivo.
- `ShowGeneral()` detecta si la llamada viene del hilo de interfaz: si no (o si no hay `Application`), crea un hilo **STA dedicado** que muestra el diálogo y bloquea (Join) hasta que el usuario lo cierra — así el cartel aparece hasta en crashes de hilo de fondo, donde el proceso se termina justo después.

## Arranque con splash — `Program.cs` y `App.xaml.cs`

- **Entrada manual**: el `.csproj` define `<StartupObject>RealFenixFailures.UI.Program</StartupObject>`. `Program.Main` es `[STAThread]`, muestra el splash **antes** de crear la aplicación y envuelve `new App()` + `InitializeComponent()` + `Run()` en try/catch: un fallo ahí (config, recursos de App.xaml) va directo a log + cartel.
- **Splash**: el `SplashScreen` clásico de WPF se reemplazó por `Views/SplashWindow.xaml`, ventana custom sin bordes (560×340, `WindowStyle=None`, centrada, sin tarea) que muestra **la pieza gráfica original** (`Assets/Branding/splash-screen.png`). La barra de progreso se anima con la técnica de la imagen vieja: 6 frames generados con la misma estética (`splash-frame-0.png` con la barra **vacía**, `splash-frame-1..4.png` con el relleno en 52/68/84/100 %; el frame 0 es el PNG original con el 36 %) que se intercambian en vivo cada **350 ms** (`FrameIntervalMs` en `SplashWindow.xaml.cs`) en una secuencia **0 → full que se detiene en el último frame** (no retrocede ni reinicia). La barra **no está conectada al progreso real del arranque**: si la app termina antes, se corta donde iba; si tarda más, queda llena esperando el cierre. La animación corre en un **hilo STA dedicado** (`Program.StartSplash`, con su propio `Dispatcher`): durante el arranque el hilo principal está ocupado construyendo el host y un `DispatcherTimer` del hilo principal no llega a dispararse; con el pump propio la animación va siempre fluida. Se muestra antes de crear la `Application` (los frames están embebidos en el ensamblado como `EmbeddedResource` en el `.csproj` y se cargan por `Assembly.GetManifestResourceStream`; los pack URIs de `Application` no aplican aquí) y la cierra `App.OnStartup` con `Program.CloseSplash()` al renderizarse la ventana principal — **esa es la duración del splash: no hay tiempo fijo, se cierra cuando la app principal carga**. `Program.CloseSplash()` encola el cierre en el dispatcher del hilo del splash y termina su pump; es el cierre explícito para ramas fallidas y para la segunda instancia de la instancia única.
- **Cursor durante el arranque**: el `SplashScreen` clásico mostraba el cursor de "aplicación iniciando" (`IDC_APPSTARTING`) sobre el splash, lo que daba sensación de app trancada (y encima la barra era una imagen fija). La ventana custom lleva `Cursor="Arrow"` y no usa `SplashScreen`, así que el cursor se ve normal durante todo el arranque.
- **Entrega del foreground al mostrar la ventana principal (2026-10-28)**: el splash corre en otro hilo STA y mientras está abierto es la **ventana activa del proceso**. `App.OnStartup` muestra `MainWindow` con `Show()` y cierra el splash con `Program.CloseSplash()` (encolado en el hilo del splash): si nadie reafirma la activación, el cierre de la ventana activa cancela la activación pendiente del `Show()` y Windows le entrega el primer plano a la app que estaba detrás — la ventana principal queda visible pero detrás y sin foco (síntoma reportado y medido: nunca fue la ventana de foreground en 60 s). El fix es `newWindow.Activate()` entre `Show()` y `CloseSplash()`: mientras el splash sigue activo, el proceso conserva el derecho de Win32 a tomar el foreground, así que la principal queda activa antes de que el splash se destruya y el cierre ya no roba el foco. Limitación del sistema (correcta): si durante el arranque el usuario hace click en otra app, Windows niega el robo de foco y la ventana abre detrás; clickearla la trae al frente, y el restore desde bandeja ya hace `Activate()`.
- **`App.OnStartup`** quedó con tres zonas de manejo:
  1. `AttachGlobalHandlers` + guard de instancia única (segunda instancia → cierra splash y sale en silencio: comportamiento normal, **no** es un crash y no genera log).
  2. try/catch alrededor de `_host.StartAsync()` + `MigrateAsync` + `InitializeAsync` → "no pudo iniciarse (servicios, conexión o base de datos)".
  3. try/catch alrededor de la creación de `MainWindow` + tray → "no pudo iniciarse (ventana principal)".
  - Ambas ramas hacen `Program.CloseSplash()` + `CrashReporter.ReportFatal(…)` (cartel primero, cierre al confirmar) + `return`.

## Notas y decisiones

- **Splash por frames**: el splash original (el `SplashScreen` de WPF + PNG con la barra dibujada) no podía animar la barra porque era una imagen estática, y la primera iteración XAML con `ProgressBar` cambió la estética que al usuario no le gustó. El diseño final conserva **la imagen de siempre** como frame con el 36 % y agrega 5 frames idénticos: uno con la **barra vacía** (`splash-frame-0.png`, el frame inicial) y cuatro con la barra más avanzada (52/68/84/100 %), generados con un script con la misma paleta: se intercambian en vivo y el ojo percibe la barra llenándose de 0 a full. Detalle: los frames se versionan y van **embebidos en el ensamblado** (`EmbeddedResource`), no como archivos sueltos en la carpeta de publish; no se generan en runtime.
- El host lee appsettings.json desde el directorio del ejecutable (AppContext.BaseDirectory). Debug superpone appsettings.Development.json; Release no lo carga. Si falta el archivo base requerido, el arranque falla con cartel y log. Ver [configuración por compilación](../desarrollo.md#configuración-por-nombres-de-claves).
- El log de crash se acumula sin rotación (nombre con timestamp): conviene decidir algún día una política de retención.

## Crash al cerrar: Close() reentrante en MainWindow_Closing

Contexto: al pulsar la X (o Alt+F4), `MainWindow_Closing` hace `e.Cancel = true` y muestra el modal `CloseChoiceWindow` con `ShowDialog`. Si el usuario elige «Cerrar la app por completo», el handler llamaba a `Close()` **dentro del propio evento `Closing`**: la ventana todavía está procesando `WM_CLOSE` y el `Close()` reentrante lanza `InvalidOperationException` (*«Cannot set Visibility to Visible or call Show, ShowDialog, Close, or WindowInteropHelper.EnsureHandle while a Window is closing»*) en `MainWindow.xaml.cs:58`. Esa excepción llega a `DispatcherUnhandledException` del crash reporter, que cierra la app con cartel y log de crash (`crash-20261018-143622-468.log`).

Fix (2026-10-18): el `Close()` del `case Shutdown` se difiere hasta después de que el handler de `Closing` termina, con `Dispatcher.BeginInvoke(new Action(Close))`. `IsExitAllowed` ya quedó en `true`, así que el `Closing` reentrante disparado por el cierre diferido retorna sin cancelar y la ventana se cierra sin excepción. No se alteró el resto: cancelar, segundo plano (bandeja) ni la lógica del motor.

Verificación: harness WPF externo al repo (`RFCloseHarness`) que linkea `MainWindow.xaml.cs` real y recorre los tres caminos del modal con stubs; antes del fix reproduce la excepción exacta (rojo) y después del fix cierra sin excepción (verde). Detalle en `CHANGELOG.md` (2026-10-18).

## Pendiente / no verificado

- **Foreground del arranque (2026-10-28)**: verificado con harness de medición (lanzamiento instrumentado + `EnumWindows`/`GetForegroundWindow`): sin fix, el foreground nunca pasa a la app; con fix, la ventana principal toma el foreground en el instante del cierre del splash y lo conserva (en las corridas donde el usuario no estaba tipeando en otra app, caso en el que Windows niega el robo de foco por diseño). **Falta la verificación manual con doble click real** en la PC de testing.
- Verificación 2026-10-24: pruebas del sink/coordinador/escritura (recuperable/fatal, escalada, concurrencia, reentrada, deduplicación, fallo de escritura/planificación, UTC y archivos únicos). Render WPF aislado del diálogo fatal/recuperable y Ajustes, sin errores de binding. No se provocó un crash real de la aplicación en una sesión de simulador; falta validación manual de cierre/continuación y apertura con Explorer/navegador.
- Verificación **manual en ejecución real** pendiente de hacer por el usuario: forzar una excepción en runtime con la ventana abierta (cartel + log + cierre), forzar un fallo de arranque real (cartel + log), y mirar el splash durante un arranque con la app publicada. Lo verificado a la fecha es build, tests y smoke test del binario (ver CHANGELOG).
- El cartel en crash de **hilo de fondo** (AppDomain) es best-effort: depende de que el hilo STA dedicado pueda crear la ventana; el proceso igual se termina al cerrar el cartel.
