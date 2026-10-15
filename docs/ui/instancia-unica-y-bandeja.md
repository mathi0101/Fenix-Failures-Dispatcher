# UI: instancia única y bandeja del sistema

Documento del comportamiento de `RealFenixFailures.UI` respecto a: (1) impedir más de un proceso corriendo a la vez y (2) seguir en segundo plano con un icono en la bandeja del sistema cuando se cierra la ventana.

## Instancia única

`RealFenixFailures.UI/Services/SingleInstanceGuard.cs`:

- Usa un `Mutex` con nombre fijo (`Local\RealFenixFailures.UI.SingleInstance.<GUID>`) tomado en `App.OnStartup` **antes** de iniciar el host (`IHost`), aplicar migraciones de EF o correr el initializer. Si el Mutex ya existía, el proceso nuevo no toca nada de infraestructura: solo señala y termina.
- La señalización usa un `EventWaitHandle` hermano (`...ShowRequest`) nombrado igual en ambas instancias. El proceso duplicado hace `Set()` y llama a `Application.Shutdown()`. La instancia primaria escucha ese evento con `ThreadPool.RegisterWaitForSingleObject` y, al dispararse, restaura la ventana principal (mismo camino que "Abrir" en la bandeja).
- Alcance/limitaciones conocidas:
  - El Mutex es `Local\` (sesión de usuario actual), no `Global\`: si una copia corre elevada (administrador) y otra no, cada una ve su propia sesión y **no se detectan entre sí**. No resuelto en este alcance.
  - No hay mecanismo de recuperación si el proceso muere sin liberar el Mutex de forma anómala (crash duro); el runtime libera los Mutex abandonados al terminar el proceso, así que no debería requerir intervención manual, pero no se probó ese caso puntual.

## Icono de bandeja

`RealFenixFailures.UI/Services/TrayIconService.cs`, usando `System.Windows.Forms.NotifyIcon` (sin agregar paquetes NuGet: se habilitó `<UseWindowsForms>true</UseWindowsForms>` en el `.csproj`).

- **Nota de build**: habilitar `UseWindowsForms` con `ImplicitUsings` agrega por defecto `global using System.Drawing;` y `global using System.Windows.Forms;`, lo que genera ambigüedad de tipos con WPF (`UserControl`, `Color`, `Brush` existen en ambos namespaces). Se removieron esos dos usings globales en el `.csproj` (`<Using Remove="..." />`) y `TrayIconService.cs` usa su propio `using System.Windows.Forms;` local.
- Menú contextual con dos ítems: **Abrir** (restaura la ventana) y **Salir** (cierra la app de verdad).
- Doble click sobre el icono también restaura la ventana.
- `Dispose()` oculta y libera el `NotifyIcon` en `App.OnExit`; limitación conocida de WinForms: si el proceso termina de forma anómala sin pasar por `OnExit`, el icono puede quedar "fantasma" en la bandeja hasta que el usuario pase el mouse sobre él.

## Ciclo de vida ventana ↔ bandeja

- `App.xaml` tiene `ShutdownMode="OnExplicitShutdown"`: WPF no cierra la app solo porque se oculte la última ventana.
- `MainWindow` expone `IsExitAllowed` (default `false`) y hookea `Closing`: si `IsExitAllowed` es `false`, cancela el cierre y hace `Hide()` en lugar de destruir la ventana. Esto aplica también al botón **X** de la barra de título custom (`CloseWindow` sigue llamando a `Close()`; ahora ese `Close()` queda interceptado).
- **Cambio de UX**: el botón X de la ventana ya no cierra la app, la manda a la bandeja. El botón de minimizar (icono "_") no cambió: sigue minimizando al taskbar normal. La única forma de cerrar la app por completo es "Salir" desde el menú del icono de bandeja, que pone `IsExitAllowed = true` en la ventana y llama a `Application.Shutdown()`.
- Restaurar (desde "Abrir"/doble click en la bandeja, o por señal de instancia única) siempre fuerza `WindowState.Normal` antes de `Show()` + `Activate()`.

## Pendiente / no verificado

- No se verificó manualmente en ejecución real (abrir dos instancias, minimizar a bandeja, restaurar, salir desde el tray): el build de verificación quedó bloqueado por una restricción del entorno del agente al momento de implementar este cambio. **Falta correr `dotnet build` y probar el flujo a mano antes de dar esto por funcionando.**
