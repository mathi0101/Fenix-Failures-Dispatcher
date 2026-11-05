# UI: localización bilingüe ES/EN (issue #12)

Documento de estado y diseño del subsistema de localización de `RealFenixFailures.UI`: cómo se resuelve el idioma, de dónde salen los textos y cómo se migran las superficies restantes. Plan completo en [`plan-bilingue-issue-12.md`](plan-bilingue-issue-12.md).

## Estado

**Implementado (fases 0–3 de [plan-bilingue-issue-12.md](plan-bilingue-issue-12.md), 2026-11-04)**: toda la capa visible migrada a claves — ~210 textos XAML (MainWindow, CloseChoice, RealisticRiskProfile, SessionResult, UnfinishedSessions, paneles Custom/Log/Training/Realistic/Settings, FatalErrorDialog) y ~140 textos de viewmodels/servicios (RealisticModeViewModel, SessionResultWindowViewModel, MainWindowViewModel, SettingsWindowViewModel, item VMs, LiveFailureEntry, RiskProfileOption/Window, TrayIconService, GithubUpdateChecker, CrashReporter). El residuo XAML sin migrar es intencional: marca («Real Fenix Failures», splash), terminología aeronáutica en inglés («Cold and Dark», «Taxi») y unidades («+10 min», «NM»).

**Ajustes rediseñados (2026-11-04, mismo día)**: General pasa a grilla de dos columnas (Idioma | SimBrief) con textos de ayuda a ancho completo; **Ayuda y Documentación unificadas** en una sola pestaña (guía rápida en grilla 2×2 + card de repositorio abajo); **pestaña nueva «Modo Realista»** a la derecha de General para las preferencias de usuario futuras del modo, con el aviso de fin de vuelo (minutos de gracia) rediseñado: input con unidad visible y presets rápidos (0/5/15/30) vía `SetRealisticGraceCommand`. Y **fix del selector de idioma**: con `SelectedValue`+`SelectedValuePath` el binding empujaba `default(AppLanguage)` al cargar el panel (antes de que `ItemsSource` estuviera poblado) y el setter forzaba English — el idioma mostrado no reflejaba el real. Ahora bindea `SelectedItem` a `SelectedLanguageOption` (el getter matchea por record-equality, así que WPF no empuja null al cargar) con guard de null en el setter.

**Catálogo de presets en inglés (2026-11-04)**: traducidos a inglés los textos visibles de los dos catálogos de presets — `training_presets.json` (6 presets: `Description` + `TriggerDescription`; los `Name` ya estaban en inglés) y `realistic_presets.json` (44 presets: `Name` + `Description` + `TriggerDescription`, 132 cadenas). Edición de datos curados vía mapeo literal con aserción de ocurrencia única: sin tocar `Failures`, probabilidades, `Phase` ni `Difficulty`. Ojo: los presets se siembran a la BD una sola vez (`PresetService.InitializeAsync` siembra solo si no existen del tipo), así que **las BDs existentes (incluida la de la PC de testing) siguen mostrando los presets en español** hasta re-sembrar; los arranques con BD nueva salen en inglés.

**Verificado** (2026-11-04): build verde; suite completa 313/314 (el único fallo, `FailureEngineSettings_RealisticMode_HasObjectiveDefaults`, es del WIP de calibración de otra tarea, ajeno a localización); suite de localización 34/34; publish Release con el satélite `es/RealFenixFailures.resources.dll` en la salida; smoke test de arranque real: primera ejecución detectó Windows en inglés y persistió `Language: "en"` en `userprefs.json` sin tocar el resto de las preferencias.

**Verificado visualmente** (QA manual en la PC de testing, 2026-11-05 — cierre de v0.6.0-pre): el cambio de idioma en vivo en pantalla (selector de Ajustes, bandeja, refresco de paneles) quedó verificado en sesión real; el mecanismo sigue cubierto además por tests unitarios de eventos (`Item[]`/`LanguageChanged`).

## Decisiones de diseño

- **Mecanismo**: recursos `.resx` estándar de .NET, compilados como recursos satélite (`es/RealFenixFailures.resources.dll`). **Sin paquete externo**: el spike con WPFLocalizeExtension 3.10.0 mostró incompatibilidad con el compilador XAML de .NET 10 (MC3074 incluso en proyecto mínimo; verificado 2026-11-04), así que se implementó el administrador propio `Localization/Loc.cs` (~120 líneas, patrón clásico WPF de indexador + `PropertyChanged("Item[]")`).
- **Culturas**: el resx neutro (`Strings.resx`) es **inglés** y hace de fallback (cualquier cultura no soportada cae a inglés); el español vive en `Strings.es.resx`. `NeutralLanguage=en` y `SatelliteResourceLanguages=en;es` en el `.csproj`.
- **Cambio de idioma en vivo, sin reiniciar**: `Loc.Instance.CurrentLanguage` aplica `CurrentUICulture`/`CurrentCulture` del hilo y dispara `PropertyChanged("Item[]")`, con lo que todos los bindings `{loc:Loc Clave}` refrescan; los textos compuestos de viewmodels se re-notifican suscribiéndose a `Loc.Instance.LanguageChanged` (patrón: `LocalizedViewModelBase` cuando se migre esa fase).
- **Detección del primer arranque** (única vez): `LanguageBootstrapper.Initialize()` corre en `Program.Main` antes del splash y de la `Application` (los diálogos de crash pre-DI también salen en el idioma correcto). Preferencia guardada manda; si no hay, `CurrentUICulture` de Windows: español → ES, **cualquier otro caso → EN**. El valor detectado se persiste en `App.OnStartup` y desde ahí manda la elección del usuario. No existe la opción «automático» en el selector.
- **Persistencia**: propiedad `Language` (`"es"`/`"en"`, nulo = sin elegir) en `userprefs.json` vía `UserPreferencesService` (mismo patrón lock+save que el resto). El cambio desde el selector persiste de inmediato (no va por el debounce).
- **Alcance de traducción** (acuerdos de la issue): se mantiene el **voseo rioplatense**; los catálogos de datos — fallas (`failures.json`) y presets (`training_presets.json`, `realistic_presets.json`: `Name`/`Description`/`TriggerDescription`) —, las fases de vuelo y la terminología aeronáutica/ECAM quedan **en inglés** (son datos persistidos en BD y convención del dominio, no capa de UI); no se traducen marca, glifos de ventana ni logs internos.
- **Nombres de idioma en el selector**: endónimos invariables («English» / «Español»), sin claves de traducción.

