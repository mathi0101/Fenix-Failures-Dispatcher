# UI: manejo global de errores y arranque con splash

Documento del comportamiento de `RealFenixFailures.UI` respecto a: (1) capturar excepciones no controladas a nivel de aplicación, registrarlas en `logs/crash` y mostrar un cartel de error general en pantalla, y (2) mostrar un splash de carga durante el arranque de la app. Todo el código vive en `RealFenixFailures.UI/Diagnostics/` y en el punto de entrada `Program.cs`.

## Problema que resuelve

Antes de este cambio, una excepción no controlada (en el hilo de interfaz, en un hilo de fondo, o durante el arranque) terminaba el proceso en silencio **sin dejar rastro**: ni cartel, ni log. Con este cambio:

- Toda excepción no controlada queda registrada en un archivo propio en `logs/crash/` con toda la información relevante del proceso y el **trace completo** (incluidas excepciones internas anidadas y `AggregateException`).
- Las que son fatales muestran un cartel de error general en pantalla.
- Si la app intenta abrirse y no puede, pasa exactamente lo mismo (log + cartel) en lugar de morir sin explicación.
- Durante el arranque se muestra un splash sin bordes para que se vea que la app está cargando (y no "trancada").

## Handlers globales — `Diagnostics/CrashReporter.cs`

`CrashReporter.AttachGlobalHandlers(app)` se conecta **como primer paso de `App.OnStartup`** y registra tres handlers:

| Evento | Cuándo dispara | Comportamiento |
|---|---|---|
| `DispatcherUnhandledException` | Excepción no controlada en el hilo de interfaz (incluye errores en handlers `async void` y en el arranque del dispatcher) | Log + cartel (`shutdown: true`): al cerrar el cartel, la app se cierra de forma controlada (`Shutdown`) |
| `AppDomain.UnhandledException` | Excepción no controlada fatal en **cualquier** hilo | Log + cartel *best-effort* (se muestra en un hilo STA dedicado si hace falta). El proceso termina igual al retornar el handler (comportamiento del CLR) |
| `TaskScheduler.UnobservedTaskException` | Tarea fire-and-forget que falló sin observador | **Solo log** (no fatal: la app sigue corriendo). Decisión documentada: no se muestra cartel para no spamear diálogos por fallos de tareas de fondo no fatales |

### Sentido de `Program.StartupFailed`

Caso raro pero verificado en la práctica: si el constructor de `App` falla (p. ej. `appsettings.json` faltante), `System.Windows.Application.Current` ya quedó seteado por el ctor base y WPF puede llegar a invocar `OnStartup` desde el pump de mensajes del diálogo de error — con `_host` null. El flag `Program.StartupFailed` hace que `OnStartup` retorne de inmediato en ese caso y se evite un segundo reporte de crash espurio (con stack trace de `NullReferenceException` que confundía).

## Archivo de crash — `Diagnostics/CrashLogWriter.cs`

- Ruta (determinista, junto al ejecutable): `<AppContext.BaseDirectory>\logs\crash\crash-YYYYMMDD-HHmmss-fff.log`. Es distinto del log de Serilog `logs/realfenix-*.log`, que es relativo al directorio de trabajo y con rolling diario.
- Contenido: fecha local y UTC, contexto del error, versión de la app, runtime .NET, sistema operativo, máquina, usuario, PID, ruta del proceso, directorios base y actual, procesadores, y la excepción con tipo, mensaje, fuente, datos y **stack trace completo por nivel** (inner exceptions recursivas; `AggregateException` aplana todas sus internas).
- `Write()` **nunca lanza**: un fallo del propio registro (disco lleno, permisos) no debe enmascarar el manejo del error original; devuelve `null` y el cartel se muestra igual.

## Cartel de error — `Diagnostics/FatalErrorDialog.xaml(.cs)`

- Ventana **autocontenida** (colores/estilos inline, no depende de los recursos de la app) para poder mostrarse incluso sin un `Application` corriendo (crash previo al host WPF).
- Muestra: mensaje general, la ruta del archivo de crash, botón **"Ver registro de crash"** (abre el archivo con la asociación por defecto del sistema), un expander **"Detalle técnico"** con el stack trace completo en un TextBox de solo lectura (seleccionable/copiable) y botón **Cerrar**.
- `ShowGeneral()` detecta si la llamada viene del hilo de interfaz: si no (o si no hay `Application`), crea un hilo **STA dedicado** que muestra el diálogo y bloquea (Join) hasta que el usuario lo cierra — así el cartel aparece hasta en crashes de hilo de fondo, donde el proceso se termina justo después.

## Arranque con splash — `Program.cs` y `App.xaml.cs`

