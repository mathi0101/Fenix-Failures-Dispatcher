# Plan de implementación — App bilingüe ES/EN (issue #12)

> Issue: [#12 App bilingüe ES/EN seleccionable, 100 % traducida (visual)](https://github.com/mathi0101/Fenix-Failures-Dispatcher/issues/12) · Milestone V1.0.0
> Estado: **fases 0–3 implementadas (2026-11-04); queda QA manual visual**. Decisiones del usuario: voseo rioplatense se mantiene; lo que hoy está en inglés (catálogo de fallas, fases de vuelo, terminología aeronáutica) queda en inglés. Estado real en [`localizacion.md`](localizacion.md).

## 1. Objetivo

Toda la capa visible de la app (ventanas, paneles, tooltips, mensajes de validación, diálogos, modal de resultados y bandeja) en **español e inglés**, con:

- **Detección automática en el primer arranque** según el idioma de la PC: si el idioma de Windows es español → ES; cualquier otro caso (inglés u otro idioma) → **EN por defecto**.
- **Selector de idioma en Ajustes → General** (Español / English), aplicado **en vivo** (sin reiniciar) y **persistido en `userprefs.json`**.
- Un único origen de textos por clave: **ningún mensaje hardcodeado en vistas o viewmodels**.

## 2. Dimensionamiento (levantamiento real del código)

Superficie medida sobre `dev` (nov 2026):

| Superficie | Archivos | Textos (aprox.) |
|---|---|---|
| XAML (atributos `Text/Header/Title/Content/ToolTip`) | 12 vistas/paneles | ~370 (≈330 traducibles; el resto son íconos `✈─□✕`, bindings y marcas) |
| Strings dinámicos en viewmodels/code-behind | ~15 archivos | ~140 (RealisticModeViewModel 59, SessionResultWindowViewModel 26, MainWindowViewModel 13, SettingsWindowViewModel 11, LiveFailureEntryViewModel 11, RiskProfile*, TrayIconService, FatalErrorDialog, GithubUpdateChecker, CloseChoiceWindow) |
| Diálogos de crash (pre-DI) | CrashReporter/FatalErrorDialog | ~15 |

**Total estimado: ~450–500 claves de traducción.** Los logs internos (Serilog, `CrashLogWriter`, `TelemetryService`) no son parte de la capa visible y quedan como están.

## 3. Decisiones de diseño (recomendación + alternativas)

### 3.1 Mecanismo de localización → `.resx` + administrador propio (Plan B, por spike)

El estándar de la industria para WPF es la infraestructura nativa de .NET: **archivos `.resx` compilados como recursos satélite**. Como capa de cambio en vivo se evaluó **[WPFLocalizeExtension](https://github.com/XAML-Localization-Extension/WPFLocalizeExtension)** 3.10.0 (NuGet, ~1,9 M descargas, MIT), pero el **spike del 2026-11-04 lo descartó**: el compilador XAML de .NET 10 no resuelve sus markup extensions (error MC3074 con el namespace oficial, con `clr-namespace` explícito, y reproducido en un proyecto mínimo aislado — es un problema del mecanismo de resolución de `assembly=` del XAML compiler, no del proyecto). Se aplicó el **Plan B previsto**:

- Mismos `.resx` satélite (`Strings.resx` = inglés neutro/fallback, `Strings.es.resx` = español), **claves idénticas a las del plan A**.
- Administrador propio mínimo (`Localization/Loc.cs`, ~120 líneas): singleton con indexador que al cambiar el idioma dispara `PropertyChanged("Item[]")` y **todos los bindings refrescan en vivo**; markup extension `{loc:Loc Clave}` y `Loc.T(key, args)` para C#. Patrón clásico documentado por Microsoft, cero dependencias.

Detalle vigente en [`localizacion.md`](localizacion.md#decisiones-de-diseno).

### 3.2 ¿Cambio en vivo o reinicio? → **en vivo**

Es lo que el mecanismo da casi gratis y la expectativa estándar hoy (browsers, VS Code, Steam). Estrategia por superficie:

- **XAML estático**: refresca automáticamente (el markup extension escucha el cambio de cultura).
- **Strings compuestos en viewmodels**: los VMs que tienen textos suscritos a `ILocalizationService.LanguageChanged` re-disparan `PropertyChanged` (se agrega un `LocalizedViewModelBase` con `RefreshLocalizedProperties()` para no repetirlo 15 veces).
- **Menú de bandeja** (`TrayIconService`): se reconstruye en el evento `LanguageChanged`.
- **`CultureInfo.CurrentUICulture`/`CurrentCulture`** del hilo UI se fijan al idioma elegido para que fechas/`string.Format` queden coherentes.

### 3.3 Detección inicial y persistencia

- `UserPreferences` gana una propiedad `string? Language` (`"es"` | `"en"`; ausente/nulo = primer arranque).
- En `Program.Main`, **antes del splash y de crear la `Application`** (así también los diálogos de crash quedan en el idioma correcto):
  1. Si `userprefs.json` tiene `language` → usar ese valor.
  2. Si no: `CultureInfo.CurrentUICulture` (idioma de visualización de Windows). Si el código de dos letras es `es` → **ES**; cualquier otro caso → **EN**.
  3. Resolver y **persistir inmediatamente** el valor resuelto (la detección corre una sola vez; después manda la elección del usuario).
- El selector de Ajustes ofrece **Español / English** (sin opción "automático": mantenerla implicaría re-detectar y sorprender al usuario; el comportamiento automático queda cubierto en el primer arranque).

### 3.4 Decisiones menores (confirmadas con el usuario, 2026-11-04)

| Tema | Decisión |
|---|---|
| **Variante de español** | ✅ **Voseo rioplatense** («Descargá», «Podé»): es la voz actual de la app y se mantiene. |
| **Nombres de fallas del catálogo** (`failures.json`) | ✅ **Quedan en inglés**: son datos persistidos (`TriggeredFailure.FailureName` en la BD) y convención ECAM/aviación. La etiqueta visible alrededor (estados, acciones) sí se traduce. Un eventual catálogo bilingüe sería un issue aparte sobre Domain. |
| **Fases de vuelo y terminología aeronáutica** | ✅ **Quedan en inglés** (misma regla: «Cold and Dark», «Taxi», ECAM). |
| **Plurales** | Claves dobles donde haga falta (`..._One` / `..._Many`) elegidas desde el VM; sin motor de pluralización (sobre-ingeniería para 2 idiomas). |
| **No se traduce** | Marca («Real Fenix Failures», títulos de marca), glifos de ventana (`✈ ─ □ ✕`), tokens de formato (`HH:mm`), logs internos, textos de telemetría/crash-log técnico. El splash es una imagen de marca sin texto: nada que traducir. |

## 4. Arquitectura propuesta

Nuevos archivos (todos en `RealFenixFailures.UI`):

```
Localization/
  Strings.resx            → EN (neutra, fallback)
  Strings.es.resx         → ES
  Strings.Designer.cs     → clase tipada (generada, PublicClass)
  LocalizationService.cs  → ILocalizationService: T(key), T(key,args), CurrentLanguage, LanguageChanged
  LanguageResolver.cs     → lógica pura de primer arranque (testeable): prefs + CurrentUICulture → "es"|"en"
ViewModels/Base/LocalizedViewModelBase.cs   → re-notificación de propiedades al cambiar idioma
```

Flujo de arranque:

```
Program.Main
  └─ LocalizationService.Initialize(  ← lee userprefs.json (AppPaths), resuelve o carga idioma,
       prefs)                            fija culturas UI del hilo, persiste si fue primer arranque)
  └─ StartSplash() → new App() → host DI (LocalizationService registrado como singleton)
  └─ MainWindow / tray / diálogos leen SIEMPRE de Strings / ILocalizationService
```

Cambios puntuales:

- `RealFenixFailures.UI.csproj`: paquete `WPFLocalizeExtension`, `<NeutralLanguage>en</NeutralLanguage>`, `<SatelliteResourceLanguages>en;es</SatelliteResourceLanguages>` (higiene de publish), xmlns `lex` en las vistas.
- `UserPreferencesService` / `UserPreferences`: propiedad `Language` con el patrón existente (lock + save).
- `SettingsWindowViewModel` + `SettingsPanel.xaml`: sección **Idioma / Language** en Ajustes → General (ComboBox ES/EN; aplicar en vivo + persistir).
- `HostBuilderExtensions`: registro DI de `ILocalizationService`.
- `App.xaml.cs` / `Program.cs`: inicialización temprana; `TrayIconService` reconstruye su menú en `LanguageChanged`.

Convención de claves: `Área_Elemento[_Detalle]` — `MainWindow_Status_SimDisconnected`, `Settings_General_LanguageHelp`, `Realistic_Ofp_PendingValidation`, `Results_Failures_One/Many`. Un solo diccionario plano (≈500 entradas es manejable); nombres estables porque las claves quedan en el XAML.

## 5. Fases de trabajo

Cada fase termina con build verde, tests, entrada en `docs/CHANGELOG.md` y commits selectivos (rama `feature/app-bilingue` desde `dev`).

### Fase 0 — Infraestructura y spike (~1 sesión)
1. Spike WPFLocalizeExtension sobre `net10.0-windows` x64: DEBUG + `dotnet publish -c Release` (verificar que el satélite `es/` llega al output y al single-file; si no, `IncludeSatelliteAssembliesInSingleFile`). Decidir A vs B **antes de migrar textos**.
2. Crear `Localization/` (resx EN/ES, servicio, resolver), `LocalizedViewModelBase`, propiedad `Language` en prefs, inicialización temprana en `Program.Main`, registro DI.
3. Selector de idioma en Ajustes → General con 3–5 textos de muestra ya migrados de SettingsPanel.
4. Tests: `LanguageResolver` (es→ES, en→EN, otro→EN, sin prefs→detecta, con prefs→respeta); paridad de claves.

**Aceptación:** la app arranca en ambos idiomas según detección/preview; el selector cambia en vivo las muestras; persiste tras reiniciar.

### Fase 1 — Superficie principal (~130 claves)
`MainWindow.xaml` (+ mensajes de estado de `MainWindowViewModel`), `SettingsPanel` (60), `LogPanel`, `CustomModePanel`, `TrainingModePanel` + `TrainingScenarioViewModel`/`CustomPresetViewModel`, `CloseChoiceWindow` (+ code-behind), `TrayIconService` (rebuild del menú), mensajes de update-check de `SettingsWindowViewModel`/`GithubUpdateChecker`.

### Fase 2 — Modo Realista (~230 claves, la fase más grande)
`RealisticModePanel` (166), `RealisticModeViewModel` (59), `RealisticRiskProfileWindow` + `RiskProfileOptionViewModel` + `RiskProfileWindowViewModel`, `UnfinishedSessionsWindow` + su item VM, `FlightSessionItemViewModel`, `UserAircraftItemViewModel`, `WearSystemViewModel`.

### Fase 3 — Resultados, fallas en vivo y diálogos (~90 claves)
`SessionResultWindow` + `SessionResultWindowViewModel`, `LiveFailureEntryViewModel`, `FatalErrorDialog` (+ textos de `CrashReporter`, que se resuelven desde el servicio estático ya inicializado).

### Fase 4 — Cierre y calidad (~1 sesión)
1. Test de **paridad de claves** ES/EN (agregar ProjectReference a UI en `RealFenixFailures.TestUnits`) + test anti-regresión de claves faltantes.
2. Validador best-effort de textos hardcodeados (script que escanea XAML/VMs con literales traducibles restantes) — no perfecto, pero detecta olvidos gruesos.
3. QA manual en DEBUG (mocks): recorrer las 12 superficies en ES y EN, cambio en vivo, bandeja, primer arranque con Windows en es/en/otro, persistencia, diálogo fatal simulado. Capturas para la issue.
4. Docs: nueva `docs/ui/localizacion.md` (estado y diseño del subsistema) + actualización de `docs/ui/configuracion-y-preferencias.md`.

**Nota:** no requiere sesión real MSFS/Fenix para validarse (es capa UI con mocks), pero se recomienda QA final en la PC de testing antes del release del milestone.

## 6. Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| WPFLocalizeExtension incompatible con .NET 10 / single-file | Spike en fase 0 con decisión temprana; plan B con el mismo `.resx` y administrador propio |
| Textos con interpolación (`{p.ValidationMessage}`) mal re-formateados al cambiar idioma | Convención única `T(key, args)` + re-notificación centralizada en `LocalizedViewModelBase` |
| Claves con typo en XAML (falla silenciosa en runtime) | Paridad de claves testeada + fallback visible en QA + clase tipada para VMs |
| Plurales y género mal resueltos | Revisión del diccionario por fase; claves `One/Many` donde aplique |
| Trabajo simultáneo de otros agentes sobre las mismas vistas | Releer cada archivo antes de editarlo; fases acotadas por archivo; commits selectivos explícitos |

## 7. Bump de versión

La tarea completa es `feat` → **bump sugerido: 0.6.0** (desde `0.5.1-beta.1` en `Directory.Build.props`). El salto a 1.0.0 lo cierra el runbook de release cuando el milestone V1.0.0 quede completo; el número final se define únicamente en ese flujo.
