# UI: panel de configuración (overlay), preferencias de usuario y autorefresco

Documento del comportamiento de `RealFenixFailures.UI` respecto a: (1) el panel de configuración con pestañas (overlay interno, no una ventana), (2) las preferencias de usuario persistidas (ID de SimBrief) y (3) la eliminación del botón Refresh en favor del autorefresco.

## Ventana de configuración (overlay interno, no una ventana independiente)

El contenido de configuración es un **UserControl** (`RealFenixFailures.UI/Views/Panels/SettingsPanel.xaml` + `.cs`) **embebido dentro de `MainWindow`**, con ViewModel `RealFenixFailures.UI/ViewModels/SettingsWindowViewModel.cs`:

- El botón **⚙** de la status bar (posición del viejo Refresh) ejecuta `OpenSettingsCommand` del `MainWindowViewModel`, que pone `IsSettingsOpen = true`.
- Con esto aparece un **overlay sobre la fila de contenido** (Todo lo que está debajo de la status bar): un fondo semitransparente (`#C0080A0F`) que **opaca y bloquea** los modos, paneles y el log (inalcanzables mientras el overlay esté abierto). La title bar y la status bar quedan visibles e intactas.
- El panel de configuración se muestra **fijo en el centro** del área de contenido (780×620, con sombra y esquinas redondeadas, misma estética).
- Cerrar el overlay: botón **✕** del header del panel o tecla **ESC** (el panel eleva `CloseRequested`; `MainWindow` lo recibe y pone `IsSettingsOpen = false`). Al cerrar, el `MainWindowViewModel` persiste las preferencias pendientes (`Settings.SaveNow()`).
- `MainWindowViewModel` inyecta el `SettingsWindowViewModel` (transient) y lo expone como `Settings`; el panel se enlaza con `DataContext="{Binding Settings}"`.
- Pestañas (TabControl con estilo propio definido como recursos locales del panel):
  1. **General** — campo **ID de SimBrief** (alias de simbrief.com) persistido en las preferencias de usuario, con feedback "✓ Guardado" (debounce de 400 ms al escribir) y nota de privacidad.
  2. **Ayuda** — guía rápida de uso (cabina lista → elegir modo → activar y volar → cierre/bandeja).
  3. **Documentación** — enlaces a GitHub (repo), docs del proyecto y documentación pública de la API de SimBrief (`https://www.simbrief.com/api/`).
  4. **Actualizaciones** — versión instalada (InformationalVersion, conserva el sufijo `-dev`) y **buscar actualizaciones**: el botón consulta GitHub Releases de verdad (`IUpdateChecker` → `GithubUpdateChecker`, API pública `GET /repos/mathi0101/Fenix-Failures-Dispatcher/releases?per_page=5`), muestra pre-releases, indica "al día / hay versión nueva / sin releases / error" por colores, y ofrece descargar a GitHub Releases cuando hay versión más nueva. No descarga ni instala sola.
  5. **Reportar un fallo** — botón que abre GitHub Issues del repo.
- Los enlaces se abren con `Process.Start` + `UseShellExecute` (mismo patrón que la donación en `MainWindow`). URLs centralizadas como constantes en `SettingsWindowViewModel` (`DefaultRepositoryUrl` apunta a `https://github.com/mathi0101/Fenix-Failures-Dispatcher`).

## Preferencias de usuario

`RealFenixFailures.UI/Services/UserPreferencesService.cs` (singleton en DI):

- Persiste un JSON en `%AppData%\RealFenixFailures\userprefs.json` con `SimbriefUserId`.
- `Load()` tolerante: si el archivo no existe o falla la lectura, devuelve valores por defecto (log warning, no rompe la app). `Save()` crea el directorio si falta.
- Actualmente solo expone `SimbriefUserId`; el modelo `UserPreferences` es la base para más preferencias futuras.
- El guardado es con debounce desde el ViewModel (400 ms) y forzado (`SaveNow`) al cerrar el overlay.
- Configuración por máquina/usuario, no versionada, fuera de `appsettings.json` (que sigue siendo la configuración de arranque del host).

## Eliminación del botón Refresh (autorefresco)

- Se quitó el botón "↻ Refresh" de la status bar de `MainWindow` y toda su lógica: `RefreshCommand` y el método `RefreshAsync()` de `MainWindowViewModel` fueron eliminados.
- El refresco queda a cargo del **timer automático** del orquestador (`EngineOrchestrator.StartAutomaticTimerAsync`, configurado con `CheckConnectionIntervalSeconds`, 5 s): actualiza el estado de conexión y alimenta al detector de fases en cada tick; la UI refleja esos cambios por eventos `PropertyChanged`.
- En `InitializeAsync` el VM hace un `UpdateConnection` inicial (para pintar el estado antes del primer tick) y carga los presets custom una vez (`LoadCustomPresetsAsync`); las altas/bajas de presets custom ya se reflejan localmente en la UI (Create/Delete actualizan la colección), así que no se necesita recarga manual.
- `UpdateCommandStates` ya no referencia `RefreshCommand`.

## Pendiente / no verificado

- El flujo visual completo (abrir configuración, escribir ID, ver "✓ Guardado", abrir URLs, bloqueo del fondo) no se verificó manualmente en sesión interactiva: lo verificado es build limpio (0 errores/0 warnings), arranque de la app en Debug sin excepciones y 80/80 tests de `TestUnits`.
- El fallo del primer intento de `dotnet run` desde la raíz de la solución (appsettings.json no encontrado) es pre-existente y ajeno a este cambio: la app espera `appsettings.json` relativo al working directory del proyecto UI.