- **Entrada manual**: el `.csproj` define `<StartupObject>RealFenixFailures.UI.Program</StartupObject>`. `Program.Main` es `[STAThread]`, muestra el splash **antes** de crear la aplicación y envuelve `new App()` + `InitializeComponent()` + `Run()` en try/catch: un fallo ahí (config, recursos de App.xaml) va directo a log + cartel.
- **Splash**: el `SplashScreen` clásico de WPF se reemplazó por `Views/SplashWindow.xaml`, ventana custom sin bordes (560×340, `WindowStyle=None`, centrada, sin tarea) que muestra **la pieza gráfica original** (`Assets/Branding/splash-screen.png`). La barra de progreso se anima con la técnica de la imagen vieja: 6 frames generados con la misma estética (`splash-frame-0.png` con la barra **vacía**, `splash-frame-1..4.png` con el relleno en 52/68/84/100 %; el frame 0 es el PNG original con el 36 %) que se intercambian en vivo cada **350 ms** (`FrameIntervalMs` en `SplashWindow.xaml.cs`) en una secuencia **0 → full que se detiene en el último frame** (no retrocede ni reinicia). La barra **no está conectada al progreso real del arranque**: si la app termina antes, se corta donde iba; si tarda más, queda llena esperando el cierre. La animación corre en un **hilo STA dedicado** (`Program.StartSplash`, con su propio `Dispatcher`): durante el arranque el hilo principal está ocupado construyendo el host y un `DispatcherTimer` del hilo principal no llega a dispararse; con el pump propio la animación va siempre fluida. Se muestra antes de crear la `Application` (los frames se cargan por ruta de archivo, `CopyToOutputDirectory` en el `.csproj`) y la cierra `App.OnStartup` con `Program.CloseSplash()` al renderizarse la ventana principal — **esa es la duración del splash: no hay tiempo fijo, se cierra cuando la app principal carga**. `Program.CloseSplash()` encola el cierre en el dispatcher del hilo del splash y termina su pump; es el cierre explícito para ramas fallidas y para la segunda instancia de la instancia única.
- **Cursor durante el arranque**: el `SplashScreen` clásico mostraba el cursor de "aplicación iniciando" (`IDC_APPSTARTING`) sobre el splash, lo que daba sensación de app trancada (y encima la barra era una imagen fija). La ventana custom lleva `Cursor="Arrow"` y no usa `SplashScreen`, así que el cursor se ve normal durante todo el arranque.
- **`App.OnStartup`** quedó con tres zonas de manejo:
  1. `AttachGlobalHandlers` + guard de instancia única (segunda instancia → cierra splash y sale en silencio: comportamiento normal, **no** es un crash y no genera log).
  2. try/catch alrededor de `_host.StartAsync()` + `MigrateAsync` + `InitializeAsync` → "no pudo iniciarse (servicios, conexión o base de datos)".
  3. try/catch alrededor de la creación de `MainWindow` + tray → "no pudo iniciarse (ventana principal)".
  - Ambas ramas hacen `Program.CloseSplash()` + `CrashReporter.ReportFatal(…, shutdown: true)` (cartel primero, cierre al cerrar el cartel) + `return`.

## Notas y decisiones

- **Splash por frames**: el splash original (el `SplashScreen` de WPF + PNG con la barra dibujada) no podía animar la barra porque era una imagen estática, y la primera iteración XAML con `ProgressBar` cambió la estética que al usuario no le gustó. El diseño final conserva **la imagen de siempre** como frame con el 36 % y agrega 5 frames idénticos: uno con la **barra vacía** (`splash-frame-0.png`, el frame inicial) y cuatro con la barra más avanzada (52/68/84/100 %), generados con un script con la misma paleta: se intercambian en vivo y el ojo percibe la barra llenándose de 0 a full. Detalle: los frames se versionan como archivos que se copian al output; no se generan en runtime.
- El host sigue leyendo `appsettings.json` del **directorio de trabajo actual** (comportamiento preexistente de `Host.CreateDefaultBuilder`, no cambiado): si se lanza el exe desde otra carpeta sin ese archivo, el arranque ahora falla con cartel y log en vez de morir en silencio.
- El log de crash se acumula sin rotación (nombre con timestamp): conviene decidir algún día una política de retención.

## Crash al cerrar: Close() reentrante en MainWindow_Closing

Contexto: al pulsar la X (o Alt+F4), `MainWindow_Closing` hace `e.Cancel = true` y muestra el modal `CloseChoiceWindow` con `ShowDialog`. Si el usuario elige «Cerrar la app por completo», el handler llamaba a `Close()` **dentro del propio evento `Closing`**: la ventana todavía está procesando `WM_CLOSE` y el `Close()` reentrante lanza `InvalidOperationException` (*«Cannot set Visibility to Visible or call Show, ShowDialog, Close, or WindowInteropHelper.EnsureHandle while a Window is closing»*) en `MainWindow.xaml.cs:58`. Esa excepción llega a `DispatcherUnhandledException` del crash reporter, que cierra la app con cartel y log de crash (`crash-20261018-143622-468.log`).

Fix (2026-10-18): el `Close()` del `case Shutdown` se difiere hasta después de que el handler de `Closing` termina, con `Dispatcher.BeginInvoke(new Action(Close))`. `IsExitAllowed` ya quedó en `true`, así que el `Closing` reentrante disparado por el cierre diferido retorna sin cancelar y la ventana se cierra sin excepción. No se alteró el resto: cancelar, segundo plano (bandeja) ni la lógica del motor.

Verificación: harness WPF externo al repo (`RFCloseHarness`) que linkea `MainWindow.xaml.cs` real y recorre los tres caminos del modal con stubs; antes del fix reproduce la excepción exacta (rojo) y después del fix cierra sin excepción (verde). Detalle en `CHANGELOG.md` (2026-10-18).

## Pendiente / no verificado

- Verificación **manual en ejecución real** pendiente de hacer por el usuario: forzar una excepción en runtime con la ventana abierta (cartel + log + cierre), forzar un fallo de arranque real (cartel + log), y mirar el splash durante un arranque con la app publicada. Lo verificado a la fecha es build, tests y smoke test del binario (ver CHANGELOG).
- El cartel en crash de **hilo de fondo** (AppDomain) es best-effort: depende de que el hilo STA dedicado pueda crear la ventana; el proceso igual se termina al cerrar el cartel.