## Convención de claves

`Área_Sección_Elemento` — `Settings_General_LanguageLabel`, `Settings_General_SimbriefHelp`. Un único diccionario plano (el volumen total ronda las 500 claves). Textos con datos interpolados usan `{0}, {1}…` y se resuelven con `Loc.T(key, args)`; plurales con claves dobles `..._One`/`..._Many` elegidas desde el VM.

**Uso**:

```xaml
<TextBlock Text="{loc:Loc Settings_General_LanguageLabel}"/>
```
```csharp
var msg = Loc.T("Realistic_Ofp_Invalid", validationMessage);
```

## Componentes (`RealFenixFailures.UI/Localization/`)

| Archivo | Rol |
|---|---|
| `Strings.resx` / `Strings.es.resx` | Diccionarios EN (neutro/fallback) y ES (voseo). Paridad de claves exigida por test. |
| `Loc.cs` | Singleton + indexador con refresco en vivo, `T(key, args)`, `ApplyCulture` y markup extension `LocExtension`. |
| `LanguageResolver.cs` | Lógica pura del primer arranque (preferencia → detección → EN default) + `AppLanguage` y `LanguageOption`. |
| `LanguageBootstrapper.cs` | Lectura temprana de `userprefs.json` (antes de la DI) y aplicación de la cultura del arranque. |

## Pruebas

`RealFenixFailures.TestUnits/Localization/LocalizationTests.cs` (TFM del proyecto de pruebas pasado a `net10.0-windows` + `UseWPF` + ProjectReference a UI; los `Compile Include` de fuentes UI quedaron obsoletos y fueron removidos, con `Using Include System.IO/System.Net.Http` para compensar los usings implícitos que el SDK de WindowsDesktop no agrega; `TestAssemblyInfo.cs` deshabilita la paralelización de xUnit porque `Loc` es un singleton global que los tests mutan):

- **`LanguageResolverTests`**: preferencia válida manda (normalizando mayúsculas/espacios), valores ausentes/corruptos → detección, detección por cultura (`es-*` → ES; `en-*`, `fr`, `pt`, `de`, invariante → EN).
- **`LocTests`**: traducción por idioma, formato con args y cultura, indexador, eventos (`Item[]`, `LanguageChanged`, sin eventos si no cambia).
- **`ResxParityTests`**: ES e EN tienen exactamente las mismas claves y ningún valor vacío (lee el `.resources` del ensamblado principal y del satélite `es/RealFenixFailures.resources.dll`).
- **`SessionResultWindowViewModelTests`** (existentes): fijan `Loc.Instance.CurrentLanguage = AppLanguage.Spanish` al inicio porque el modal hoy resuelve sus textos en construcción.

Validador de olvidos: `tools/check-hardcoded-strings.py` (best-effort; excluye logs de Serilog y excepciones internas por diseño).

## Pendiente / limitaciones

- **Re-sembrado de presets en BDs existentes — resuelto (2026-11-04)**: la BD se autocorrige al primer arranque con el sincronizador de catálogos por hash ([`docs/persistence/catalog-sync.md`](../persistence/catalog-sync.md)); hasta verificar en sesión real, el refresco queda sujeto a esa verificación.
- **QA manual en la PC de testing — completado (2026-11-05)**: recorrido de las superficies en ES y EN con la sesión real MSFS/Fenix del cierre de v0.6.0-pre: cambio en vivo desde Ajustes, refresco de la bandeja, primer arranque con detección de idioma y persistencia.
- **Modal de resultados**: `SessionResultWindowViewModel` resuelve sus textos al construirse (ventana efímera); si el usuario cambia de idioma con el modal abierto, el texto nuevo se ve al reabrirlo. Aceptado para v1; re-notificación vía `LocalizedViewModelBase` si se pide refresco en vivo dentro del modal.
- **`RealisticRiskProfileWindow`**: al cambiar de idioma con la ventana abierta, el VM reconstruye las tarjetas y re-aplica la última estimación (re-pide el servicio); verificado por compilación y tests de eventos, no visualmente.
- **Textos con `{0}` y números**: el separador decimal sigue la cultura del idioma activo; revisar en QA que las tasas del panel Realista se lean bien en ambos idiomas.
- **`CrashReporter`/diálogos pre-DI**: funcionan con el idioma del bootstrap temprano; el mensaje de `MessageBox` de crash usa `Loc.T` (verificado por build, no en crash real).
