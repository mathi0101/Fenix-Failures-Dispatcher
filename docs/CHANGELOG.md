# CHANGELOG

Bitácora técnica del proyecto. No es un changelog de release: registra **qué** cambió, **por qué**, **qué áreas** tocó, **qué pruebas** se corrieron y **qué queda pendiente**. Solo se agregan entradas; no se reescriben ni se eliminan entradas pasadas.

## [Sin publicar]

### 2026-10-23 — UI §8 del modo Realistic: bloque OFP ampliado, navegación por pastillas y expectativa de fallas

**Qué y por qué**

- Implementa la sección §8 de [spec-modelo-probabilistico-y-desgaste.md](realistic-mode/spec-modelo-probabilistico-y-desgaste.md) sobre `RealisticModePanel`:
  - **Bloque OFP ampliado** (§8.1): chips origen → destino + texto de ruta, filas Nº de vuelo, Callsign, Salida programada (hora local con UTC en tooltip), Llegada estimada, Duración estimada, Distancia (`RouteDistanceNM` ahora expuesto en `FlightPlanDto` y mapeado por `SimbriefOfpService`), Aeronave del OFP y Fuente. Sin OFP se muestra un estado vacío en lugar de las filas; el estado de validación sigue siempre visible.
  - **Botón «Descargar OFP» rediseñado** (§8.3): outline accent de 32 px con icono de descarga; el tooltip explica el bloqueo mientras hay sesión activa (`OfpButtonToolTip`).
  - **Fila «OFP» del panel de requisitos arreglada** (§8.4): era texto estático «✘ Pendiente» que nunca actualizaba; ahora se bindea a `OfpRowDisplay` con color según `OfpValidated`.
  - **Check nuevo «Aeronave del OFP»** (§8.4): refleja el resultado de la validación de tipo ICAO del OFP contra la aeronave seleccionada (`OfpAircraftMatchDisplay`/`OfpAircraftMatchIsOk`). Nuevo orden: MSFS · Datos aeronave · Aeronave del OFP · OFP · Perfil, con Fenix como nota opcional al final; la fila «Perfil seleccionado» desaparece (quedó duplicada dentro del card-button).
  - **Card-button de perfil** (§8.4): reemplaza al ghost pequeño; muestra nombre del perfil (`HeaderProfileDisplay`), subtítulo «Seleccionado»/«Sin perfil» (`ProfileCardSubtitle`) e icono de edición; deshabilitado con sesión activa u operación pendiente.
  - **Ajustes por feedback visual** (mismo día): los estados de las filas OFP de requisitos pasan a ser **cortos** («✔ Cumplido»/«✘ Sin OFP»/«✘ Pendiente»/«✘ Vencido»/«✘ Inválido») para que no rompan el layout, y el mensaje completo de validación se muestra en el tooltip (`OfpValidationToolTip` nuevo) — el detalle largo queda en el bloque OFP de la izquierda, que es su lugar. Además, botón **cuadrado chico rojo con icono de borrar** junto a «Descargar OFP» (`DeleteOfpCommand`, estilo `DangerIconButtonStyle`) para descartar el OFP cargado: deshabilitado sin OFP, durante la descarga, con sesión activa u operación pendiente; borra solo el estado de la UI (`_ofpPlanForUi = null` + recálculo + log) — la fila de BD queda sin efecto y un nuevo download la reemplaza (`ISimbriefOfpService` no tiene borrado y no se agregó).
  - **Pastillas navegables** (§8.5): las 3 pastillas de pasos pasan a ser botones (`NavigateToStepCommand` con `CommandParameter` string; hacia atrás libre, hacia adelante solo «Continuar»); deshabilitadas con el motor activo u operación pendiente. Se quitan los botones «◀ Volver» de los encabezados de pasos 2 y 3, y «Estado · Perfil» sube a la banda del encabezado, a la misma altura que las pastillas (§8.6).
  - **Expectativa de fallas** (§8.7): junto al desgaste medio del header colapsable se muestra `ExpectationDisplay` (≈ fallas esperadas y P(≥1) para la duración del OFP; sin duración, la tasa por hora con su referencia), calculada por el nuevo `RealisticExpectationService` — estimación de presentación que reutiliza la física del motor, no cambia su comportamiento. Se recalcula con OFP, perfil y dashboard.
- `RealisticModeViewModel`: `NavigateToStepCommand` + bools `CanNavigateToStep1/2/3` (con `PropertyChanged` en cambios de paso y de estado del motor), propiedades de detalle OFP (`OfpOriginDisplay`, `OfpDestinationDisplay`, `OfpRouteTextDisplay`/`HasOfpRouteText`, `OffBlockUtcToolTip`), `OfpButtonToolTip` y `ProfileCardSubtitle`; `DeleteOfpCommand`/`CanDeleteOfp` y `RaiseOfpState()` (extracción del bloque de `OnPropertyChanged` de `ApplyOfpResult`).

**Áreas tocadas**: Application (`RealisticExpectationDto`, `IRealisticExpectationService`, `RealisticExpectationService`, `FlightPlanDto.RouteDistanceNM`, `SimbriefOfpService`), UI (`RealisticModePanel.xaml`, `RealisticModeViewModel`, `HostBuilderExtensions` con el registro del servicio), TestUnits (`RealisticExpectationServiceTests`, 8 tests) y `docs/realistic-mode/estado-y-diseno.md`.

**Verificación**

- Build Debug: **0 errores, 0 advertencias**. `dotnet test`: **185/185 pasan** (8 nuevos de la expectativa: referencia 0.22/h, desgaste 0 + 2 h Moderate → 0.44 esperadas, triplicado con desgaste 100 %, multiplicador por perfil, sin datos y horas no positivas).
- **No verificado visualmente en sesión real MSFS/Fenix**; antes de desplegar a la PC de testing hay que borrar `realfenixfailures.db` (esquema nuevo del motor v2) y migrar su `appsettings.json`.

### 2026-10-23 — Motor Realistic v2: fórmula probabilística unificada, score de agravamiento y desgaste dinámico por horas de vuelo

**Qué y por qué**

- Motivación: el incidente real de la sesión 12 (2026-10-22, perfil Moderate sin desgaste: doble falla hidráulica Major del mismo ATA 29 que terminó en emergencia declarada). El modelo vigente no medía carga combinada (caps por severidad independientes), no agrupaba por sistema y el respiro solo actuaba tras una Critical. Spec completa con la discusión de diseño: `docs/realistic-mode/spec-modelo-probabilistico-y-desgaste.md`.
- **Fórmula unificada** para tierra y vuelo: `p = p_base × mult_severidad × mult_perfil × factor_desgaste × peso_preset`, con física global nueva `RealisticMode:Physics` (`FailureEngineSettings.RealisticPhysicsSettings`). En vuelo la fórmula produce λ (eventos/hora) y `RealisticFailurePolicy.RollTick` integra el tiempo real (Poisson, `1−e^(−Σλ·Δt)` con Δt capado): la cadencia queda invariante al `EvaluationIntervalSeconds`; sorteo único ponderado por λ (reemplaza el doble sorteo severidad→candidato).
- **Score de agravamiento**: se eliminan `MinorCap`/`MajorCap`/`CriticalCap` y `MinorPerHour`/`MajorPerHour`/`CriticalPerHour`; queda `AggravationCap` por perfil sobre el score combinado (activas + reservas de cola, pesos 1/2/4, filtrado pre-roll). Sin bloqueo por ATA: los 2º/3º fallos del mismo sistema pagan `SameAtaAggravationExtra` (1) por falla previa simultánea del mismo ATA. `GroundFailuresCap` y el respiro tras Critical se conservan. El perfil pasa de 11 a 6 knobs; `appsettings.json` migrado a la estructura nueva (los `appsettings` locales viejos dejan de leerse: caen a defaults del catálogo).
- **Desgaste dinámico**: `WearMaxMultiplier` por perfil (2.5/3/3.5/4) reemplaza a los gains (`WearFactor = 1+(max−1)×wear/100`). Columna nueva `AircraftWearableSystem.WearPerFlightHour` (%/h, con seed data por ATA) y acumulación al cerrar sesión (`wear += rate × horas de sesión`, clamp 0–100) vía el nuevo `IAircraftSystemWearRepository`/`AircraftSystemWearRepository` — antes el desgaste era estático.
- Seed de tierra: mismo mecanismo Bernoulli individual + truncado por cap (mayor desgaste → mayor p → azar), ahora con la fórmula unificada y la p_base global.
- `RealisticSessionManager` acepta settings ausentes con defaults (antes propagaba null y el engine lo rechaza); `RealisticSessionEngine` gana parámetro opcional `IAircraftSystemWearRepository`.

**Áreas tocadas**: Domain (`FailureEngineSettings`, `RealisticProfileSettings`, `RealisticProfileCatalog`, `AircraftWearableSystem`, nueva interfaz `IAircraftSystemWearRepository`), Application (`RealisticFailurePolicy` reescrito, `RealisticSessionEngine`, `RealisticSessionManager`), Infrastructure (`AircraftSystemWearRepository`, `DependencyInjection`, `SeedData` con `WearPerFlightHour`), UI (`appsettings.json`), TestUnits (policy/catalogo/engine/persistence actualizados + 21 tests nuevos) y `docs/realistic-mode/estado-y-diseno.md`.

**Verificación**

- Build Debug y Release: **0 errores, 0 advertencias**.
- `dotnet test`: **177/177 pasan** (13 nuevos: física y seed con fórmula unificada, WearFactor lineal, λ por severidad/perfil/preset, score y extras ATA, invariancia de la cadencia a intervalo 5 s vs 30 s, reproducción del incidente de la sesión 12 — 3º hidráulico nunca elegible en Moderate, posible en 7700 pagando el extra —, flush de preparadas por score y acumulación de desgaste sobre SQLite real: 2 h × 0.6 %/h → +1.2 %).
- **No verificado en sesión real MSFS/Fenix**; la integración real requiere borrar `realfenixfailures.db` (esquema nuevo) y migrar el `appsettings` de la PC de testing.

### 2026-10-22 — Splash frames embebidos en el ensamblado: la raíz de la app publicada queda sin la carpeta Assets

**Qué y por qué**

- Los 6 frames del splash (`Assets/Branding/splash-frame-*.png`) viajaban sueltos a salida y publish (copiados por un `None` con `CopyToOutputDirectory`) porque `SplashWindow` se muestra antes de crear la `Application` y los cargaba por ruta de archivo (`AppContext.BaseDirectory\Assets\Branding`). Ahora van **embebidos en el ensamblado** (`EmbeddedResource`, recurso `RealFenixFailures.UI.Assets.Branding.splash-frame-N.png`) y `SplashWindow` los carga por `Assembly.GetManifestResourceStream` + `BitmapImage.StreamSource` (con `FileNotFoundException` descriptiva si faltara uno): no necesita `Application` ni pack URIs, funciona igual en el hilo STA del splash y dentro del exe único.
- Resultado: el publish single-file ya no crea la carpeta `Assets\` en la raíz de la app (el icono y el fondo ya eran `<Resource>` embebidos).

**Áreas tocadas**: `RealFenixFailures.UI.csproj` (`None`+copiar → `EmbeddedResource`), `Views/SplashWindow.xaml.cs` (carga por stream de recurso), `docs/ui/manejo-de-errores-y-arranque.md` (sección splash), este CHANGELOG.

**Verificación**

- Build Debug y Release: 0 errores, 0 advertencias. `dotnet test`: **164/164 pasan**.
- Harness mínimo (`Assembly.GetManifestResourceStream` sobre los DLL Debug y Release de la UI): los 6 recursos existen con los nombres esperados y cada stream tiene firma PNG válida.
- Smoke test del exe Debug (arranque real, 12 s): proceso vivo sin crash y detenido limpio (la carga de frames no lanzó excepciones; **no verificado visualmente** que la animación se vea igual — requiere mirada del usuario en pantalla).
- `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true` a carpeta fresca: raíz sin `Assets\`, solo exe único + `appsettings.json` + DLLs sueltas (SimConnect managed/nativa + nativas de WPF) + pdbs.

### 2026-10-22 — Empaquetado: SimConnect (wrapper managed + nativa) viaja suelto a la carpeta de publish incluso con PublishSingleFile

**Qué y por qué**

- Objetivo: que `Microsoft.FlightSimulator.SimConnect.dll` quede copiado en la carpeta de publicación como archivo suelto, incluso cuando el publish es en un solo archivo (`PublishSingleFile=true`); antes no viajaba (referencia con `Private=False` y un `None Update` que no alcanzaba el archivo porque vive fuera del proyecto).
- **Wrapper versionado en `libs\`**: se agrega `RealFenixFailures.Integrations.SimConnect\libs\Microsoft.FlightSimulator.SimConnect.dll` (172 KB, copia del SDK) para no depender de dónde (ni si) está instalado el SDK al compilar: tener el simulador no garantiza el SDK y su carpeta la elige quien lo instala. El csproj define `SimConnectManagedDll` (libs\ si existe, si no la ruta del SDK) y una única `Reference` con `HintPath` a esa fuente (`Private=False`, `EmbedInteropTypes=False`).
- **Copia suelta a salida y publish**: `None Include` con `Link`, `CopyToOutputDirectory`/`CopyToPublishDirectory=PreserveNewest` y `ExcludeFromSingleFile=true` (el pipeline de publish respeta esa metadata y el bundler excluye esos archivos del exe único; ver `Microsoft.NET.Publish.targets`). El item transita del proyecto de integración a la UI por el canal estándar `GetCopyToOutputDirectoryItems`.
- **Deduplicación en la UI**: RAR también resuelve el wrapper como dependencia del ensamblado de `Integrations.SimConnect` y lo metía en `ReferenceCopyLocalPaths`; esa segunda vía duplicaba el archivo en el publish (NETSDK1152) y con single-file lo habría empaquetado dentro del exe. `RealFenixFailures.UI.csproj` agrega el target `QuitarSimConnectWrapperDeCopyLocal` (`AfterTargets=ResolveAssemblyReferences`) que lo quita de esa lista, dejando solo la copia suelta.
- Reemplaza el `None Update="Microsoft.FlightSimulator.SimConnect.dll"` agregado a mano en el csproj de la integración (era un no-op: el glob de `None` solo incluye archivos dentro de la carpeta del proyecto).

**Áreas tocadas**: `RealFenixFailures.Integrations.SimConnect.csproj`, `RealFenixFailures.UI.csproj`, nuevo `RealFenixFailures.Integrations.SimConnect/libs/Microsoft.FlightSimulator.SimConnect.dll` (versionado), `docs/integraciones/simconnect.md` (sección "Obtención de la DLL del SDK"), este CHANGELOG.

**Verificación**

- `dotnet publish RealFenixFailures.UI -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`: carpeta de publish con el exe único (~173 MB) y **`Microsoft.FlightSimulator.SimConnect.dll` (172.032 B) y `SimConnect.dll` (85.504 B) sueltos** junto a `appsettings.json` y `Assets\`.
- Build Debug y Release de la solución: 0 errores, 0 advertencias (una MSB3026 transitoria por lock de otro proceso MSBuild en `Domain.dll`, desapareció al reintentar). `dotnet test`: **164/164 pasan**.
- **Verificado en producción por el usuario (2026-10-22)**: publish con SimConnect suelto funcionando en sesión real con MSFS/Fenix.

### 2026-10-22 — Seed de fallas de tierra: modelo probabilístico individual con desgaste del sistema padre (reemplaza el rango uniforme)

**Qué y por qué**

- El seed de tierra elegía primero una cantidad uniforme `Next(0, GroundFailuresCap)` y rellenaba al azar con máx. 1 falla por sistema. Ahora **cada falla Minor del catálogo hace su propio sorteo Bernoulli** y la cantidad emerge de los sorteos; `GroundFailuresCap` pasa a ser tope, no objetivo.
- **Probabilidad efectiva** (`RealisticFailurePolicy.GroundFailureProbability`): `p = clamp(p_base × (1 + k × wear_sistema_padre/100), 0, 1)`. La probabilidad **nunca se define por falla**: sale del perfil de riesgo, con desgaste del sistema padre amplificando.
- **Perfil** (`RealisticProfileSettings`): nuevos `GroundFailureBaseProbability` (p_base, igual para todas las Minor) y `GroundWearProbabilityGain` (k). Defaults en `RealisticProfileCatalog` — Low: 0.004/1.5, Moderate: 0.01/2, High: 0.015/2.5, Squawk7700: 0.025/3 (~170 Minor elegibles; esperanza bajo el cap por perfil). `SnapshotSettings` del engine ahora copia ambos campos (sin esto, el perfil clonado quedaba con p_base=0 y no se sembraba nada).
- **Truncado por cap** (`RealisticFailurePolicy.SelectGroundFailures`): si los elegidos superan el cap, prioriza **mayor desgaste → mayor p_base → azar** (en ese orden). Se elimina la regla "1 falla por sistema" (`usedSystems`): pueden salir 2+ del mismo sistema. Con cap ≤ 0 no se siembra (antes `Next(0, 0)` lanzaba `ArgumentException`).
- **Desgaste**: mapeo `FenixFailureSystem.AtaId ↔ AircraftWearableSystem.AtaId` con el wear de la aeronave de la sesión; `SystemWearDto` expone `AtaId` (mapeado en `UserAircraftService`) para resolverlo desde memoria. El `Probability` del candidato sintético pasa a llevar la p_efectiva (antes un `Next()` placeholder sin uso real).
- **Logs**: el seed reporta sembradas/candidatos, perfil, p_base y tope.

**Áreas tocadas**: `Domain/Services/RealisticProfileSettings.cs`, `Domain/Services/RealisticProfileCatalog.cs`, `Application/Session/RealisticFailurePolicy.cs` (nuevo `GroundFailureCandidate<T>`, `GroundFailureProbability`, `SelectGroundFailures`), `Application/Session/RealisticSessionEngine.cs` (`SeedGroundFailuresAsync`, `BuildGroundCandidatesAsync`, `SnapshotSettings`), `Application/DTOs/UserAircraftDto.cs`, `Infrastructure/Services/UserAircraftService.cs`, `UI/appsettings.json` (nuevas claves `GroundFailureBaseProbability`/`GroundWearProbabilityGain` en los 4 perfiles), `TestUnits` (`RealisticFailurePolicyGroundSeedTests` nuevo, `RealisticProfileCatalogTests`, `RealisticSessionEnginePersistenceTests`), `docs/realistic-mode/estado-y-diseno.md` (sección nueva), este CHANGELOG.

**Verificación**

- Build Debug de la solución: 0 errores, 0 advertencias. `dotnet test RealFenixFailures.TestUnits/RealFenixFailures.TestUnits.csproj`: **164/164 pasan** (10 nuevos: 4 de probabilidad, 7 de selección/truncado + defaults de catálogo).
- **No verificado**: integración real MSFS/Fenix (requiere sesión real); distribución del nuevo modelo en vivo pendiente de observación en la PC de testing (`\\PC-MATHIAS\RealFenixFailures`).

### 2026-10-20 — Suite de TestUnits compila y pasa 148/148: tests ajustados a la API vigente + 2 bugs de producción corregidos

**Qué y por qué**

- La suite **no compilaba** (30 errores) porque los tests quedaron con la API anterior a los commits que enlazaron el flight plan a la sesión y movieron las fallas de tierra a definiciones persistidas. Se ajustaron los tests a la API vigente: `RealisticSessionEngineTests`, `RealisticSessionEnginePersistenceTests` y `RealisticSessionManagerTests` construyen el engine con el `IFlightPlanRepository` nuevo (6º dep) y usan `StartNewAsync(RealisticModeContext, CT)` con `FlightPlanId`; `RealisticProfileCatalogTests` reemplaza los tests de `GroundRange` (eliminado) por resolución de `GroundFailuresCap` por perfil; `FlightSessionServiceTests` y `UserAircraftServiceTests` siembran un `FlightPlan` y usan `FlightSession.FlightPlanId` (FK Restrict; antes insertaban sesiones con FK 0 inexistente).
- **Fix real 1 — `FlightSessionService.ToResultDto`**: `DestinationIcao` se mapeaba desde `session.FlightPlan?.OriginIcao` (copia de la línea de origen, regresión del commit `5a826a7`); el resultado de sesión devolvía el origen como destino. Ahora lee `FlightPlan.DestinationIcao`.
- **Fix real 2 — `SimBriefOfpReader`**: `RouteText` se leía de `general/route` (inexistente) y quedaba siempre `null`; regresión del mismo commit que cambió la línea. Ahora vuelve a leer `route/text`, como el formato real del OFP.
- **Mock SimConnect recalibrado**: el commit `5eb8b03` retemporizó los keyframes del vuelo sintético (despegue en t=65s) pero dejó 2 tests con el timeline viejo (documentado como pendiente en ese mismo commit); se re-sincronizaron (takeoff t=60s, crucero t=165s, reinicio de vuelo t=45s).
- **Serilog**: los 4 tests de `SerilogDiagnosticsTests` apuntaban a un path de log inyectado por configuración, pero `ApplyDefaultConfiguration` (desde `d50119a`) escribe siempre en `%AppData%\RealFenixFailures\logs`; las aserciones pasan a leer los archivos reales rodantes (`realfenix-<fecha>.log`, `simconnect-diagnostic-<fecha>.log`) con tokens únicos por ejecución.

**Áreas tocadas**: `Application/Services/FlightSessionService.cs` (fix), `Integrations.SimBrief/Services/SimBriefOfpReader.cs` (fix), `TestUnits` (`RealisticSessionEngineTests`, `RealisticSessionEnginePersistenceTests`, `RealisticSessionManagerTests`, `RealisticProfileCatalogTests`, `FlightSessionServiceTests`, `UserAircraftServiceTests`, `MockSimConnectClientTests`, `SerilogDiagnosticsTests`), este CHANGELOG.

**Verificación**

- `dotnet test RealFenixFailures.TestUnits/RealFenixFailures.TestUnits.csproj`: **148/148 pasan, 0 fallos** (antes no compilaba; los 6 fallos en la última corrida compilable —4 Serilog + 2 Mock— quedaron fuera al recalibrar/redireccionar).
- No se verifica integración real MSFS/Fenix (requiere sesión real) ni se tocó comportamiento de los modos.

### 2026-10-20 — Seed debug temporal: aeronave de ejemplo con sesiones, fallas y desgaste (solo DEBUG)

**Qué y por qué**

- Para no cargar datos a mano cada vez que se regenera la base local en desarrollo (borrar la `.db` y `EnsureCreated` reconstruye el esquema vacío), se agrega un **seed temporal solo DEBUG**: `DebugSampleDataSeeder` (`Infrastructure/Services`, implementa `IDebugSampleDataSeeder` en `Application/Interfaces`). Siembra la aeronave **`LV-DBG`** (A320) con historial completo en una única transacción: 3 `FlightPlan`, 4 `FlightSession` (3 finalizadas + 1 abierta, que no cuenta para totales de horas/vuelos), 4 `TriggeredFailure` con `FenixFailureId` reales del catálogo en estados distintos del ciclo de vida (ResolvedByPilot, NotResolved, AutoResolved, Sent) y 6 `AircraftSystemWear` acumulado. Es idempotente (solo siembra si no existe `LV-DBG`).
- **Integración**: en `Infrastructure/DependencyInjection.cs` se registra bajo `#if DEBUG` como `IDebugSampleDataSeeder` y también como `IInitializerService -> DebugSampleDataSeeder`, para que corra después de `DatabaseSeeder` (catálogo+presets). `App.OnStartup` pasa a resolver `IEnumerable<IInitializerService>` e iterar en orden de registro (Application antes que Infrastructure ⇒ la FK de `TriggeredFailure.FenixFailureId` se garantiza tras el catálogo). En **Release** el seed no se registra ni se resuelve: no siembra nada. Los seeds estáticos y `DatabaseSeeder` no cambian; el seed debug es temporal y se retira quitando el bloque `#if DEBUG` y las clases.
- Los `FenixFailureId` de ejemplo (`F_FUEL_PUMP_LEFT_1`, `F_ENGINE_1`, `F_HYD_LEAK_GREEN`, `F_BRAKE_ABCU`) existen en `failures.json` (catálogo curado), así que las fallas siembran con FK válida.

**Áreas tocadas**: NUEVO `Application/Interfaces/IDebugSampleDataSeeder.cs`, NUEVO `Infrastructure/Services/DebugSampleDataSeeder.cs`, NUEVO `docs/realistic-mode/estado-y-diseno.md` (sección «Seed debug temporal»), `Infrastructure/DependencyInjection.cs`, `UI/App.xaml.cs` (resolución `IEnumerable<IInitializerService>`), `docs/arquitectura.md`, este CHANGELOG.

**Verificación**

- Build focalizado Debug: `RealFenixFailures.Infrastructure` **0 errores / 0 advertencias**; `RealFenixFailures.UI` **0 errores / 0 advertencias**. Build **Release** de `RealFenixFailures.Infrastructure`: **0 errores / 0 advertencias** (confirma que el `#if DEBUG` deja el habilitado sin seed).
- Verificación empírica con repositorios y catálogo reales sobre SQLite de archivo recién creada (`EnsureCreated`), replicando el orden real del arranque: catálogo **384 definiciones**; tras el seeder: **1 aeronave LV-DBG, 3 planes, 4 sesiones (3 finalizadas + 1 abierta), 4 `TriggeredFailure` (0 huérfanas respecto del catálogo), 6 desgastes**; re-ejecutar el seeder no duplica (idempotencia). Los estados y navegaciones de las fallas quedaron asociados a sus sesiones como se diseñó.
- `RealFenixFailures.TestUnits` **no compila** por WIP ajeno preexistente (ahora 30 errores: `StartNewAsync`/`RealisticModeContext` con `FlightPlanId`, etc., en `RealisticSessionEngineTests` y otros); ninguno en los archivos de este cambio. La suite completa queda pendiente de que ese WIP se consolide.

### 2026-10-20 — Seed unificado: DatabaseSeeder orquesta catálogo y presets; SeedData vuelve al seed estático

**Qué y por qué**

- Se retira el WIP aditivo de la aeronave de ejemplo `LV-SEED` (sesiones, fallas y desgaste): `SeedData` vuelve a su alcance original y solo conserva el seed estático de esquema (`PresetType` + 19 `AircraftWearableSystem`, vía `OnModelCreating`). `FailuresPersistenceService` recupera su constructor de 2 parámetros: ya no recibe `RealFenixDbContext` ni invoca el sembrado runtime de `TriggeredFailure` que dependía de la aeronave semilla.
- Nuevo **`DatabaseSeeder`** (`Application/Services`) como **punto único de orquestación** de los datos semilla del arranque: primero carga el catálogo de fallas (`IFailuresPersistenceService.LoadInitialFailuresAsync`) y luego los presets (`IPresetService.InitializeAsync`), reutilizando los servicios existentes. Implementa `IInitializerService`, de modo que `App.xaml.cs` no cambia: el arranque sigue resolviendo `IInitializerService` desde DI, ahora materializado por `DatabaseSeeder` (`AddScoped<IInitializerService, DatabaseSeeder>()`). Se elimina `InitializerService` (Application/Services), cuya responsabilidad (fallas + presets) queda absorbida por el seeder: un único lugar para orquestar todo dato semilla futuro.
- Pruebas ajustadas: `FailuresPersistenceServiceTests` vuelve a construir el servicio con 2 parámetros; `UserAircraftServiceTests` revierte los filtros por matrícula `LV-XYZ`/`LV-WEAR` (compensaban la presencia de `LV-SEED`) a `Assert.Single` sin filtro.

**Áreas tocadas**: NUEVO `Application/Services/DatabaseSeeder.cs`, ELIMINADO `Application/Services/InitializerService.cs`, `Application/DependencyInjection.cs`, `Infrastructure/Persistence/Seed/SeedData.cs`, `Infrastructure/Services/FailuresPersistenceService.cs`, `TestUnits` (`FailuresPersistenceServiceTests`, `UserAircraftServiceTests`), `docs/realistic-mode/estado-y-diseno.md`, `docs/arquitectura.md`, este CHANGELOG.

**Verificación**

- Build focalizado Debug: `RealFenixFailures.Application` **0 errores / 0 advertencias**; `RealFenixFailures.Infrastructure` **0 errores / 0 advertencias**.
- `RealFenixFailures.TestUnits` **no compila** por WIP ajeno ya documentado (22 errores: `RealisticProfileCatalog.GroundRange`, firmas `StartNewAsync`/`RealisticModeContext` con `FlightPlanId`; en `RealisticProfileCatalogTests`, `RealisticSessionEngineTests`, `RealisticSessionManagerTests`, `RealisticSessionEnginePersistenceTests`); ninguno en los archivos tocados por este cambio. Revalidar la suite completa cuando ese WIP se consolide.

### 2026-10-19 — Realistic: el engine reacciona a la confirmación de Fenix (evento + feed de estados)

**Qué y por qué**

- La reconciliación del ciclo de vida (`RealisticSessionEngine.ReconcileFailureLifecycleAsync`, backend ya existente del 2026-10-17) solo persistía las transiciones `Sent→Active` y `Active→ResolvedByPilot`; nada más se enteraba del cambio: `SessionEventCategory.FailureStateChanged` (enum 11) quedaba sin uso y el feed de la UI decía «Enviada (sin confirmación)» para siempre.
- **Engine**: por cada transición observada ahora también se registra un evento de sesión `FailureStateChanged` (códigos `FAILURE_CONFIRMED_ACTIVE` / `FAILURE_RESOLVED_BY_PILOT`); un fallo al registrar no aborta la reconciliación (la transición ya se persistió). Las transiciones se devuelven en `EvaluationResult.StateChanges` (`FailureStateChangeInfo`: FenixFailureId, descripción, estado nuevo, severidad, timestamp), en todos los resultados del tick (`WithStateChanges`).
- **Manager**: `IRealisticSessionManager` gana el evento `FailureStateChanged` (`FailureStateChangedEventArgs`) y lo despacha por cada `StateChanges` del resultado de evaluación.
- **UI**: `LiveTriggeredFailures` pasa de `ObservableCollection<string>` a `ObservableCollection<LiveFailureEntryViewModel>` (nuevo VM con estado + color); el feed muestra el estado confirmado y se actualiza en vivo (enviada amarillo, activa Minor/Major naranja, activa Critical rojo, resuelta verde; `Color/Brush Accent.Orange.Primary` nuevos). La entrada existente se actualiza por `FenixFailureId`; si no está en el feed, se inserta.

**Áreas tocadas**: `RealFenixFailures.Application` (EvaluationResult, RealisticSessionEngine, IRealisticSessionManager, RealisticSessionManager), `RealFenixFailures.UI` (RealisticModeViewModel, LiveFailureEntryViewModel, RealisticModePanel.xaml, DesignTokens/Brushes), `TestUnits` (RealisticSessionEngineTests) y `docs/realistic-mode/estado-y-diseno.md`.

**Verificación**

- `dotnet build RealFenixFailures.slnx -c Debug`: 0 errores, 0 advertencias.
- `dotnet test`: **141/147** — 35/35 de `RealisticSessionEngineTests`, incluyendo asserts nuevos de `StateChanges` y del evento `FailureStateChanged` en las transiciones Sent→Active y Active→ResolvedByPilot.
- **6 fallos preexistentes, ajenos a este trabajo**:
  - `SerilogDiagnosticsTests` (4): leen los logs de diagnóstico de `Directory.GetCurrentDirectory()/logs` pero el sink escribe en `%AppData%\RealFenixFailures\logs` desde el refactor `d50119a` (2026-10-19, "centraliza datos del usuario"); los tokens nuevos nunca aparecen en los archivos que el test inspecciona. Pendiente de la tarea de infraestructura.
  - `MockSimConnectClientTests` (2): el WIP de estabilización del mock en el árbol (keyframes del timeline reescritos, sin commitear) dejó descalibrados estos tests; los commiteará el agente principal.
- **No verificado**: integración real MSFS/Fenix (la confirmación por lectura solo puede validarse en sesión real).

### 2026-10-19 — Fallas de tierra: sin whitelist; el universo sale de las definiciones persistidas con severidad 1/2

**Qué y por qué**

- Se elimina `GroundFailureIds` de `RealisticModeSettings` (`FailureEngineSettings.cs`) y del bloque `FailureEngine:RealisticMode` de `appsettings.json`: era una whitelist de 9 fallas duplicada a mano, desactualizable respecto del catálogo curado (`failures.json` → `FenixFailureDefinition` con `Severity`).
- `RealisticSessionEngine.BuildGroundCandidatesAsync` reescrito sin whitelist ni iteración de presets: las candidatas de tierra son **todas las `FenixFailureDefinition` persistidas con severidad `Minor`/`Major` (1/2)** (excluidas `F_OH_NAV_IR_ALIGNMENT_*`, ajenas al engine), con `Group`/`System` cargados por `GetAllWithSystemsAsync`. Por cada una construye un `PresetFailureDefinition` sintético (`FenixFailureId` + `FenixFailure` = entidad persistida trackeada, `Probability = 1.0`, sin condiciones de armado); el seed escoge desde esa lista por rango de riesgo y sistema distinto (sin cambios en `SeedGroundFailuresAsync`).
- Al no mutar navegaciones (ya no se copia `Group`/`Severity` sobre definiciones de preset), el seed deja de depender de la reconciliación contra presets y el tracking compartido de `GetAllWithSystemsAsync` sigue evitando conflictos de identity en `SaveChanges`. `SnapshotSettings` no copia más la lista.
- El `PresetName` del item de tierra en cola/historial pasa a ser el nombre del **sistema** (`Group.System.Name`, p. ej. `ELEC`) en lugar del nombre del preset OnGate.

**Áreas**

- `RealFenixFailures.Domain/Services/FailureEngineSettings.cs`, `RealFenixFailures.Application/Session/RealisticSessionEngine.cs`, `RealFenixFailures.UI/appsettings.json`, `docs/realistic-mode/estado-y-diseno.md`. El test `RealisticSessionEnginePersistenceTests` fue adaptado a la nueva fuente (sin `GroundFailureIds`) pero sigue siendo parte de la tarea del fix de tracking (archivo sin commitear); no entra en este commit.

**Pruebas**

- Build Debug de la solución: 0 errores/0 advertencias.
- Suite completa en este entorno: **141/147**. 4 fallos preexistentes en `SerilogDiagnosticsTests` (ajenos, documentados en «Datos del usuario centralizados…») y 2 en `MockSimConnectClientTests` por WIP sin commitear de `MockSimConnectClient.cs` (dominio ajeno, los commiteará el agente principal).
- Focus del cambio: 36/36 en `RealisticSessionEngineTests` + `RealisticSessionEnginePersistenceTests` — el test de integración de tierra (`GroundSeed_OnGate_TriggeredFailurePersistsWithoutTrackingConflict`) arma el candidato desde la definición persistida en BD (severity Major) **sin** `GroundFailureIds` y persiste 1 `TriggeredFailure` (State `Sent`) + evento `FAILURE_TRIGGERED`.

### 2026-10-19 — Fix: seed de fallas de tierra no envenena el tracking de EF (fallas no persistían)

**Qué y por qué**

- `RealisticSessionEngine.BuildGroundCandidatesAsync` asignaba `def.FenixFailure.Group = entity.Group` tomando la navegación de una instancia **AsNoTracking** (del repo `FenixFailureDefinitionRepository.GetAllWithSystemsAsync`) sobre definiciones ya trackeadas por el contexto de la sesión (cargadas con tracking por `PresetRepository.GetAllAsync`). En el siguiente `SaveChanges`, `DetectChanges` → `NavigationFixer` intentaba adjuntar ese grafo desanclado y chocaba con las mismas claves ya trackeadas: `InvalidOperationException` ("cannot be tracked because another instance with the same key value").
- Efecto en producción (log `realfenix-20261019.log`, avion en gate con todo apagado): las fallas de tierra sembradas se disparaban a Fenix y el disarm funcionaba, pero **ninguna se persistía**; al detener la sesión, el evento SESSION_STOPPED y el `StopAsync` fallaban igual, dejando la sesión sin `FinishedAt` (queda como "sin terminar" en el próximo arranque).
- Fix (raíz): `GetAllWithSystemsAsync` deja de usar `AsNoTracking()` — la consulta comparte identidad con el contexto de la sesión y la asignación `def.FenixFailure.Group = entity.Group` se vuelve segura (misma instancia trackeada, sin AttachGraph de un grafo desanclado). Alternativa previa descartada (solo `GroupId` + `Severity`, sin navegación): dejaba `Group` null y rompía el seed de tierra (el bucket por sistema `Group.SystemId` caía a 0 → "Menos candidatos de tierra que el rango deseado: 1 de 5", observado en corrida real 03:50).
- Logging Fenix (mismo trabajo): `ArmFailureAsync`/`DisarmFailureAsync` logueaban el objeto `Stream` (`{@stream}`) y Serilog serializaba las properties del `MemoryStream` (`CanRead/Length/ReadTimeout` con accesors que tiran excepción) en vez del contenido; ahora se loguea el contenido real de la respuesta (`ReadResponseContent`). Además `FenixApiClient` logueaba errores sin la excepción (solo mensaje genérico, retornando `null`): ahora los six catch de `HttpRequestException`/`TaskCanceledException` pasan la excepción y su mensaje al log (el contrato de retorno `null` se mantiene).

**Áreas**

- `RealFenixFailures.Application/Session/RealisticSessionEngine.cs`, `RealFenixFailures.Infrastructure/Repositories/FenixFailureDefinitionRepository.cs`, `RealFenixFailures.Integrations.Fenix/Services/FenixApiFailureService.cs`, `RealFenixFailures.Integrations.Fenix/Services/FenixApiClient.cs`, TestUnits (nuevo `Session/RealisticSessionEnginePersistenceTests.cs`), `docs/realistic-mode/estado-y-diseno.md`.

**Pruebas**

- Test de regresión de integración (engine + repositorios reales sobre SQLite en memoria): seed de tierra en OnGate → flush → `AddTriggeredFailureAsync` persiste 1 `TriggeredFailure` (State `Sent`) y el evento `FAILURE_TRIGGERED`. **Red**: sin el fix, la falla no se persiste (la excepción de tracking se traga en el flujo de compensación); **verde**: con el fix, 1/1 pasa.
- Suite: **143/147** en este entorno. 4 fallos restantes en `SerilogDiagnosticsTests` **preexistentes y ajenos a este cambio** (los tests buscan `simconnect-diagnostic-*.log` en `<CWD>/logs`, pero el sink escribe en `AppPaths.LogsDirectory` = `%AppData%\RealFenixFailures\logs` desde el refactor `d50119a`; requieren arreglo aparte).
- Build Debug de la solución: 0 errores/0 advertencias.
- Corrida real del usuario (03:50, Release con el fix `GroupId`): el conflicto de tracking desapareció (`Failure triggered: F_DOOR_FWD_ENTRY_LEFT` sin excepción), pero el seed quedó en "1 de 5" por el efecto del `Group` null → de ahí el cambio al fix de raíz (tracking). **Pendiente**: nueva corrida real con el fix de raíz para confirmar seed 3-5 + persistencia + logs de respuesta Fenix legibles.


### 2026-10-19 — Datos del usuario centralizados en %AppData% y appsettings limpio

**Qué y por qué**

- Nueva ubicación única de datos del usuario: **%AppData%\RealFenixFailures** (BD SQLite `realfenixfailures.db`, preferencias `userprefs.json`, logs `logs\realfenix-.log` y `simconnect-diagnostic-.log`), centralizada en `RealFenixFailures.Infrastructure/AppPaths.cs`. Antes la BD y los logs quedaban relativos a la carpeta del ejecutable/CWD (frágil ante reinstalaciones, OneDrive y carpetas protegidas como Program Files). La carpeta base ya la usaba el panel de Ajustes (`UserPreferencesService`); ahora se reutiliza para todo.
- `appsettings.json` limpio: se quitan `ConnectionStrings:Sqlite` (la ruta se calcula en código), los nombres de catálogos embebidos `FailureEngine:FailuresJson/TrainingPresetsJson/RealisticPresetsJson` (pasan a constantes en `RealFenixFailures.Domain/Services/EmbeddedResourceNames.cs`; no son configurables), `FailureEngine:GlobalProbabilityMultiplier` (no lo lee ningún código) y el bloque `Serilog:WriteTo` (el sink de archivo se configura en `SerilogExtensions` con la ruta de `AppPaths`).
- Ajustes de capas: `FailuresPersistenceService` y `PresetsLoader` dejan de recibir `IOptions<FailureEngineSettings>` (nombres de recursos ahora constantes); `UserPreferencesService` usa `AppPaths`. `RealFenixDbContextFactory` (design-time) usa la misma ruta.
- Docs: `docs/desarrollo.md` (nueva sección "Datos del usuario" + checklist "Publicar una versión (v1 en adelante)": migración inicial → `MigrateAsync()`, versión + tag, verificación de %AppData%, ZIP portable, appsettings limpio), `AGENTS.md`, `arquitectura.md`, `simconnect.md`.

**Pruebas**

- `appsettings.json` validado como JSON sintácticamente correcto (parseo con Python).
- Build **no verificado** en este entorno (restore NuGet incompleto por red; mismo bloqueo del cambio anterior). Compilar en la máquina del desarrollador antes de declararlo funcionando. Cambio de comportamiento de datos (migración de BD/logs a %AppData%) requiere verificación manual en sesión real.

### 2026-10-19 — Persistencia de severity y repairable en el cargado inicial

**Qué**

- `FailuresPersistenceService.LoadInitialFailuresAsync` ya **no fuerza `FailureSeverity.Minor`**: lee `severity`/`repairable` del catálogo curado `failures.json` y persiste `Severity` + `CanBeRepaired` (columna nueva) en cada `FenixFailureDefinition` al hacer seed inicial.
- Cadena completa: `FenixManualFailure` (+`Severity` int default 1, `Repairable` default false) → `FenixFailureDto` (+`Severity` enum default `Minor`, `Repairable`) → `FenixMappers` (cast del int al enum) → `FailuresPersistenceService` → repo.
- Esquema: `CanBeRepaired` con `HasDefaultValue(false)` en `RealFenixDbContext`; con la política dev (EnsureCreated), se borró la DB local para que regenere con la columna y el seed curado.
- Test nuevo de integración `FailuresPersistenceServiceTests` (Sqlite en memoria + JSON embebido real): seed de 384 definiciones con distribución 173/179/32 y 20 reparables, más casos puntuales (`F_ENGINE_1` Major+reparable, `F_OH_NAV_IR_ALIGNMENT_1` Minor+reparable, `F_ELEC_BUS_DC_ESSENTIAL` Critical+no reparable).

**Por qué**

- Cerrar la tarea del catálogo curado: la base debe reflejar severidad y reparabilidad para que el motor (y los topes por severidad de los perfiles) decidan sobre datos reales, no sobre todo-Minor.

**Áreas**

- Domain (`FenixFailureDto`, `FenixFailureDefinition`), Integrations.Fenix (`FenixManualFailure`, `FenixMappers`), Infrastructure (`FailuresPersistenceService`, `RealFenixDbContext`), TestUnits (`FailuresPersistenceServiceTests`), `docs/realistic-mode/estado-y-diseno.md`.

**Pruebas**

- **146/146 tests** (145 previos + el nuevo de integración end-to-end del seed); build Debug y Release con 0 errores/advertencias. **No verificado**: corrida manual de la app real (el gate aisló el exe; el seed se ejercitó vía test de integración sobre el mismo JSON embebido, reader y repo de producción).

### 2026-10-19 — Versionado central + chequeo de actualizaciones en Ajustes

**Qué y por qué**

- **Versionado único de la solución**: nuevo `Realistic Failures Solution/Directory.Build.props` con `<Version>0.1.0-dev.0</Version>`. El SDK lo propaga a los 8 proyectos: `AssemblyVersion` deriva a 0.1.0.0 y `InformationalVersion` conserva el sufijo `0.1.0-dev.0` (lo que muestra la UI). Antes no había versión gestionada: el SDK ponía `1.0.0` por defecto, que es lo que mostraban Ajustes (`SettingsWindowViewModel.AppVersion`) y los crash logs (`CrashLogWriter`). Al publicar un release, el tag `vX.Y.Z` de GitHub debe coincidir con este archivo.
- **Chequeo real de actualizaciones**: la pestaña "Actualizaciones" de Ajustes dejó de ser solo un botón que abre GitHub Releases. Ahora `IUpdateChecker` (contrato en Application) → `GithubUpdateChecker` (UI/Services) consulta la API pública `GET https://api.github.com/repos/mathi0101/Fenix-Failures-Dispatcher/releases?per_page=5` (sin token, con User-Agent propio, timeout 15 s). Incluye pre-releases: mientras la app se comparte como builds de prueba, la publicación más reciente suele ser pre-release y se ofrece igual.
- `SettingsWindowViewModel`: compara versión instalada (InformationalVersion, conserva el sufijo `-dev`) vs. el tag más reciente con parseo SemVer (tolera `vX.Y.Z-prefijo`); una versión `-dev` queda obsoleta cuando se publica cualquier release (incluso pre-release). `AppVersion` usa `AssemblyInformationalVersionAttribute` (antes `AssemblyVersion`, que pierde el sufijo).
- `UpdateCheckState` (enum): Idle / Checking / UpToDate / NewVersionAvailable / NoReleases / Error. El XAML colorea con DataTriggers (verde=al día, ámbar=hay nueva, rojo=error) y `BooleanToVisibilityConverter` (nuevo recurso local) muestra la barra de progreso y el botón "Descargar e ir a GitHub Releases" cuando hay versión nueva.
- DI: `services.AddSingleton<IUpdateChecker, GithubUpdateChecker>()` en `HostBuilderExtensions`.

**Pruebas**

- `<Version>` propagado verificado con `dotnet msbuild RealFenixFailures.UI -getProperty:Version` → `0.1.0-dev.0`.
- **Build NO verificado**: el `dotnet restore`/`build` de la solución no pudo completarse dentro de este entorno de trabajo por restore de NuGet incompleto desde la red (`NETSDK1064: Package Microsoft.CodeAnalysis.Analyzers ... was not found`; el caché local del sandbox estaba vacío). El código quedó listo y sin errores aparentes, pero **compilar debe hacerse en la máquina del desarrollador** antes de declararlo funcionando.

**Áreas**

- NUEVO `Realistic Failures Solution/Directory.Build.props`.
- NUEVO `Application/DTOs/ReleaseInfoDto.cs`, `Application/DTOs/UpdateCheckResultDto.cs`, `Application/Interfaces/IUpdateChecker.cs`.
- NUEVO `UI/Services/GithubUpdateChecker.cs`, `UI/ViewModels/UpdateCheckState.cs`.
- `UI/DependencyInjection/HostBuilderExtensions.cs`, `UI/ViewModels/SettingsWindowViewModel.cs`, `UI/Views/Panels/SettingsPanel.xaml`.
- `docs/ui/configuracion-y-preferencias.md`, este CHANGELOG.

### 2026-10-19 — Política EOL: normalización LF de textos con .gitattributes/.editorconfig

**Qué y por qué**

- Raíz del repo: `.gitattributes` (`* text=auto eol=lf`; `*.bat`/`*.cmd` `text eol=crlf`; binarios `-text`: `dll`, `exe`, `png`, `jpg`, `jpeg`, `gif`, `ico`, `pdf`, `zip`) y `.editorconfig` (`root=true`, `[*] end_of_line=lf`, `[*.bat]`/`[*.cmd]` `end_of_line=crlf`). No imponen indentación, trim de espacios, BOM, encoding ni nueva línea final.
- Config local: `core.autocrlf=false` y `core.eol=lf`; no se tocó la configuración global ni `core.safecrlf`.
- Conversión mecánica CRLF→LF (por bytes, exclusivamente saltos de línea) de **298** textos del árbol de trabajo (tracked + untracked no ignorados; incluye WIP Realistic y skills `.abacusai`); **59** ya estaban en LF; **7** binarios omitidos (declarados `-text`); 0 excluidos por NUL/UTF-16 en textos (los 7 archivos con NUL son binarios de extensión declarada). No hay `.bat`/`.cmd` en el árbol. Se preservan BOM, encoding, bytes no salto y línea final. Manifiesto previo a la mutación con sha256 original y normalizado (CRLF→LF) de los 364 archivos baseline.
- Docs: sección de política EOL en `docs/desarrollo.md` y actualización puntual de `AGENTS.md` (enlace a la política); esta entrada. No se tocó el índice real de git ni se ejecutó `git add --renormalize`.

**Verificación (cambio exclusivamente de bytes de salto de línea; sin build ni tests declarados)**

- Post-conversión: 364/364 hashes coinciden con el manifiesto (normalizados para textos, originales para binarios); 0 violaciones CRLF fuera de las excepciones; binarios (7/7) con sha256 idéntico al baseline.
- Prueba de índice temporal (`GIT_INDEX_FILE` y `GIT_OBJECT_DIRECTORY` temporales con alternates a los objetos originales): `git read-tree HEAD`, `git add -A -- .` y `git diff --cached --check` con 0 bytes de stderr, sin warnings de conversión; el mismo procedimiento sin la nueva política emitía ~10 KB de warnings. Índice real sin cambios: sha256 del archivo de índice idéntico antes y después.
- Los 3 docs editados después de la normalización (`AGENTS.md`, `docs/desarrollo.md`, este CHANGELOG) y los 2 configs nuevos quedan fuera de la comparación de hashes por cambio de contenido/ser nuevos; verificada su ausencia de CRLF.
- Tras la conversión, `git status` conserva marcas de modificación para archivos sin diferencias de contenido en `git diff`. La comparación con un índice temporal limpio apunta a metadatos del índice anterior; no se confirmó una causa específica de OneDrive. El índice real se preservó para no mezclar ni stagear el trabajo Realistic en curso. La integración de la normalización al próximo commit debe revisarse junto con ese trabajo.

### 2026-10-18 — SimBrief: timestamps Unix del bloque times y OFP de referencia

**Qué y por qué**

- `SimBriefOfpReader.ParseUtc` interpreta segundos Unix con `DateTimeOffset.FromUnixTimeSeconds(...).UtcDateTime`, mantiene fechas textuales UTC/offset y devuelve `default` para valores fuera de rango sin lanzar excepción. Antes los timestamps numéricos se perdían y se reemplazaban por fechas calculadas desde la hora de descarga.
- Mapeo del bloque real `times`: off-block de `sched_out` (alternativos por elemento ausente: `sched_off`, `orig`), llegada estimada de `est_on` (alternativos: `sched_on`, `dest`); duración de `est_time_enroute` en segundos convertida a minutos. Los límites de validez reciben los mismos instantes UTC. Se conserva el fallback de ICAO del mock anterior (`general/aircraft_icao`) sin reemplazar la lectura real de `aircraft/icaocode` ya presente en el árbol de trabajo.
- Captura completa aportada por el usuario guardada sin modificaciones en `docs/integraciones/ejemplos/simbrief-ofp-SBSP-SUMU-2026-10-18.txt`. La extensión es intencional: el HTML `<br>` sin cerrar de la captura impide parsearla como XML completo. Se documentan esta limitación, las unidades, los campos y la semántica de los horarios en `docs/integraciones/simbrief.md`; enlace desde el estado del modo Realistic.

**Pruebas**

- Regresión antes del fix: `dotnet test RealFenixFailures.TestUnits/RealFenixFailures.TestUnits.csproj --filter FullyQualifiedName~SimBriefOfpReaderTests --no-restore --verbosity minimal`: **9 fallidas / 8 correctas**; el bloque real no producía los horarios esperados y la prueba existente de ICAO evidenciaba incompatibilidad con el mock anterior.
- Después: **17/17** pruebas focalizadas correctas; bloque `times` suministrado, duración, alternativa programada, fechas textuales, epoch cero/negativo, espacios, offset y valores inválidos/fuera de rango. Captura guardada comparada byte a byte con el OFP del prompt.
- Build Debug y Release de `RealFenixFailures.slnx` (`--no-restore`): **0 errores / 0 advertencias**. Suite Release completa: **115/115** correctas.
- Comprobación previa al commit: Release nuevamente con **0 errores / 0 advertencias**, suite **115/115** y auditoría de dependencias directas/transitivas sin paquetes vulnerables según las fuentes configuradas. El nuevo intento Debug quedó bloqueado por DLL en uso por `RealFenixFailures.UI` y Visual Studio (`MSB3027`/`MSB3021`); no se cerraron procesos del usuario. El build Debug exitoso anterior corresponde a la ejecución inicial del trabajo.
- Descarga HTTP real, UI y sesión MSFS/Fenix: **no verificadas**. No se amplió el parser para aceptar XML mal formado.

### 2026-10-18 — Fix: crash de cierre por Close() reentrante en el modal

**Qué**

- Al elegir «Cerrar la app por completo» en `CloseChoiceWindow`, `MainWindow_Closing` llamaba a `Close()` dentro del propio evento `Closing` (la ventana ya estaba procesando `WM_CLOSE`): `InvalidOperationException` — «Cannot set Visibility to Visible or call Show, ShowDialog, Close, or WindowInteropHelper.EnsureHandle while a Window is closing» — en `MainWindow.xaml.cs:58`, que llegaba al crash reporter y cerraba la app con cartel y log (`crash-20261018-143622-468.log`).
- El `Close()` del `case Shutdown` se difiere hasta después de que el handler de `Closing` termina, con `Dispatcher.BeginInvoke(new Action(Close))` (cambio de 1 línea): `IsExitAllowed` ya está en `true`, el `Closing` reentrante disparado por el cierre diferido retorna sin cancelar y la ventana cierra sin excepción. Sin cambios en cancelar, segundo plano/bandeja ni lógica del motor.

**Por qué**

- `Window.Close()` no puede ejecutarse mientras la ventana está en cierre (reentrancia dentro de `Closing`); diferirlo resuelve la causa raíz sin refactor del ciclo de vida.

**Áreas**

- `RealFenixFailures.UI/Views/MainWindow.xaml.cs` (1 línea), `docs/ui/manejo-de-errores-y-arranque.md` (sección nueva), este CHANGELOG.

**Pruebas**

- Harness WPF externo al repo (`%TEMP%\RFCloseHarness`): fuente real de `MainWindow.xaml.cs` linkeada (sin proxy), stubs de `MainWindowViewModel`, `TrayIconService`, `InitializeComponent` y `SettingsPanelHost` para no arrancar la app ni la BD; `Application` con `ShutdownMode` explícito en hilo STA, cierre disparado con `BeginInvoke` y botón del modal accionado por reflexión.
  - **Antes del fix (rojo)**: se reproduce la excepción reentrante exacta (mismo mensaje y punto que el log de crash), exit code 1.
  - **Después del fix (verde)**: Cancelar → ventana visible y no cerrada; Segundo plano → oculta, no cerrada, aviso de bandeja; Cerrar app → `Closed` sin excepción. Exit code 0.
- Build Release de la solución: 0 errores/0 advertencias; `dotnet test RealFenixFailures.TestUnits/RealFenixFailures.TestUnits.csproj -c Release`: **99/99**; `git diff --check` limpio.

### 2026-10-17 — Modal de confirmación al cerrar la ventana principal

**Qué**

- Al hacer clic en la X (o Alt+F4), ya no se pasa a segundo plano de forma inmediata: se muestra un modal (`CloseChoiceWindow`) con tres opciones:
  - **Seguir en segundo plano**: la app queda en la bandeja (comportamiento anterior) y muestra el aviso de bandeja.
  - **Cerrar la app por completo**: detiene el motor de fallas y sale (equivale a "Salir" desde la bandeja).
  - **Cancelar**: vuelve a la ventana sin cambios.
- `MainWindow.MainWindow_Closing` coordina la elección (flag de reentrada para no abrir el modal dos veces); `TrayIconService` se inyecta en `MainWindow` para el aviso de segundo plano.

**Pruebas**

- Build Release 0 errores/advertencias y **99/99 tests**. No se probó manualmente el flujo visual del modal (requiere ejecutar la app); pendiente de revisión en vivo.

### 2026-10-17 — Fix OFP: mock con fechas dinámicas y feedback si falta userid

**Qué**

- `MockSimBriefApiClient` (DEBUG) generaba el OFP con fechas fijas del 2026-10-17, por lo que al pasar el día el plan siempre quedaba `Expired` («venció») y nunca se podía validar/activar. Ahora genera fechas dinámicas (`UTC+30 min` offblock, `+95 min` eta) para que el OFP sintético siempre quede dentro de la ventana en desarrollo.
- `CanDownloadOfp` ya no exige userid: el botón «Descargar OFP» queda habilitado con aeronave seleccionada. Si no hay userid de SimBrief configurado en Ajustes, `DownloadOfpAsync` muestra el error explicativo en el estado del OFP («Configurá tu userid de SimBrief en Ajustes…») en lugar de no hacer nada.
- `DownloadOfpCommand.RaiseCanExecuteChanged()` al seleccionar/deseleccionar aeronave (antes no se notificaba el cambio de habilitación).

**Pruebas**

- Build Release 0 errores/advertencias y **99/99 tests**. Diagnóstico por lectura del código (fechas fijas vs `DateTime.UtcNow`); no verificado con la API real de SimBrief (pendiente).

### 2026-10-17 — Front Realistic (Paso 3): userid de SimBrief desde Ajustes

**Qué**

- `RealisticModeViewModel.SimbriefUserId` pasa de campo editable (TextBox en el Paso 3) a getter de `UserPreferencesService` (el userid de SimBrief se configura en **Ajustes**; ya no se edita desde el modo Realistic). `CanDownloadOfp` y `DownloadOfpAsync` usan ese valor.
- El botón «Descargar OFP» queda dentro del bloque «OFP y ruta», abajo a la izquierda, más chico (26 px) y con icono de descarga (glifo Segoe MDL2). Mantiene el tooltip explicativo. Se quitó el TextBox del userid del Paso 3.

**Pruebas**

- Build Release 0 errores/advertencias y **99/99 tests**. Sin verificación en sesión real.

### 2026-10-17 — Backend Realistic (Fase 8): resultados de sesión y recuperación

**Qué**

- `ResultSummaryDto`, `FailureResultDto` y `SessionResultDto` (DTOs del modal de resultados, diseño objetivo §13): conteos por estado (`ActiveAtEnd`, `ResolvedByPilot`, `AutoResolved`, `Rejected`), por severidad (`Minor/Major/Critical`) y detalle por falla (estado, fuente de resolución, fase, fecha).
- `FlightSessionDto` ampliado: `OriginIcao`, `DestinationIcao`, `OffBlockUtc` (del `FlightPlan` asociado) y `Result`. `FlightSessionItemViewModel` del historial muestra la ruta del OFP (`OfpStatusDisplay` ya no es "No registrado" hardcodeado) y `ResultDisplay`/`ResolvedDisplay`.
- `IFlightSessionService.GetResultAsync(sessionId)`, `GetUnfinishedSessionsAsync()` y `FinalizeSessionAsync(sessionId, finishedAt)`; `IFlightSessionRepository.GetUnfinishedAsync` + impl (incluye `FlightPlan`). `UserAircraftService` incluye `FlightPlan` y mapea ruta/resultado.
- `FinalizeSessionAsync` marca `FinishedAt` si la sesión está abierta y devuelve el resultado (base de la tarjeta "sesión sin finalizar" del front).

**Por qué**

- Diseño objetivo §6 y §13: historial con info de OFP, modal de resultados y recuperación de sesiones abiertas al reabrir la app.

**Áreas**

- Application (DTOs, `IFlightSessionService`, `FlightSessionService`), Infrastructure (`IFlightSessionRepository`, repo, `UserAircraftService`), UI (`FlightSessionItemViewModel`), tests.

**Pruebas**

- Build Release 0 errores/advertencias y **99/99 tests** (4 nuevos: resumen por estado/severidad, resultado nulo para sesión inexistente, finalización marca `FinishedAt`, sesiones sin finalizar). Pendiente: modal de resultados y "finalizar sesión" en la UI (contrato listo); sin verificación en sesión real.

### 2026-10-17 — Backend Realistic: pausa que desarma en Fenix + reanudar con restauración opcional

**Qué**

- `RealisticSessionManager.PauseAsync` ahora **desarma en Fenix** las fallas activas/armadas de la sesión (`DisarmFailureAsync` por falla, desde `ITriggeredFailureRepository.GetActiveForSessionAsync` + `Session.FailedFailures/ArmedFailures`); el motor sigue pensando en memoria. Fallas no desarmadas quedan en log (no aborta la pausa). Requisito del diseño objetivo §6 (el ATC puede pedir terminar la emergencia; la pausa es la vía).
- `ResumeAsync(ct, restoreFailures)` (nuevo parámetro opcional, compatible): con `restoreFailures=true` re-arma las fallas en curso reconstruyendo la condición de armado desde la BD (`BuildArmRequestFrom`) vía `IFenixFailureApiDispatcher.ArmFailureAsync`. Best-effort: si la condición no se reconstruye, la falla queda sin re-armar y se registra.
- `IRealisticSessionManager.ResumeAsync` ampliado; `RealisticModeViewModel.RestoreFailuresOnResume` (default true) alimenta el parámetro en `ResumeSessionAsync`.

**Por qué**

- La pausa del modo Realistic no debe dejar fallas activas en el avión mientras el piloto sigue volando; reactivar debe ofrecer restaurarlas (diseño objetivo §6 y §14).

**Áreas**

- Application (`IRealisticSessionManager`, `RealisticSessionManager`), UI ViewModel (`RealisticModeViewModel`), tests del manager.

**Pruebas**

- Build Release 0 errores/advertencias y **95/95 tests** (2 nuevos: Pausa desarma falla activa en Fenix; Reanudar con restauración no lanza y mantiene la sesión). El desarme/armado real contra Fenix sigue pendiente de verificación en sesión real (los mocks de DEBUG no lo cubren).

### 2026-10-17 — Backend Realistic: ciclo de vida de fallas con confirmación por Fenix

**Qué**

- `RealisticSessionEngine.ReconcileFailureLifecycleAsync` (por lectura del estado de Fenix, diseño objetivo §10): cada tick transiciona `Sent→Active` cuando `failed=true` (confirma `FenixConfirmedAtUtc`) y `Active→ResolvedByPilot` cuando `failed=false` y ya no está armada (el piloto la despejó; persiste `ResolvedAtUtc`, `ResolvedPhase`, `ResolutionSource=Pilot`).
- `FlightSessionService.UpdateTriggeredFailureStateAsync` persiste la transición usando el rastreo EF.
- `ITriggeredFailureRepository`/`TriggeredFailureRepository`: nuevos `GetActiveForSessionAsync` (no-tracking) y `GetActiveForUpdateAsync` (rastreo para transicionar).
- `RealisticSessionEngine` gana dependencia `ITriggeredFailureRepository` (inyectada por el manager en `StartNewSessionAsync`); tests del engine y del manager actualizados al nuevo constructor.

**Por qué**

- Es el corazón del modo Realistic: pasar de "falla enviada" a "confirmada activa" y detectar la resolución por piloto sin necesidad de un evento de Fenix.

**Áreas**

- Application (`RealisticSessionEngine`, `FlightSessionService`, `IFlightSessionService`, `RealisticSessionManager`), Domain/Infrastructure (`ITriggeredFailureRepository`, `TriggeredFailureRepository`), tests.

**Pruebas**

- Build Release 0 errores/advertencias y **93/93 tests** (2 nuevos: Sent confirmada por Fenix → Active; Active despejada por piloto → ResolvedByPilot). El comportamiento real de Fenix (cómo responde a failed=true/false) ya está documentado en `docs/integraciones/fenix-api.md` y confirmado manualmente; el mapeo de estados en sesión real sigue pendiente de prueba en vivo.

### 2026-10-17 — Front Realistic (Paso 3): enganche del OFP/SimBrief de punta a punta

**Qué**

- `RealisticModeViewModel` inyecta `ISimbriefOfpService` (backend de la Fase 2). El botón "Descargar OFP" del Paso 3 pasa de `IsEnabled="False"` (backend pendiente) a ejecutar `DownloadOfpAsync` real: toma el `userid` (campo editable `SimbriefUserId`) y el `IcaoTypeCode` de la aeronave seleccionada, descarga/valida/persiste y aplica el resultado al ViewModel.
- `OfpValidated`, `RouteDisplay` y `OfpStatusDisplay` dejan de ser valores hardcodeados y se alimentan del `FlightPlanDto` devuelto (`ValidationState`: Válido/Pendiente/Inválido/Fuera de ventana con mensaje). `SimbriefUserId` notifica a `DownloadOfpCommand` (se habilita al haber userid + aeronave). `IsOfpDownloading` deshabilita la descarga durante el proceso.
- La activación del modo Realistic queda habilitada sólo con OFP válido en ventana (`CanActivate` ya exigía `OfpValidated`; ahora se cumple de verdad).
- En DEBUG se usa el mock de SimBrief (OFP sintético); en Release, HTTP real.

**Por qué**

- Es el requisito del diseño objetivo: sesión por OFP de SimBrief. Cierra de punta a punta el flujo de preparación del Paso 3.

**Áreas**

- `RealisticModeViewModel.cs` (inyección, `DownloadOfpAsync`, `ApplyOfpResult`, propiedades OFP reales, `SimbriefUserId` editable), `RealisticModePanel.xaml` (botón + campo userid), `docs/realistic-mode/estado-y-diseno.md`.

**Pruebas**

- Build Release 0 errores/advertencias; **91/91 tests** (los 3 de OFP siguen pasando; el enganche es UI que se verifica manualmente). Sin prueba manual contra la API real de SimBrief (pendiente; DEBUG usa mock). La ventana completa no se ejercitó en vivo (requiere ejecutar la app).

### 2026-10-17 — Backend Realistic (Fase 2): módulo OFP/SimBrief completo

**Qué**

- Nueva integración `RealFenixFailures.Integrations.SimBrief`: `SimBriefApiClient` (HTTP real por `userid`, base `https://www.simbrief.com/api/xml.fetcher.php`), `MockSimBriefApiClient` (DEBUG con OFP sintético), `SimBriefApiOptions` (`SimBriefApi:BaseUrl`, `DownloadTimeoutSeconds`) y `SimBriefOfpFetcher` que adapta el cliente al contrato `IOfpFetcher` de la capa de aplicación.
- `SimBriefOfpReader` (implementa `ISimBriefOfpReader` de Domain): parsea el XML del OFP a `FlightPlan` (aerolínea, número, ICAO, horarios UTC, duración, ruta, raw) sin validar.
- `SimbriefOfpService` (Application): descarga → valida ICAO == aeronave registrada, campos mínimos (origen/destino/duración) y ventana UTC (`RealisticMode.Ofp` PreStart −2 h / PostArrival +2 h) → persiste `FlightPlan` → devuelve `FlightPlanDto` con `ValidationState`/mensaje.
- Persistencia `IFlightPlanRepository` → `FlightPlanRepository` (Add, GetById, GetLatestForAircraftType). Registrado en DI; sección `SimBriefApi` en appsettings; csproj nuevo agregado a la solución, UI y TestUnits.
- El userid de SimBrief NO se guarda en BD (vive en userprefs): privacidad; `FlightPlan` no lo almacena.

**Por qué**

- El OFP es un requisito del diseño objetivo (sesión por OFP de SimBrief) y el único habilitador de la activación del modo Realistic en el front.

**Áreas**

- `Integrations.SimBrief` (nuevo), `Application` (DTO `FlightPlanDto`, interfaz y `SimbriefOfpService`), `Domain` (`IFlightPlanRepository`, `ISimBriefOfpReader`), `Infrastructure` (`FlightPlanRepository`), `UI` (HostBuilderExtensions + appsettings), `TestUnits` (3 tests), solución.

**Pruebas**

- Build Release 0 errores/advertencias; **91/91 tests** (3 nuevos: parser del OFP mapea campos, OFP válido en ventana persiste y usable, ICAO distinto inválido). El enganche del front (botón "Descargar OFP") queda para la etapa contigua. Sin prueba manual contra la API real de SimBrief todavía.

### 2026-10-17 — Campo `repairable` en las 384 fallas del catálogo (reparables en vuelo)

**Qué**

- Cada falla de `failures.json` lleva ahora `"repairable": true|false`, **independiente de la severidad** (ningún valor de severidad se modificó en esta pasada; el motor decidirá más adelante cómo combinar ambos). Define si la función normal del sistema puede restaurarse **durante el vuelo** (reset de computadora, relight de motor, realineación de ADIRs, reintento de arranque); `false` por defecto (mantenimiento en rampa / persiste).
- Distribución: **20 true / 364 false**. True: `F_ENGINE_1/2`, `F_OH_FLT_CTL_*_RECOVERABLE_*` (FAC/ELAC/SEC resettable), `F_MCDU_1/2_RECOVERABLE`, `F_START_FAULT_*_HOT/HUNG/NO_IGNITION` y `F_OH_NAV_IR_ALIGNMENT_*` (se resuelven al alinear; externas). Excluidas deliberadamente: `Engine surge`, `Bird strike`, `Engine failure with damage`, `Fire … extinguished with N bottles` (estado posterior, no acción del piloto) y `Start valve` (hardware atascado).
- Diff aditivo: 384 líneas `repairable`, 0 eliminaciones (el campo `severity` quedó intacto; CRLF y formato preservados).

**Por qué**

- El catálogo necesita el atributo de reparabilidad por separado: hay fallas que se autodescriben (recoverable/resettable, con daño, bird strike) y otras que el evento no revela (engine failure = posible relight). El engine podrá usar severidad y reparabilidad de forma independiente.

**Áreas**

- `RealFenixFailures.Domain/Resources/failures.json`, `docs/realistic-mode/estado-y-diseno.md` (sección «Campo repairable»). Sin cambios de código.

**Pruebas**

- JSON válido y 384/384 con `severity`+`repairable`; diff verificado aditivo; **build Debug 0 warnings y 88/88 tests** (el parser de Fenix ignora el campo desconocido). Asignación por conocimiento de sistemas, sujeta a validación del usuario (archivo `severidad_reparabilidad_revision.tsv`); no probada en simulación. Persistencia en DB sigue pendiente.

### 2026-10-17 — Severidad de fallas del catálogo (campo `severity` en las 384 fallas)

**Qué**

- Las 384 fallas de `failures.json` (EmbeddedResource versionado) llevan ahora `"severity": 1|2|3`, asignada con la escala operativa documentada en `docs/realistic-mode/estado-y-diseno.md` («Severidad de fallas del catálogo»): 1 Minor (respaldo/redundancia absorbe o informativo), 2 Major (ECAM actions y vuelo con limitaciones), 3 Critical (desvío/emergencia o control comprometido). Reglas de desempate: redundancia del A320 como guía, control de vuelo manda, fuego/humo/descompresión → 3, informativo → 1, duda → el menor.
- Distribución: **173 Minor, 179 Major, 32 Critical**; detalle por ATA en el doc y archivo de revisión completo (`severidad_propuesta_revision.tsv`, 384 filas id|ATA|grupo|título|severidad) para validación por el usuario.
- `IRS Alignment` (IR1/2/3) con severidad 1 pero marcadas como fallas de procedimiento externas al engine (no armables); ver sección de externas.
- El JSON solo ganó las 384 líneas `"severity"` (diff aditivo, 0 eliminaciones), preservando EOL CRLF y formato.

**Por qué**

- Tarea pendiente de la Fase 1: el catálogo curado por id ahora puede alimentar `FenixFailureDefinition.Severity` sin el `Minor` forzado actual.

**Áreas**

- `RealFenixFailures.Domain/Resources/failures.json`, `docs/realistic-mode/estado-y-diseno.md`, `docs/CHANGELOG.md`. Sin cambios de código.

**Pruebas**

- Validación estructural del JSON (384/384 asignadas, archivo legible); diff verificado (384 insertions, 0 deletions). **Pendiente (no ejecutado):** build/tests y persistencia en DB; la asignación por conocimiento de sistemas es propuesta sujeta a validación del usuario ítem por ítem (no probada en simulación).

### 2026-10-17 — Documentación: fallas ajenas al engine (externas) detectadas en la API real

**Qué**

- Se documenta en `docs/realistic-mode/estado-y-diseno.md` (sección nueva "Fallas ajenas al engine (externas)") el hallazgo verificado en sim real: con el A320 en gate cold & dark, `GET /fenix/failures/manual` devuelve 3 fallas activas por diseño — `F_OH_NAV_IR_ALIGNMENT_1/2/3` (`failed=true`, sin armado) — que se resuelven con la alineación de ADIRs del cockpit prep (flujo normal del Captain), no las provoca el engine.
- Se registran los 3 impactos sobre el engine actual: (1) `ResetAllFailuresAsync` al detener desarma también las externas; (2) `ActiveFailureCount`/`ActiveFenixFailureIds` las cuentan y pueden bloquear el disparo en gate (3/3 con las IR); (3) la reconciliación futura no distinguiría externa resuelta de propia resuelta.
- Decisión registrada: fuera de alcance; el diseño futuro es una "cartera de fallas propias" (engine-owned): topes y desarme/reset solo sobre los IDs armados por la sesión (persistidos en `TriggeredFailure`), las externas consumen 0 y no se tocan; las fallas de procedimiento tipo IR ALIGNMENT no deberían ser armables por el engine.

**Por qué**

- Hallazgo de integración real que condiciona topes, reset y reconciliación del modo Realistic; no era previsible desde el catálogo y debe quedar asentado antes de diseñar el motor.

**Áreas**

- `docs/realistic-mode/estado-y-diseno.md`; sin cambios de código.

**Pruebas**

- Ninguna ejecutada (cambio de documentación). Hallazgo verificado por consulta real a la API local (`GET /fenix/failures/manual`) con el avión cargado en gate.

### 2026-10-17 — Consolidación del esquema en desarrollo (EnsureCreated, sin migraciones)

**Qué**

- Política nueva de esquema para desarrollo: se eliminan las migraciones existentes (`InitialCreate`, `FixFailurePresetDuplicateColumn`, `AddSessionEventHistoryAndDerivedAircraftTotals`, `RealisticModeFlowSchema` y el snapshot) y `App.xaml.cs` pasa de `MigrateAsync()` a `EnsureCreatedAsync()`: el esquema se crea completo desde el modelo actual (con seeds) en bases nuevas; no hay historial ni `__EFMigrationsHistory`.
- Se borraron las bases locales de la app (`realfenixfailures.db` en la solución, el proyecto y `bin/Release`; la de `bin/Debug` quedó retenida por la app abierta y se regenera al cerrarla) — la app las recrea solas.
- `docs/desarrollo.md` y `AGENTS.md` actualizados a la política: al cambiar el esquema, borrar la base local; al acercarse a v1, generar una única migración inicial y volver al flujo clásico.

**Por qué**

- Decisión del usuario: en desarrollo la base es un artefacto regenerable; las migraciones solo tendrán sentido en producción (v1). Evita fricción de historial en cada fase y el riesgo de «migration not found» contra bases viejas.

**Áreas**

- `RealFenixFailures.UI/App.xaml.cs`, `RealFenixFailures.Infrastructure/Migrations` (eliminados), `docs/desarrollo.md`, `AGENTS.md`, `docs/realistic-mode/estado-y-diseno.md`.

**Pruebas**

- Build Release sin advertencias y **88/88 tests** (los tests usan `EnsureCreated()` en memoria, mismos que antes). Base real borrada/regenerable; sin verificación manual en sesión real.

### 2026-10-17 — Backend Realistic (Fase 1): configuración y esquema objetivo

**Qué**

- Nueva sección `FailureEngine:RealisticMode` en `FailureEngineSettings` + `appsettings.json`: intervalo de evaluación (30 s), tope de cola de preparadas (5), fallas en tierra habilitadas, tope de líneas de pensamiento (50) y ventanas OFP (`PreStartWindowHours=2`, `PostArrivalWindowHours=2`) y desgaste (`AmplificationFactor=0.5`, `ThresholdNoticePct=80`). Sin cadencias de disparo nuevas.
- Migración nueva `RealisticModeFlowSchema` (no se tocaron las aplicadas): tabla `FlightPlans` (origen/destino/aerotipo, OFP+valores UTC, validez, estado de validación, XML crudo, fuente) con FK desde `FlightSession` (+`OriginIcao`/`DestinationIcao`/`ResultSnapshotJson`); ciclo de vida en `TriggeredFailure` (`State` default `Sent`, `FenixConfirmedAtUtc`, `ResolvedAtUtc`, `ResolvedPhase`, `ResolutionSource`, `CanBeTemporary`, `PreparedAtUtc`); `AtaId` en `FenixFailureSystem` y `AircraftWearableSystem`; `CanBeTemporary` en `FenixFailureDefinition`.
- Seed de sistemas de desgaste ampliado de 8 a 19, alineado a los bloques ATA de `failures.json` (21..80); los Ids 1-8 cambian de semántica (Engine 1/2 se fusionan en ATA 70/80). Impacto en datos viejos de `AircraftSystemWear` documentado en `estado-y-diseno.md`.
- Enums nuevos/aditivos: `FailureLifecycleState`, `ResolutionSource`, `ValidationState`; `SessionEventCategory` 9..14 (`Pause`…`SessionRecovered`).
- La severidad por falla (campo `severity` en `failures.json` y su persistencia) **queda delegada al usuario**, no se implementa en esta fase.

**Por qué**

- Primer bloque del plan de backend aprobado: sienta el esquema que consumen las fases siguientes (OFP/validez, estados de falla, pausa/desarme, desgaste, resultados).

**Áreas**

- `Domain` (entidades `FlightPlan` nueva, `FlightSession`, `TriggeredFailure`, `FenixFailureDefinition`, `FenixFailureSystem`, `AircraftWearableSystem`; enums; `FailureEngineSettings`), `Infrastructure` (`RealFenixDbContext`, `SeedData`, migración `RealisticModeFlowSchema`), `UI/appsettings.json`, `TestUnits` (3 tests nuevos), `docs/realistic-mode/estado-y-diseno.md`.

**Pruebas**

- Migración validada en base temporal: aplicación completa, `Down` y re-`Up` (19 sistemas con AtaId 21..80 y `FlightPlans` presentes); la base real no se modificó. Build Release sin advertencias. **88/88 tests** (incluye los 3 nuevos: seed 19/AtaId únicos, valores aditivos de `SessionEventCategory`, defaults de `RealisticMode`). Build Debug del proyecto de tests OK; el build Debug de la solución quedó bloqueado por el ejecutable abierto (igual que en commits previos). Sin verificación manual en sesión real.

### 2026-10-17 — Documentación del contrato crudo de la API de Fenix

**Qué**

- `docs/integraciones/fenix-api.md` documenta el contrato HTTP crudo: forma de `GET /fenix/failures/manual` (idéntica a `failures.json`, con el estado real por falla: `failed` = activa, `failureCondition` = armado), las cinco condiciones de armado (IAS, altitud sobre/bajo, tiempo, evento) y el payload de `POST /fenix/failures/saveManual` para activar, armar, desarmar/limpiar (no existe campo `armed`).
- Se añade la derivación de estados del modo Realistic (limpia/armada/activa), el mapeo con la guía oficial de FenixSim (EFB pestañas Failed/Armed, resolución por el FMGC/EFB, mantenimiento en rampa) y la lista de verificación con la API real: GET de catálogo completo y operaciones POST confirmadas manualmente; reset verificado por envío, detección de resuelta por piloto y mantenimiento quedan pendientes.
- Se registra que la respuesta **no incluye severidad** (asegura la necesidad del catálogo curado por `id` en el plan del motor) y la limitación conocida de `ResetAllFailuresAsync` (verifica solo `failed`, no `failureCondition`).

**Por qué**

- Ambos endpoints ya estaban implementados en `FenixApiClient`/`FenixApiFailureService` pero sin documentación del protocolo; es la base para definir el mapeo de estados de falla del modo Realistic.

**Áreas**

- `docs/integraciones/fenix-api.md`; sin cambios de código.

**Pruebas**

- Ninguna ejecutada (cambio de documentación). Comportamiento de la API confirmado por revisión manual del usuario con el avión cargado (listado completo con estados; armado/desarme/activación/desactivación vía `saveManual`). Pendientes detallados en el doc.

### 2026-10-17 — Ajuste de densidad y selector modal del front Realistic

**Qué**

- Paso 3: tarjetas paralelas para ruta/OFP y requisitos, rótulos/estados compactos, sin duplicar conexiones ni fase de la barra superior. Desplazamiento vertical con barra nativa oculta; pensamiento del motor visible solo durante sesión activa.
- El requisito «Perfil» abre una ventana modal de cuatro perfiles expandibles: distingue topes y cadencia del diseño objetivo del motor vigente, indica público y uso VATSIM. Bajo/Moderado/Alto tienen selección provisional y botón Confirmar; Cancelar no cambia la elección; 7700 puede consultarse pero permanece no seleccionable.
- El aviso VATSIM sale del cuerpo de la pantalla y se presenta como confirmación al intentar iniciar con requisitos válidos. Aclara la prioridad del controlador y que, hasta que exista pausa que desarme, hay que detener y revisar fallas en Fenix. Se recomienda terminar en tierra, sin prohibir detener en vuelo.
- La validación OFP continúa ausente (`OfpValidated=false`): el intento de inicio y su confirmación VATSIM no son alcanzables todavía. No se implementaron backend OFP, pausa ni perfil 7700.

**Por qué**

- Aprovechar el espacio disponible y evitar tarjetas/avisos redundantes en preparación sin confundir capacidades objetivo con funciones existentes.

**Áreas**

- `RealFenixFailures.UI` — panel, ventana modal de perfiles y ViewModel; `docs/realistic-mode/` — diseño y estado.

**Pruebas**

- `dotnet build RealFenixFailures.slnx -c Debug --nologo -v q`: 0 errores, 0 advertencias.
- `dotnet test RealFenixFailures.TestUnits/RealFenixFailures.TestUnits.csproj -c Debug --no-build --nologo -v q`: 85/85.
- `dotnet run --project RealFenixFailures.UI.csproj -c Debug --no-build` ejecutado desde el proyecto UI: se abrió la ventana principal y se navegó por UI Automation a Realista/paso 1. El primer intento desde la carpeta de solución no inició por ausencia de `appsettings.json` en ese directorio; se cerró y se ejecutó desde la carpeta correcta.
- Pendiente de verificación manual: paso 3, ventana modal y diseño WPF a tamaño mínimo (no había aeronaves registradas en la sesión de prueba); la confirmación de inicio sigue bloqueada por la ausencia de validación OFP.

### 2026-10-17 — Flujo de tres pasos del front Realistic y validación de aeronaves

**Qué**

- `RealisticModePanel` y `RealisticModeViewModel` pasan de cuatro pasos a tres: selección/registro de aeronave; resumen e historial; preparación y monitoreo en la misma pantalla. Se conserva el desglose de sistemas y se añade el promedio del desgaste registrado a la tarjeta de aeronave, con «Sin datos» cuando faltan filas; se indica sesión sin finalizar y «OFP: No registrado» en el historial (no existe campo OFP aún). La zona central es desplazable y deja accesibles las acciones inferiores.
- Registro: matrícula de máximo 10 caracteres, normalización de espacios extremos y mayúsculas también en `UserAircraftService.CreateAsync`; selector y validación de ICAO A319/A320/A321. `GetAllAsync` carga desgaste y sistema para las tarjetas. Tests nuevos comprueban normalización, longitud, tipos admitidos y carga de desgaste.
- El paso de preparación muestra conexiones y datos coherentes, cabecera de aeronave/perfil/ruta/fase/estado, aviso operativo y VATSIM (el controlador prevalece), tres perfiles vigentes explicados sin atribuirles los topes futuros y tarjeta 7700 no seleccionable. Incluye lista de fallas enviada sin confirmación y ojo desplegable sin razonamientos ficticios. La pausa que desarma y la descarga de OFP están deshabilitadas con explicación, incluida en sus tooltips. Reanudar evaluación por desconexión conserva su semántica antigua. El perfil no cambia durante una sesión activa.
- **Limitación deliberada**: el inicio de nuevas sesiones desde esta pantalla queda bloqueado porque el OFP obligatorio aún no se puede descargar/validar (`OfpValidated=false`); no se fingió validación. Detener sesiones ya activas sigue accesible, con confirmación inline si está en vuelo y protección contra dobles operaciones. El reset de Fenix real no está garantizado. No se implementaron motor, integración OFP, pausa/desarme real, topes 7700, auto-resume ni resultados.
- `docs/realistic-mode/diseno-objetivo.md` §14 explicita el flujo final acordado y `estado-y-diseno.md` distingue esta UI del backend pendiente.

**Por qué**

- Definir la experiencia de preparación y sesión antes de cambiar el motor, sin representar como operativas capacidades todavía ausentes.

**Áreas**

- `RealFenixFailures.UI` — panel y ViewModels Realistic; `RealFenixFailures.Infrastructure` — servicio de aeronaves; `RealFenixFailures.TestUnits` — pruebas del servicio; `docs/realistic-mode/` — diseño y estado.

**Pruebas**

- `dotnet build RealFenixFailures.slnx -c Debug --nologo -v q`: 0 errores, 0 advertencias.
- `dotnet test RealFenixFailures.TestUnits/RealFenixFailures.TestUnits.csproj -c Debug --nologo -v q`: 85/85.
- `dotnet build RealFenixFailures.slnx -c Release --nologo -v q`: 0 errores, 0 advertencias.
- Renderizado manual WPF a 900×720 y sesión real MSFS/Fenix: **no verificados**. Los tests actuales no cubren bindings ni interacciones WPF.

### 2026-10-17 — Documento de diseño objetivo del modo Realistic (diseño/visión, sin código)

**Qué**

- Cambio de **documentación/diseño** (sin cambios de código): se agrega `docs/realistic-mode/diseno-objetivo.md`, un documento de diseño que plasma la **visión** del modo Realistic tal como la define el usuario, como resultado de un intercambio de definición guiado (Q1–Q89). Cubre: visión, flujo de alto nivel (qué entra/sale y cuándo), configuración y arranque (requisitos, OFP y ventana temporal), perfiles de riesgo como comportamiento completo (Bajo/Moderado/Alto/7700), ciclo de vida de la sesión, fallas iniciales de tierra, ciclo de vida de una falla, fallas temporales, resolución por el piloto, pipeline del motor y plan de intensidad (con pesos de críticas por fase según distribución histórica), desgaste por sistema (19 ATAs de `failures.json`), historial y resultados, UX de la ventana del modo, e inicio sin Fenix operativo.
- Es documento de **intención/objetivo**, no de estado de implementación: `estado-y-diseno.md` sigue siendo la referencia de lo implementado.
- Se crea el tracker local de diseño `.scratch/realistic-vision/` (mapa + issues 01–12, gitignored) y se amplía `CONTEXT.md` (glosario y decisiones de dominio).

**Por qué**

- El usuario quiere documentar de forma clara y concisa todo lo que tiene pensado para el modo Realistic, desde la UX y los inputs hasta el engine y sus outputs, antes de evaluar viabilidad e implementación.

**Áreas**

- `docs/realistic-mode/diseno-objetivo.md` — documento nuevo.
- `docs/CHANGELOG.md` — esta entrada.
- `.scratch/realistic-vision/` y `CONTEXT.md` — apoyo del proceso de definición (el tracker es gitignored).

**Pruebas**

- **No se ejecutaron pruebas**: el cambio es solo de documentación/diseño, sin cambios de código (no aplica build/test).

### 2026-10-16 — Planeamiento conceptual del motor realista: estados internos, memoria y política de decisión (documentación)

**Qué**

- Cambio de **documentación/diseño** (sin cambios de código): se amplía la sección de investigación/diseño del modo Realistic en `docs/realistic-mode/estado-y-diseno.md` con una subsección nueva, **"Cómo debe pensar el motor: estados internos y política de decisión"**, ubicada entre "Relación entre inputs y outputs" y "Planificación previa vs reevaluación en tiempo real". Incluye: tabla de **estados internos** del motor (observar/recolectar, validar, contextualizar, evaluar elegibilidad, ponderar, decidir, ejecutar, registrar, aprender/replanificar) con la pregunta que resuelve y sus entradas/salidas, mapeada a las fases del flujo ya documentado; apartado de **memoria del motor** (qué recuerda durante la sesión — fallas disparadas, candidatos descartados, cooldowns, desvíos del OFP, contexto reciente — y qué no arrastra entre sesiones salvo datos persistentes); apartado de **política de decisión** (cuándo armar, posponer o no armar; influencia del objetivo y la plausibilidad; cómo evitar repeticiones absurdas); apartado de **explicabilidad** (qué debe poder contestar el motor en lenguaje natural sobre cada decisión) y un mini-flujo textual que conecta los estados internos con el flujo existente sin reemplazarlo.
- Las secciones existentes (flujo, inputs/outputs, OFP/SimBrief y preguntas abiertas) se conservan íntegras; el resto del documento no se tocó.

**Por qué**

- El usuario quiere una capa adicional de planeamiento conceptual centrada en el comportamiento interno del motor (estados internos, memoria, política de decisión y criterios de explicación), como paso previo a decidir la siguiente pregunta de diseño.

**Áreas**

- `docs/realistic-mode/estado-y-diseno.md` — subsección nueva dentro de "Investigación y diseño" (inserción; el resto del contenido no se tocó).
- `docs/CHANGELOG.md` — esta entrada.

**Pruebas**

- **No se ejecutaron pruebas**: el cambio es solo de documentación/diseño, sin cambios de código (no aplica build/test).

### 2026-10-15 — Reducción de ruido de logs: niveles raíz Serilog y transiciones INFO únicas (SimConnect / API Fenix)

**Qué**

- `SerilogExtensions.ApplyDefaultConfiguration` (`RealFenixFailures.Infrastructure/Logging/SerilogExtensions.cs`) ahora aplica `MinimumLevel` **Default y Override** del appsettings al **logger raíz** (antes `ReadFrom.Configuration` se invocaba dentro del `WriteTo.Logger` hijo y los overrides Microsoft/System no filtraban INFO en el archivo). El sink principal sigue excluyendo la categoría `SimConnect.Diagnostics` y el sink de diagnóstico separado solo existe si `SimConnectDiagnostics:Enabled`. Path, `outputTemplate` y `rollingInterval` del sink principal se siguen leyendo de appsettings.
- `appsettings.json` (UI): override nuevo `System.Net.Http.HttpClient: Warning` (suprime el chatter INFO del framework HTTP) y `Microsoft.EntityFrameworkCore.Database.Command` pasa de `Warning` a `Debug` (override más específico que `Microsoft: Warning`, aplicado en el logger raíz).
- `AddInfrastructure` (`RealFenixFailures.Infrastructure/DependencyInjection.cs`) re-mapea `RelationalEventId.CommandExecuted` de Information a Debug con `ConfigureWarnings(w => w.Log((RelationalEventId.CommandExecuted, LogLevel.Debug)))`; el resto de eventos/warnings/errores y `EnableDetailedErrors`/sensitive logging se conservan.
- `SimulatorFlightDataProvider.IsConnectedAsync`: la transición se compara contra el estado previo **antes** de actualizarlo (antes se comparaba después, por lo que nunca se notificaba); emite INFO cortos únicos `SimConnect conectado`/`SimConnect desconectado` solo en cambios de estado; el primer `false` no se considera desconexión; timeout esperado o API caída no emiten WRN ni stacktrace por intento (ERR solo ante excepción no prevista) e inicialización pasa a Debug. Se mantienen caché de 10 s, formato y ritmo de los logs de diagnóstico.
- `SimConnectClient`: se silencia el `COMException` esperado con HRESULT `0x80004005` al intentar conectar (sin mensaje repetido ni stacktrace; la transición INFO la reporta el provider); `Successfully connected`, `Disconnected`, `SimConnect Open` y `Timeout: Reconnecting` pasan a Debug. Se conservan ERR en message pump y procesamiento de datos, WRN en `OnRecvException` y la corrección de datum IDs únicos (`datumId++`).
- `FenixApiClient`: los fallos de conectividad/timeout esperables (`HttpRequestException`/`TaskCanceledException`) en la consulta de disponibilidad y en llamadas HTTP ya reportadas como cambio de estado pasan a Debug sin stacktrace; los errores HTTP no-success inesperados siguen en Warning y las excepciones inesperadas no se silencian.
- `FenixApiFailureService.UpdateHealthState`: con `lock` simple (seguro ante llamadas concurrentes de armado/desarme/health check) emite INFO cortos `API Fenix conectada`/`API Fenix desconectada` solo en transiciones; el primer `false` no cuenta como desconexión inicial y resultados repetidos no loguean. Se mantienen caché y comportamiento de API.
- Tests: se amplía `SerilogDiagnosticsTests` (overrides por categoría, filtrado de INFO EF/HttpClient, Debug propio conservado, separación diagnóstica y override específico `Database.Command=Debug`); nuevos tests de transiciones para `SimulatorFlightDataProvider` (primer false sin línea, INFO único por conexión, timeout sin WRN, ERR solo inesperado) y `FenixApiFailureService` (sin línea en fallo inicial, una línea por transición, concurrencia) con loggers de captura (`Logging/RecordingLoggers.cs`); 11 tests nuevos, el resto intacto.

**Por qué**

- En la sesión de producción del 2026-10-15 (`realfenix-20261015_003.log`) había ≈2.168 INFO de `System.Net.Http.HttpClient`, 989 INFO de `Microsoft.EntityFrameworkCore.Database.Command` y errores repetidos de conexión a SimConnect (COM `0x80004005`) con stacktrace cada intento, mientras el host fijaba Microsoft logging en Trace y los overrides no filtraban: `ReadFrom.Configuration` corría en el sublogger hijo. El usuario pidió notificar información solo en cambios de estado conectado/desconectado (mensajes cortos `API Fenix Conectada`/`SimConnect conectado`), suprimir el chatter del framework HTTP, mover SQL/EF `CommandExecuted` de INF a DBG y dejar visibles solo los errores auténticamente inesperados.

**Áreas**

- `RealFenixFailures.Infrastructure` — `Logging/SerilogExtensions.cs`, `DependencyInjection.cs`.
- `RealFenixFailures.UI` — `appsettings.json`.
- `RealFenixFailures.Integrations.SimConnect` — `Services/SimulatorFlightDataProvider.cs`, `Services/SimConnectClient.cs`.
- `RealFenixFailures.Integrations.Fenix` — `Services/FenixApiClient.cs`, `Services/FenixApiFailureService.cs`.
- `RealFenixFailures.TestUnits` — `Logging/SerilogDiagnosticsTests.cs`, `Logging/RecordingLoggers.cs` (nuevo), `Integrations/SimulatorFlightDataProviderLoggingTests.cs` (nuevo), `Services/FenixApiFailureServiceLoggingTests.cs` (nuevo), referencia de proyecto a `Integrations.Fenix`.
- `docs/` — `integraciones/simconnect.md`, `integraciones/fenix-api.md`, `CHANGELOG.md`.

**Pruebas**

- `dotnet build RealFenixFailures.slnx -c Debug`: 0 errores, 0 advertencias.
- `dotnet test RealFenixFailures.TestUnits/RealFenixFailures.TestUnits.csproj --no-restore`: **80/80**.
- `git diff --check`: sin errores.

**Pendiente / Limitación**

- Los niveles, overrides, path y template del sink principal se leen de appsettings; un `WriteTo` adicional agregado a settings (p. ej. consola) no se refleja porque el sink principal se configura en código (necesario para excluir `SimConnect.Diagnostics` por sink). Indicado como limitación; no se agregaron dependencias nuevas.
- Por la ventana de caché de 10 s del provider, la transición `conectado → desconectado` de SimConnect no se cubre con esperas de 10 s en unit tests (el primer `false` sin conexión previa, el INFO único por conexión y el timeout sin WRN sí); la dirección completa quedó cubierta en los tests del dispatcher Fenix, que actualiza el health state por llamadas de API sin esperas.
- El nivel de ruido final en el archivo real requiere una sesión de producción; builds/tests no reemplazan la prueba manual.

### 2026-10-15 — Datos de aeronave coherentes (HasAircraftData) + fix de DuplicateID 29 en AddToDataDefinition

**Qué**

- `SimConnectClient.RegisterDataDefinitions` (`RealFenixFailures.Integrations.SimConnect/Services/SimConnectClient.cs`) asigna a cada una de las 18 `AddToDataDefinition` un ID de datum único (`uint datumId` con `datumId++`, 0..17), sin alterar el orden del struct `AircraftStateData`. Antes todas pasaban `0` explícito como último argumento (DatumID).
- El handler `OnRecvException` enriquece el mensaje con `dwException`, `dwSendID` y `dwIndex` (mismo nivel Warning, una sola línea).
- Nuevo validador puro `AircraftStateDataValidator.IsValid` (`Integrations.SimConnect/Models/AircraftStateDataValidator.cs`): rangos físicos razonables (lat/lon, altitudes MSL/AGL, heading, velocidades, VS, flaps, N1, throttle), booleanos int estrictos 0/1 (OnGround, combustión), rechazo de NaN/Infinity/denormales y del frame completamente en cero; permite aeronave estacionada a altitud 0. Un paquete inválido conserva sus valores crudos en `SimConnectAircraftState` para diagnóstico pero con `IsConnected=false` (y por lo tanto `HasSimulatorData=false`).
- El callback `OnRecvSimobjectDataBytype` actualiza `_lastDataReceivedUtc` por cualquier paquete recibido (sin bucle de reconexión cuando SimConnect responde sin avión cargado) y sobrescribe siempre el último estado: una muestra inválida nunca deja la telemetría previa válida como actual. Sin warnings por paquete (100 ms).
- `ConnectionStatusDto` gana la propiedad init-only `HasAircraftData` (default `false`; se preserva el constructor posicional). `SimulatorConnectionService.GetConnectionStatusAsync` marca `HasAircraftData` y reporta fase `Disconnected` si el handle está abierto pero no hay datos coherentes (antes `Flying`/`OnGround` con telemetría basura).
- UI: `MainWindowViewModel` sincroniza `HasAircraftData`; `SimConnectStatus` distingue `Disconnected` / `Connected · No aircraft data` / `Connected · Aircraft data` con punto gris/ámbar/verde; `CanStartEngine` exige SimConnect + datos de aeronave + Fenix; `CurrentFlightPhaseDisplay` muestra `Disconnected`/`No aircraft data` sin presentar fases viejas. `RealisticModeViewModel` sincroniza la propiedad, exige `HasAircraftData` en `CanActivate`, agrega la razón "MSFS conectado pero sin datos de aeronave disponibles." y muestra `Disconnected`/`No aircraft data` en su indicador de fase.
- `FlightEventDetector.ProcessSnapshot` con `HasSimulatorData=false` limpia todo el tracking anterior (fase, debounce, altitud y motor/latch) y devuelve `Unknown` sin emitir eventos: una lectura corrupta o la ausencia de avión nunca dejan `Landing`/`Cruise` estancadas (antes conservaba la fase confirmada). El motor realista ya salta ese tick y ese comportamiento se conserva.

**Por qué**

- Los logs de diagnóstico compartidos (2026-10-15) mostraban `SimConnect Exception: 29` en cada conexión y telemetría corrupta (`Lon=6.8e38` o `0`, `AltMSL=0`, `IndAlt≈1e-313`, `AltAGL=-2147483648`, `Flaps=2147483647`, combustión con enteros enormes, N1/throttle absurdos) con el detector marcando `Landing` aun con `OnGround=false`. Contraste con la doc oficial del SDK: enum 29 = `SIMCONNECT_EXCEPTION_DUPLICATE_ID`, que SimConnect produce por IDs repetidos en `AddToDataDefinition`; el cliente pasaba DatumID `0` en las 18 definiciones. **Raíz probable/confirmada: registro duplicado de datos.** Adicionalmente, `IsConnectedAsync` indica solo el handle y la UI confundía conexión con datos de aeronave disponibles.

**Áreas**

- `RealFenixFailures.Integrations.SimConnect` — `Services/SimConnectClient.cs`, `Models/AircraftStateDataValidator.cs` (nuevo).
- `RealFenixFailures.Application` — `DTOs/ConnectionStatusDto.cs`, `Services/SimulatorConnectionService.cs`, `Services/FlightEventDetector.cs`.
- `RealFenixFailures.UI` — `ViewModels/MainWindowViewModel.cs`, `ViewModels/Realistic/RealisticModeViewModel.cs`.
- `RealFenixFailures.TestUnits` — `Integrations/AircraftStateDataValidatorTests.cs` (nuevo), `Services/SimulatorConnectionServiceTests.cs`, `Services/FlightEventDetectorTests.cs`.
- `docs/` — `integraciones/simconnect.md`, `realistic-mode/estado-y-diseno.md`, `CHANGELOG.md`.

**Pruebas**

- `dotnet build RealFenixFailures.slnx -c Debug`: 0 errores, 0 advertencias.
- `dotnet build RealFenixFailures.slnx -c Release --no-restore`: 0 errores, 0 advertencias.
- `dotnet test RealFenixFailures.TestUnits/RealFenixFailures.TestUnits.csproj --no-restore`: **69/69**.

**Pendiente / Limitación**

- La corrección de datos de aeronave fue comprobada por el usuario en producción: reportó telemetría coherente tras publicar el fix. Los builds y tests automatizados siguen siendo evidencia separada de esa prueba manual.
- La detección se describe como **datos coherentes disponibles** (validador físico de rangos), no como confirmación de un estado de menú específico del simulador; no se usó ningún API de menú ni `L:Vars`.

### 2026-10-16 — Manejo global de excepciones, log de crash y splash de arranque (UI)

**Qué**

- Cambio de **comportamiento** en `RealFenixFailures.UI`: las excepciones no controladas ya no matan la app en silencio. Se mantiene la bitácora de errores en `docs/ui/manejo-de-errores-y-arranque.md`.
- **Handlers globales** en `Diagnostics/CrashReporter.cs` (`AttachGlobalHandlers` como primer paso de `App.OnStartup`): `DispatcherUnhandledException` (hilo de UI → log + cartel + cierre controlado), `AppDomain.UnhandledException` (hilos de fondo, fatal → log + cartel best-effort en hilo STA dedicado; el proceso igual termina, comportamiento del CLR) y `TaskScheduler.UnobservedTaskException` (fire-and-forget → **solo log**, no fatal; decisión documentada en el doc de estado).
- **Crash log** en `Diagnostics/CrashLogWriter.cs`: escribe `<AppContext.BaseDirectory>\logs\crash\crash-YYYYMMDD-HHmmss-fff.log` (UTF-8) con fecha local/UTC, contexto, versión app, runtime, SO, máquina, usuario, PID, rutas, procesadores y **trace completo por nivel** (inner exceptions recursivas + `AggregateException` aplanada). `Write()` nunca lanza y devuelve `null` si falla.
- **Cartel de error** en `Diagnostics/FatalErrorDialog.xaml(.cs)`: autocontenido (sin depender de los recursos de la app), mensaje general + ruta del log + botón "Ver registro de crash" + expander con el stack trace + botón Cerrar; se muestra en hilo STA dedicado si la llamada no viene del hilo de UI.
- **Entrada manual + splash**: `<StartupObject>RealFenixFailures.UI.Program</StartupObject>` en el `.csproj`; `Program.Main` muestra un `SplashScreen` (imagen `Assets/Branding/splash-screen.png`, 560×340, generada con la marca del proyecto; sin bordes, se cierra sola al renderizarse la ventana principal) antes de crear la app y envuelve `new App()` + `InitializeComponent()` + `Run()` en try/catch → fallo de arranque previo al host = log + cartel.
- **`App.OnStartup` reforzado**: try/catch alrededor de `_host.StartAsync()` + `MigrateAsync` + `InitializeAsync`, y alrededor de la creación de `MainWindow`; ambas ramas cierran el splash y reportan con `shutdown: true`. La rama de instancia única (segunda instancia) cierra el splash y sale en silencio (no es un crash). Flag `Program.StartupFailed` evita un segundo reporte espurio cuando WPF invoca `OnStartup` con `_host` null tras fallar el ctor de `App` (verificado en el smoke test).

**Por qué**

- El usuario reportó que los crashes no controlados y los fallos de arranque eran silenciosos e invisibles (cerraba la app sin rastro), y que el arranque lento dejaba la duda de si la app estaba abriendo o colgada. Pedido explícito: manejo global de excepciones con cartel en pantalla + log de crash con trace completo en `/logs`, lo mismo para fallos de arranque, y un splash de carga sin bordes (imagen) que los cubra.

**Áreas**

- `RealFenixFailures.UI/Program.cs` — entrada manual con splash y try/catch de arranque (nuevo).
- `RealFenixFailures.UI/Diagnostics/` — `CrashReporter.cs`, `CrashLogWriter.cs`, `FatalErrorDialog.xaml(.cs)` (nuevos).
- `RealFenixFailures.UI/App.xaml.cs` — attach de handlers globales, try/catch de arranque, cierre de splash.
- `RealFenixFailures.UI/RealFenixFailures.UI.csproj` — `<StartupObject>` + recurso `splash-screen.png`.
- `RealFenixFailures.UI/Assets/Branding/splash-screen.png` — imagen de splash (nueva).
- `docs/ui/manejo-de-errores-y-arranque.md` — doc de estado nuevo; `docs/CHANGELOG.md` — esta entrada.

**Pruebas**

- `dotnet build RealFenixFailures.slnx -c Debug`: **0 errores, 0 warnings**.
- `dotnet test RealFenixFailures.TestUnits/RealFenixFailures.TestUnits.csproj`: **80/80 pass**.
- **Smoke test del binario** (copia temporal del output): (a) arranque con `appsettings.json` faltante → `logs/crash/crash-*.log` escrito con trace completo (`FileNotFoundException` de config, versión, runtime, SO, PID) y **proceso vivo con el cartel bloqueando** (no cierre silencioso); (b) arranque normal desde su propia carpeta → proceso vivo 15 s, ventana abierta, **sin** entradas nuevas en `logs/crash`. Queda **pendiente la verificación manual en pantalla real** (cartel visible; splash durante un arranque lento; crash de hilo de fondo con cartel best-effort).



### 2026-10-16 — Diagrama Mermaid del flujo del motor realista (documentación)

**Qué**

- Cambio de **documentación/diseño** (sin cambios de código): se agrega un **diagrama Mermaid** a `docs/realistic-mode/estado-y-diseno.md`, justo después del título "Proceso mental del engine (flujo propuesto)", que visualiza el flujo principal por fases (`Origen de inputs → Validación → Construcción de contexto → Filtro de elegibilidad → Ponderación → Decisión → Ejecución → Persistencia/feedback → Replanificación`) con dos desvíos explícitos: **Validación → NoAction** cuando faltan datos coherentes y **Persistencia/feedback → Replanificación (siguiente tick)** que cierra el ciclo hacia el siguiente tick.
- El diagrama es solo un **apoyo visual** del flujo ya documentado: no cambia el contenido conceptual de la sección ni introduce lógica de implementación nueva; el resto del documento no se tocó.

**Por qué**

- El usuario pidió un diagrama Mermaid que visualice el flujo del motor realista ya documentado en la sección de diseño, como apoyo visual, manteniendo el resto del documento intacto.

**Áreas**

- `docs/realistic-mode/estado-y-diseno.md` — inserción del diagrama Mermaid en "Proceso mental del engine (flujo propuesto)" (el resto del contenido no se tocó).
- `docs/CHANGELOG.md` — esta entrada.

**Pruebas**

- **No se ejecutaron pruebas**: el cambio es solo de documentación/diseño, sin cambios de código (no aplica build/test).

### 2026-10-16 — Afinación del diagrama del motor realista: flujo por fases, inputs por categorías y outputs (documentación)

**Qué**

- Cambio de **documentación/diseño** (sin cambios de código): se refina la sección de investigación/diseño del modo Realistic en `docs/realistic-mode/estado-y-diseno.md`:
  - "Proceso mental del engine (flujo propuesto)": la cadena conceptual se convierte en un **flujo principal por fases** concreto, usable como diagrama de diseño — `Origen de inputs → Validación → Construcción de contexto → Filtro de elegibilidad → Ponderación → Decisión → Ejecución → Persistencia/feedback → Replanificación` — con tabla por fase (qué hace / qué consume / qué produce) y el mapeo a la cadena previa (`Observar → …`) conservado como resumen.
  - "Entradas y salidas del motor": la tabla de inputs se amplía y se agrupa por categorías (**simulador, aeronave, sesión/historial, plan de vuelo, entorno, reglas/configuración, manual user input**), marcando **obligatorio vs opcional** en cada fila; la tabla de outputs ahora distingue **salida operativa** (armar/desarmar/posponer), **persistencia** (session log / triggered failure) y **explicación/diagnóstico**.
  - Nueva mini-sección "Relación entre inputs y outputs": qué inputs influyen en **elegibilidad**, cuáles en **ponderación** y cuáles en la **explicación**.
- El análisis de la respuesta real de SimBrief/OFP se conserva íntegro (sigue siendo el contexto de planificación previa); no se introduce diseño de integración concreta: todo sigue siendo propuesta conceptual.
- `docs/CHANGELOG.md` se actualiza con esta entrada.

**Por qué**

- El usuario pidió "volver a lo importante": dejar el diagrama de flujos y decisiones del motor realista con todos los inputs para generar los outputs más concreto y accionable como base de diseño, refinando la sección de investigación/diseño existente en lugar de rehacer el documento.

**Áreas**

- `docs/realistic-mode/estado-y-diseno.md` — reemplazo/ampliación de los bloques "Proceso mental del engine (flujo propuesto)" y "Entradas y salidas del motor" dentro de la sección de investigación/diseño (el resto del contenido no se tocó).
- `docs/CHANGELOG.md` — esta entrada.

**Pruebas**

- **No se ejecutaron pruebas**: el cambio es solo de documentación/diseño, sin cambios de código (no aplica build/test).

**Pendiente / Limitación**

- La sección sigue siendo investigación/diseño, no comportamiento implementado; el comportamiento vigente del modo es el de "Implementado" en `estado-y-diseno.md`.
- Siguen abiertas las preguntas de diseño listadas en la sección (plausibilidad combinable, ciclo de vida del OFP, historial como narrativa, re-planificación ante desvíos y explicabilidad de la decisión).

### 2026-10-16 — Análisis de respuesta real de SimBrief/OFP como fuente de planificación (documentación)

**Qué**

- Cambio de **documentación** (sin cambios de código): se analizó una **respuesta real de SimBrief** (OFP/flightplan) tomada de la URL pública `https://www.simbrief.com/api/xml.fetcher.php?username=R3M1X0101` y se documentó el análisis en `docs/realistic-mode/estado-y-diseno.md` como subsección nueva dentro de la sección de investigación/diseño ("SimBrief / OFP como fuente de planificación (ejemplo de respuesta real)"), ubicada junto a la parte de planificación previa/inputs.
- La subsección documenta los **bloques que devuelve la respuesta** (encabezado OFP, pesajes, fuel, routing, flight log, weather, TLR, ATC flight plan y NOTAMs), qué aporta cada bloque y su utilidad potencial para el motor (tabla de tres columnas), y una clasificación corta por valor: **muy útil** (flight log, fuel, pesajes, TLR), **útil pero secundario** (routing, weather, encabezado OFP, ATC flight plan) y **no prioritario ahora** (NOTAMs).
- **Alcance explícito**: el uso de esos datos **como input al comenzar el modo realista** (integración concreta) queda **fuera de alcance** de esta investigación; solo se documenta el análisis de los datos devueltos y su valor potencial.
- `docs/CHANGELOG.md` se actualiza con esta entrada.

**Por qué**

- El rol del OFP/flightplan como entrada de planificación previa ya estaba identificado en la sección de investigación/diseño; con una respuesta real de SimBrief se documenta concretamente qué datos devuelve el servicio y cuáles tienen más valor potencial para el motor.

**Áreas**

- `docs/realistic-mode/estado-y-diseno.md` — subsección nueva dentro de "Investigación y diseño" (inserción; el resto del contenido no se tocó).
- `docs/CHANGELOG.md` — esta entrada.

**Pruebas**

- **No se ejecutaron pruebas**: el cambio es solo de documentación, sin cambios de código (no aplica build/test).

**Pendiente / Limitación**

- No se diseñó la integración concreta del OFP al iniciar el modo realista; queda fuera de alcance de esta investigación.
- El análisis se basa en una única respuesta de ejemplo (un OFP de SimBrief para SBGL-SUMU, A320); no es una especificación del formato completo de la API.

### 2026-10-16 — Investigación y diseño del modo Realistic: meta, bloques del engine y rol del OFP

**Qué**

- Cambio de **documentación/diseño** (sin cambios de código): se agrega en `docs/realistic-mode/estado-y-diseno.md` una sección de investigación/diseño entre "Decisiones abiertas" y "Limitaciones restantes". Incluye: objetivo del modo realista; tabla de capas de pensamiento del engine (contexto de vuelo, estado del avión, historial de sesión, plausibilidad, objetivo) con "qué piensa / qué necesita"; flujo numerado del proceso mental (observar → enmarcar → seleccionar → ponderar → decidir → actuar y registrar → re-planificar); tabla de inputs obligatorios/opcionales y outputs con el **OFP/flightplan como entrada de planificación previa** (no oráculo); subsección "Planificación previa vs reevaluación en tiempo real" (el OFP orienta, no manda); y 5 preguntas de diseño abiertas.
- `docs/CHANGELOG.md` se actualiza con esta entrada.

**Por qué**

- El usuario quiere abrir una línea de investigación/diseño del modo Realistic (diagramas y tablas) y documentarla sin tocar código; la discusión se separa del estado implementado para no confundir comportamiento vigente con propuestas.

**Áreas**

- `docs/realistic-mode/estado-y-diseno.md` — sección nueva de investigación/diseño (inserción; el resto del contenido no se tocó).
- `docs/CHANGELOG.md` — esta entrada.

**Pruebas**

- **No se ejecutaron pruebas**: el cambio es solo de documentación/diseño, sin cambios de código (no aplica build/test).

**Pendiente / Limitación**

- La sección es investigación/diseño y no describe comportamiento implementado; el comportamiento vigente del modo es el de "Implementado" en `estado-y-diseno.md`.
- Quedan abiertas las preguntas de diseño listadas en la sección (plausibilidad combinable, ciclo de vida del OFP, historial como narrativa, re-planificación ante desvíos, explicabilidad de la decisión).

### 2026-10-16 — Ventana de configuración con pestañas, preferencias de usuario (ID SimBrief) y eliminación del botón Refresh

**Qué**

- La UI crece con **botón ⚙** en la **status bar** de `MainWindow` (en la posición exacta donde estaba el botón Refresh) que abre la ventana de configuración (`Views/SettingsWindow.xaml` + `SettingsWindowViewModel`), sin bordes, con pestañas: **General** (primera; campo **ID de SimBrief**), **Ayuda**, **Documentación**, **Actualizaciones** y **Reportar un fallo**.
- Nuevo servicio **`UI/Services/UserPreferencesService.cs`** (singleton): persiste preferencias de usuario en `%AppData%\RealFenixFailures\userprefs.json` (por ahora `SimbriefUserId`), con carga tolerante a errores y guardado con debounce (400 ms) + guardado forzado al cerrar la ventana; el ID se muestra/edita en la pestaña General.
- **El botón "↻ Refresh" se eliminó por completo** (XAML + `RefreshCommand` + `RefreshAsync` en `MainWindowViewModel`): la app queda a cargo del autorefresco del orquestador (timer automático cada `CheckConnectionIntervalSeconds`); `InitializeAsync` hace un `UpdateConnection` inicial y carga los presets custom una vez.
- Links de la ventana (repo GitHub `mathi0101/Fenix-Failures-Dispatcher`, issues, releases, docs SimBrief) centralizados como constantes en el ViewModel y abiertos con `Process.Start` + `UseShellExecute` (patrón de la donación). La versión mostrada en "Actualizaciones" sale del ensamblado.
- Docs: nuevo `docs/ui/configuracion-y-preferencias.md` + entrada en `docs/CHANGELOG.md`.

**Por qué**

- El usuario pidió una puerta de configuración de la app con las pestañas típicas (ayuda, documentación, repo, versión, actualizaciones, reportar un fallo) y el ID de SimBrief como primer campo de la pestaña General; en el mismo trabajo pidió quitar el botón Refresh porque el refresco ya es automático.

**Áreas**

- `RealFenixFailures.UI/Views/MainWindow.xaml(.cs)` — botón ⚙ en la status bar (posición del viejo Refresh); inyección de `SettingsWindow`.
- `RealFenixFailures.UI/ViewModels/MainWindowViewModel.cs` — eliminados `RefreshCommand`/`RefreshAsync`; `LoadCustomPresetsAsync` en `InitializeAsync`.
- `RealFenixFailures.UI/Views/SettingsWindow.xaml(.cs)` y `RealFenixFailures.UI/ViewModels/SettingsWindowViewModel.cs` — nuevos.
- `RealFenixFailures.UI/Services/UserPreferencesService.cs` — nuevo.
- `RealFenixFailures.UI/DependencyInjection/HostBuilderExtensions.cs` — registros DI.
- `docs/ui/configuracion-y-preferencias.md` (nuevo) y `docs/CHANGELOG.md` (esta entrada).

**Pruebas**

- `dotnet build RealFenixFailures.slnx -c Debug`: **0 errores, 0 warnings**.
- `dotnet test RealFenixFailures.TestUnits`: **80/80 pasados**.
- Smoke test con `dotnet run` desde el directorio del proyecto UI: la app arranca y queda corriendo sin excepciones (valida el wiring de DI de la nueva ventana). No verificado: interacción visual manual de la ventana de configuración (abrir, escribir el ID, ver "✓ Guardado", abrir URLs).

**Pendiente / Limitación**

- El ID de SimBrief todavía no se consume (integración SimBrief como fuente del plan de vuelo queda para un paso siguiente).
- El fallo de `dotnet run` desde la raíz de la solución (appsettings.json no encontrado) es pre-existente y no relacionado con este cambio.
- "Buscar actualizaciones" abre GitHub Releases (no hay auto-update); el usuario lo confirmó como alcance para versiones con descarga manual.

**Seguimiento (mismo día, mismo trabajo sin publicar) — el panel pasó a ser overlay interno**

- A pedido del usuario, la configuración **ya no es una ventana independiente**: `Views/SettingsWindow.xaml(.cs)` fue reemplazada por el UserControl `Views/Panels/SettingsPanel.xaml(.cs)` **embebido en `MainWindow`**.
- El engranaje de la status bar ahora dispara `OpenSettingsCommand` (`MainWindowViewModel.IsSettingsOpen = true`): aparece un **overlay sobre la fila de contenido** (todo lo que está debajo de la status bar) con fondo `#C0080A0F` que **opaca y bloquea** el contenido, y el panel queda **fijo en el centro** (780×620). La title bar y la status bar quedan visibles e intactas.
- Cierre por **✕** del header del panel o **ESC** (evento `CloseRequested` del panel → `IsSettingsOpen = false`); al cerrar se persisten las preferencias (`Settings.SaveNow()` via setter del VM).
- `MainWindowViewModel` ahora recibe `SettingsWindowViewModel` por DI y expone `Settings`; `MainWindow` ya no depende de `SettingsWindow`; se eliminó el registro de `SettingsWindow` en `HostBuilderExtensions`.
- Áreas extra tocadas: `Views/Panels/SettingsPanel.xaml(.cs)` (nuevos), `Views/MainWindow.xaml(.cs)`, `ViewModels/MainWindowViewModel.cs`, `DependencyInjection/HostBuilderExtensions.cs`, `docs/ui/configuracion-y-preferencias.md`.
- Pruebas repetidas tras el cambio: build 0 errores/0 warnings, tests 80/80, smoke test de arranque sin excepciones (el panel se parsea al construir `MainWindow`, así que el XAML quedó validado en runtime). Sigue pendiente la verificación visual manual del overlay.


### 2026-10-15 — Historial de sesiones (SessionEventLog), snapshot de fallas y totales de aeronave derivados

**Qué**

- Nueva entidad `SessionEventLog` (`Domain/Entities/SessionEventLog.cs`) y enum `SessionEventCategory` (`Domain/Enums/SessionEventCategory.cs`). Tabla **`SessionEventLogs`** append-only por sesión: `SESSION_STARTED`, `SESSION_STOPPED`, eventos de motores (`ENGINES_STARTED`/`ENGINES_STOPPED`), cambios de fase (código = nombre de la fase en mayúsculas, p. ej. `TAXI`, `LANDING`), cruces de altitud (`ALTITUDE_CROSSED`) y fallas disparadas (`FAILURE_TRIGGERED`), cada uno con `OccurredAt`, categoría, descripción y fase. `EventCode` es un identificador **semántico** string que puede repetirse entre eventos/sesiones; la identidad de fila es `Id`.
- `FlightSessionService` (`Application/Services/FlightSessionService.cs`) persiste los eventos (`AddEventLogAsync`), y `AddTriggeredFailureAsync` guarda la falla y su evento enlazado en **una unidad de trabajo** (un único `SaveChangesAsync`, atómico).
- `RealisticSessionEngine` (`Application/Session/RealisticSessionEngine.cs`) se suscribe al `FlightEventDetector` **solo mientras la sesión está activa**; los eventos llegan **síncronos** a una cola (`_pendingFlightEvents`, con lock) y se **persisten async en cada tick** (`DrainFlightEventsAsync` dentro de `EvaluateAsync`). El `SESSION_STARTED` se registra al iniciar (si falla, la sesión se cierra y no queda activa) y el `SESSION_STOPPED` al detener, antes de `StopAsync`.
- `TriggeredFailure` **enriquecida con snapshot histórico** del preset/condición al disparar (nombre y severidad de la falla, `PresetId`/`PresetName`, `Probability`/`ProbabilityGroup`, `Ias`, altitudes superior/inferior, `Time`, `AfterEvent`/`AfterEventSeconds`); el historial no depende del catálogo mutable.
- `UserAircraft.TotalFlights` / `TotalFlightHours` **eliminadas de la tabla**: los DTOs los calculan al vuelo desde las sesiones **finalizadas** de cada aeronave (`UserAircraftService.ToDto`); las sesiones abiertas no cuentan.
- Migración nueva `20261015182532_AddSessionEventHistoryAndDerivedAircraftTotals` (`RealFenixFailures.Infrastructure/Migrations/`): `Up` elimina las dos columnas de `UserAircrafts`, agrega los snapshots nullable en `TriggeredFailures` y crea `SessionEventLogs` con FKs (`FlightSessions` cascade, `TriggeredFailures` restrict) e índice `(FlightSessionId, OccurredAt)`; `Down` revierte todo.

**Por qué**

- No existía historial por sesión: las fallas se veían por sesión pero sin el contexto de la condición que las disparó, y los totales persistidos en `UserAircraft` quedaban siempre en 0 porque nadie los actualizaba. Con el volumen de esta app, calcular los totales al vuelo desde las sesiones finalizadas es trivial y elimina la inconsistencia entre columnas derivadas y datos reales.

**Áreas**

- `RealFenixFailures.Domain` — `Entities/SessionEventLog.cs` (nuevo), `Enums/SessionEventCategory.cs` (nuevo), `Entities/TriggeredFailure.cs` (snapshot), `Entities/FlightSession.cs` (`EventLogs`), `Entities/UserAircraft.cs` (sin totales).
- `RealFenixFailures.Application` — `Services/FlightSessionService.cs`, `Interfaces/IFlightSessionService.cs`, `Session/RealisticSessionEngine.cs`, `DTOs/UserAircraftDto.cs`.
- `RealFenixFailures.Infrastructure` — `Persistence/RealFenixDbContext.cs`, `Repositories/FlightSessionRepository.cs`, `Services/UserAircraftService.cs` (totales derivados), `Migrations/20261015182532_AddSessionEventHistoryAndDerivedAircraftTotals.cs` (nueva) y `Migrations/RealFenixDbContextModelSnapshot.cs`.
- `RealFenixFailures.TestUnits` — `Session/RealisticSessionEngineTests.cs` (persistencia de eventos de sesión y drain), `Services/UserAircraftServiceTests.cs` (nuevo).
- `docs/` — `realistic-mode/estado-y-diseno.md`, `arquitectura.md`, `CHANGELOG.md`.

**Pruebas**

- `dotnet test RealFenixFailures.TestUnits/RealFenixFailures.TestUnits.csproj -c Debug --no-restore`: **55/55**.
- `dotnet build RealFenixFailures.slnx -c Debug --no-restore`: 0 errores, 0 advertencias.
- `dotnet build RealFenixFailures.slnx -c Release --no-restore`: 0 errores, 0 advertencias.
- La migración `AddSessionEventHistoryAndDerivedAircraftTotals` fue revisada y aplicada sobre una base temporal; se verificaron `Up`, `Down` hasta `FixFailurePresetDuplicateColumn` y una nueva aplicación de `Up`. EF advierte que el rebuild de SQLite usa `PRAGMA foreign_keys` fuera de transacción; la base real no se modificó.
- Sin verificación manual real MSFS/Fenix.

**Pendiente / Limitación**

- El historial persistente aplica al **modo Realistic**; Training/Custom todavía no crean `FlightSession` ni historial persistido (igual que antes).
- Las sesiones abiertas no suman a los totales de la aeronave (por diseño: solo sesiones finalizadas).
- Integración real MSFS/Fenix sin verificación manual; la migración no se aplicó sobre la base real (la app la aplicará en el próximo arranque).

### 2026-10-15 — Instancia única + icono de bandeja del sistema

**Qué**

- `SingleInstanceGuard` (nuevo, `RealFenixFailures.UI/Services`): Mutex con nombre fijo tomado en `App.OnStartup` antes de iniciar el host/migraciones; si ya hay una instancia corriendo, la nueva señala (vía `EventWaitHandle`) y cierra sin tocar infraestructura. La instancia primaria escucha esa señal (`ThreadPool.RegisterWaitForSingleObject`) y restaura su ventana.
- `TrayIconService` (nuevo, `RealFenixFailures.UI/Services`): icono en la bandeja del sistema usando `System.Windows.Forms.NotifyIcon` (sin paquete NuGet nuevo; se habilitó `UseWindowsForms` en el `.csproj`). Menú contextual con "Abrir" (restaura la ventana) y "Salir" (cierra la app de verdad). Doble click también restaura.
- `App.xaml`: `ShutdownMode="OnExplicitShutdown"` para que ocultar la ventana no cierre la app.
- `MainWindow`: nueva propiedad `IsExitAllowed` (default `false`) + handler `Closing` que cancela el cierre y hace `Hide()` si no está permitido salir. El botón X de la barra de título custom ahora minimiza a la bandeja en vez de cerrar la app; la única salida real es "Salir" desde el tray.
- `.csproj` de UI: `UseWindowsForms=true` más `<Using Remove="System.Drawing" />` y `<Using Remove="System.Windows.Forms" />` para evitar la ambigüedad de tipos (`UserControl`, `Color`, `Brush`) que ese flag introduce por sus global usings implícitos frente a WPF.

**Por qué**

- Pedido explícito del usuario: evitar procesos duplicados de la app (y que al reabrir se muestre la instancia ya corriendo) y poder dejarla en segundo plano con un indicador en la bandeja, con salida definitiva desde ahí.

**Áreas**

- `RealFenixFailures.UI/Services/SingleInstanceGuard.cs` (nuevo).
- `RealFenixFailures.UI/Services/TrayIconService.cs` (nuevo).
- `RealFenixFailures.UI/App.xaml`, `RealFenixFailures.UI/App.xaml.cs`.
- `RealFenixFailures.UI/Views/MainWindow.xaml.cs`.
- `RealFenixFailures.UI/RealFenixFailures.UI.csproj`.
- `docs/ui/instancia-unica-y-bandeja.md` (nuevo), `docs/arquitectura.md`, `docs/README.md`.

**Pruebas**

- `dotnet build`: se corrigieron en el camino dos errores de compilación (ambigüedad de tipos por `UseWindowsForms`, y `CS1503` en `RegisterWaitForSingleObject` por pasar `int` en vez de `Timeout.InfiniteTimeSpan`). **No se pudo re-verificar el build final**: la herramienta de shell del agente quedó bloqueada por su propio sistema de permisos antes de poder confirmar el último build en verde. **No verificado.**
- No se probó manualmente el flujo (dos instancias, minimizar a bandeja, restaurar, salir desde el tray). **No verificado.**

**Pendiente / Limitación**

- Correr `dotnet build RealFenixFailures.slnx -c Debug` para confirmar que compila tras el último fix.
- Probar a mano: abrir dos instancias (la segunda debe activar la primera y cerrarse), minimizar a bandeja con el botón X, restaurar desde "Abrir"/doble click, y cerrar de verdad con "Salir".
- Mutex `Local\`: si una copia corre elevada y otra no, no se detectan entre sí (ver [ui/instancia-unica-y-bandeja.md](ui/instancia-unica-y-bandeja.md)).

### 2026-10-15 — Telemetría SimConnect corregida + estabilización de fases con debounce

**Qué**

- `SimConnectClient` (Integrations.SimConnect) solicita `PLANE ALTITUDE` (se mapea a `AltitudeMSL`) en lugar de `PRESSURE ALTITUDE`, y agrega `AIRSPEED INDICATED` (knots, FLOAT64) como telemetría (el campo del struct se renombra a `PlaneAltitude`; el orden del struct y de `AddToDataDefinition` se mantiene sincronizado). `AirspeedIndicated` se propaga a `SimConnectAircraftState`, `SimulatorAircraftStateSnapshot`, clones, `SimulatorFlightDataProvider` y logs de diagnóstico. No se usan `L:Vars` ni se agrega captura de combustible (sin base).
- Redondeo **solo** de la representación de diagnóstico en `SimulatorFlightDataProvider.GetAircraftRawData`: lat/lon a 5 decimales y N1/throttle a 1 decimal; los valores internos conservan su precisión.
- Nuevo flag `SimulatorAircraftStateSnapshot.HasSimulatorData` (default `false`, `init`, copiado en `Clone`); `SimulatorFlightDataProvider` lo marca desde `SimConnectAircraftState.IsConnected`. `SimConnectClient` limpia el último estado (`new()` con `IsConnected=false`) y `_lastDataReceivedUtc = DateTime.MinValue` bajo `_stateReadWriteLock` al iniciar una conexión, al desconectar y al recibir Quit (no sobrevive una muestra obsoleta; contratos `ISimConnectClient` intactos).
- `FlightEventDetector.ProcessSnapshot`: con `HasSimulatorData=false` no procesa transiciones/eventos/altitud, devuelve la fase confirmada actual y limpia el candidato de debounce. Bandas VS simétricas (debajo de 5000 ft AGL: `VS>200` Climb / `VS<-200` Approach / resto Cruise; a/por encima: `VS>200` Climb / `VS<-200` Descent / resto Cruise). Debounce temporal por `snapshot.ObservedAt` para transiciones aéreas: Cruise 60 s, Descent 45 s, Climb/Approach 10 s; primera fase conocida al instante; fases de tierra y Landing inmediatas; candidatos distintos limpian/reinician el tiempo; la pérdida de datos borra el candidato. En modo 30 s la confirmación cae en el tick que supera el límite (la cadencia de evaluación no cambia).
- Carrera de despegue: estando en tierra, `throttle>80` y `GS>50` ⇒ Takeoff; si no, `GS>5` ⇒ Taxi (antes Takeoff requería `GS<=5` con throttle alto y era inalcanzable en carrera). Tras Landing, mientras `_hasLandedThisFlight` y no se observa Takeoff, una lectura aérea espuria de nivel bajo mantiene la fase terrestre confirmada; la fase Takeoff limpia el latch (se conserva el texto "Taxi final hacia el gate").
- `RealisticSessionEngine.EvaluateAsync`: con `HasSimulatorData=false` no actualiza `RealisticSession` ni intenta disparar una falla en ese tick (log debug + `EvaluationResult.NoAction()`).

**Por qué**

- El usuario probó horas con MSFS 2024/Fenix y reportó altitud/velocidad en cero, precisión excesiva en algunos valores y fase oscilando Cruise/Descent que volvía a Cruise tras el aterrizaje. `PRESSURE ALTITUDE` es presión, no altitud de la aeronave (documentado por MSFS como `PLANE ALTITUDE`), faltaba la IAS, las conversiones a `int` truncaban, y `DeterminePhase` tenía bandas asimétricas sin estabilidad. El estado inicial conectado con datos inventados y la muestra obsoleta tras reconexión se corrigen con el flag `HasSimulatorData` y el reset bajo lock.

**Áreas**

- `RealFenixFailures.Integrations.SimConnect/Models/AircraftStateData.cs` (campo `PlaneAltitude`, `AirspeedIndicated`).
- `RealFenixFailures.Integrations.SimConnect/Models/SimConnectAircraftState.cs` (`AirspeedIndicated` + Clone).
- `RealFenixFailures.Integrations.SimConnect/Services/SimConnectClient.cs` (definiciones, mapeo, `ClearLastKnownState`).
- `RealFenixFailures.Integrations.SimConnect/Services/MockSimConnectClient.cs` (IAS en keyframes/interpolación).
- `RealFenixFailures.Integrations.SimConnect/Services/SimulatorFlightDataProvider.cs` (`HasSimulatorData`, IAS, redondeo del log de diagnóstico).
- `RealFenixFailures.Application/DTOs/SimulatorAircraftStateSnapshot.cs` (`HasSimulatorData`, `AirspeedIndicated`, `ObservedAt` con `init`).
- `RealFenixFailures.Application/Services/FlightEventDetector.cs` (gating, bandas VS, debounce, despegue y latch).
- `RealFenixFailures.Application/Session/RealisticSessionEngine.cs` (NoAction sin datos).
- `RealFenixFailures.TestUnits/Services/FlightEventDetectorTests.cs`, `RealFenixFailures.TestUnits/Session/RealisticSessionEngineTests.cs`, `RealFenixFailures.TestUnits/Services/RealisticSessionManagerTests.cs` (fixtures válidos con `HasSimulatorData=true` + tests deterministas con `ObservedAt` explícito, sin sleeps).
- `docs/integraciones/simconnect.md`, `docs/realistic-mode/estado-y-diseno.md`.

**Pruebas**

- `dotnet build RealFenixFailures.slnx -c Debug`: OK, 0 warnings, 0 errores.
- `dotnet test RealFenixFailures.TestUnits/RealFenixFailures.TestUnits.csproj`: **49/49** passed. Tests nuevos: snapshot inválido inicial (Unknown, sin eventos) y entre muestras (preserva fase, sin eventos); `VS=+150` a >5000 ft AGL no es Descent; debounce Cruise 60 s y Descent 45 s con timestamps controlados; Landing inmediata; lectura aérea posterior al aterrizaje sin retorno a Cruise; Takeoff alcanzable; ciclo completo con Takeoff y segundo vuelo tras el latch; engine con snapshot sin datos (NoAction, sin tocar sesión ni disparar).

**Pendiente / Limitación**

- **MSFS/Fenix reales no verificados**: los cambios se validan con build/tests y mocks; el origen de posibles ceros de los SimVars estándar (`PLANE ALTITUDE`, `AIRSPEED INDICATED`) en el simulador real queda como **hipótesis** hasta revisar un log real de diagnóstico (`SimConnectDiagnostics:Enabled`).

### 2026-10-15 — Logs SimConnect separados: override Debug por categoría + tests de diagnóstico

**Qué**

- En `SerilogExtensions.ApplyDefaultConfiguration` (Infrastructure), cuando `SimConnectDiagnostics:Enabled` está en `true`, se agrega un `MinimumLevel.Override` a `Debug` solo para la categoría `SimConnect.Diagnostics` sobre el logger raíz (y el `using Serilog.Events` correspondiente). Así los eventos `Debug` de esa categoría llegan al sublogger/archivo de diagnóstico aun cuando el nivel mínimo general del logger raíz es más restrictivo (p. ej. `Information` de la configuración publicada, que eliminaba los `Debug` del archivo de diagnóstico aun con el flag activo). El override se limita a la categoría habilitada; los niveles generales y el `ReadFrom.Configuration` del logger principal no cambian.
- Nuevo `RealFenixFailures.TestUnits/Logging/SerilogDiagnosticsTests.cs`: ejecuta el código real `ApplyDefaultConfiguration` con configuración en memoria y archivo principal temporal. Con el flag en `true` emite un `Debug` en la categoría `SimConnect.Diagnostics` con token único y verifica, tras disponer el logger, que el token aparece en `logs/simconnect-diagnostic-*.log` del cwd de tests y no en el log principal. Con el flag en `false` verifica que el token no aparece en ninguno de los dos y que `IsEnabled(Debug)` es `false`.

**Por qué**

- La configuración publicada usa `Debug` como nivel general del sublogger principal, pero el logger raíz (sin nivel fijado) aplica `Information` y filtraba los `Debug` de la categoría de diagnóstico antes de llegar a su sublogger: el archivo de diagnóstico podía quedar vacío aun con el flag activo. El override por categoría es el arreglo mínimo.

**Áreas**

- `RealFenixFailures.Infrastructure/Logging/SerilogExtensions.cs` (override de nivel mínimo `Debug` por categoría dentro del `if` de `SimConnectDiagnostics:Enabled` + `using Serilog.Events`).
- `RealFenixFailures.UI/DependencyInjection/HostBuilderExtensions.cs` (permite que los eventos `Debug` lleguen al provider Serilog; el nivel efectivo de salida sigue gobernado por Serilog).
- `RealFenixFailures.TestUnits/Logging/SerilogDiagnosticsTests.cs` (nuevo).
- `docs/integraciones/simconnect.md` (nota de override por categoría y de reinicio para el flag).

**Pruebas**

- Estado previo al fix: `dotnet test RealFenixFailures.TestUnits/RealFenixFailures.TestUnits.csproj --filter FullyQualifiedName~SerilogDiagnosticsTests` → 1 passed / 1 failed (fallaba `EnabledTrue`: el token no aparecía en el archivo de diagnóstico).
- Estado posterior al fix: mismo comando → 2 passed.
- `dotnet build RealFenixFailures.Infrastructure/RealFenixFailures.Infrastructure.csproj -c Release` → OK, 0 warnings, 0 errores.

**Pendiente**

- Verificación en vivo con el flag en `true` durante una sesión con MSFS 2024 + Fenix: la generación efectiva del archivo de diagnóstico con dato crudo y estado calculado no se pudo comprobar con el simulador; no se afirma integración real verificada.

### 2026-10-15 — Log de diagnóstico SimConnect activable por appsettings

**Qué**

- Nuevo flag `SimConnectDiagnostics:Enabled` en `appsettings.json` (default `false`). Al activarlo, se escribe un archivo de log separado `logs/simconnect-diagnostic-.log` (rolling diario) con: (a) todo el dato crudo recibido de SimConnect en cada poll (`SimulatorFlightDataProvider.GetAircraftRawData`) y (b) el estado/fase de vuelo calculado por la app en cada snapshot (`FlightEventDetector.ProcessSnapshot`), para comparar ambos en desarrollo.
- `SerilogExtensions.ApplyDefaultConfiguration` (Infrastructure) divide el pipeline en dos `WriteTo.Logger`: el principal excluye la categoría `SimConnect.Diagnostics` (vía `Matching.FromSource`); el de diagnóstico la incluye exclusivamente y solo se registra si el flag está en `true`.
- Los dos emisores usan `ILoggerFactory.CreateLogger("SimConnect.Diagnostics")` (no `ILogger<T>`) para no acoplar Integrations.SimConnect/Application a Infrastructure; la categoría es un literal duplicado en los tres archivos (documentado en el doc de integración).
- Con el flag desactivado no se genera el archivo ni se ensucia el log principal (los `LogDebug` se siguen ejecutando pero no tienen sink destino).

**Por qué**

- Permitir, durante pruebas en vivo con MSFS 2024 + Fenix reales, capturar y comparar luego en desarrollo todo lo que SimConnect reporta contra lo que la app deriva (fase de vuelo, motores, AGL/MSL, etc.), sin mezclarlo con el log operativo normal ni dejarlo siempre activo.

**Áreas**

- `RealFenixFailures.Infrastructure/Logging/SerilogExtensions.cs`.
- `RealFenixFailures.Integrations.SimConnect/Services/SimulatorFlightDataProvider.cs` (constructor ahora recibe `ILoggerFactory`).
- `RealFenixFailures.Application/Services/FlightEventDetector.cs` (constructor ahora recibe `ILoggerFactory`).
- `RealFenixFailures.UI/appsettings.json` (sección `SimConnectDiagnostics`).
- `RealFenixFailures.TestUnits/Services/FlightEventDetectorTests.cs` (ajuste del constructor en `CreateDetector`).
- `docs/integraciones/simconnect.md` — nueva sección "Log de diagnóstico (SimConnect + estados calculados)".

**Pruebas**

- `dotnet build RealFenixFailures.slnx -c Debug`: OK, 0 warnings, 0 errores.
- `dotnet test RealFenixFailures.TestUnits/RealFenixFailures.TestUnits.csproj`: 38/38 passed.
- **No verificado**: activación real del flag con MSFS 2024 + Fenix corriendo (generación efectiva de `logs/simconnect-diagnostic-*.log` en una sesión en vivo); falta probarlo manualmente en la máquina con el simulador.

**Pendiente**

- Probar en vivo con el flag en `true` durante una sesión real y confirmar que el archivo se genera y contiene lo esperado (dato crudo + estado calculado) sin contaminar `logs/realfenix-.log`.

### 2026-10-15 — Provisión de presets realistas (loader + seed del catálogo)

**Qué**

- Catálogo `RealFenixFailures.Domain/Resources/realistic_presets.json`: **44 presets** en el mismo esquema que `training_presets.json` (`Name`, `Description`, `TriggerDescription`, `Phase` numérico de `ComplexFlightPhaseEnum` 1–9, `Difficulty` 1–4, `Failures[]` con `FenixFailureId`, `ProbabilityGroup` opcional y `Probability`). Cobertura por fase: OnGate 7, Parked 1, Taxi 4, Takeoff 6, Climb 7, Cruise 8, Descent 2, Approach 5, Landing 4 (131 definiciones, 108 IDs únicos referenciados, todos existentes en `Resources/failures.json`).
- Pesos neutrales: cada preset representa un escenario de falla; las alternativas de un mismo sistema/escenario llevan pesos iguales que suman 1 y `ProbabilityGroup=1`; los singletons usan `Probability=1`. `Probability` es **peso relativo de selección** entre las fallas elegibles del preset (`FailureTrigger.TryTriggerFailure` selecciona por acumulación ponderada sobre la suma de pesos), **no** la probabilidad de disparo de la sesión: esa es `BaseProbability` (0.10) × multiplicador de riesgo (Low 0.5 / Moderate 1.0 / High 1.75), aplicada por preset candidato en cada tick (`RealisticSessionEngine`). El multiplicador de riesgo no modifica los `Probability` individuales.
- `Difficulty` es metadato de clasificación (Easy=1, Medium=2, Hard=3, Insane=4); no está conectado al filtro de riesgo del modo Realistic (el filtro real opera por severidad de la falla Fenix en `FailureRuleEvaluator`, no por `Difficulty` del preset).
- El catálogo ya es `EmbeddedResource` en `RealFenixFailures.Domain.csproj` (con su `None Remove` por el patrón SDK); `FailureEngineSettings.RealisticPresetsJson` (default `string.Empty`) y `UI/appsettings.json` lo configuran como `realistic_presets.json`.
- **Loader**: `IPresetsLoader` gana `GetRealisticPresetsAsync(CancellationToken)`; `PresetsLoader` (Infrastructure) refactoriza el mapper privado a `MapToFailurePreset(dto, PresetTypeEnum)` con el tipo argumentado (Training → `TrainingMode`, Realistic → `RealisticMode`) y lee `RealisticPresetsJson` vía `EmbeddedJsonLoader`.
- **Seed**: `PresetService.InitializeAsync` siembra por tipo de forma **condicional e independiente**: primero Training (si no existe ningún `TrainingMode`), luego Realistic (si no existe ningún `RealisticMode`); idempotente por tipo y mantiene la secuencia Training → Realistic. `TestPresetsLoader` implementa `GetRealisticPresetsAsync` con un catálogo de ejemplo para conservar la interfaz.

**Por qué**

- Conectar el catálogo realista al ciclo de arranque para que la sesión Realistic arranque con candidatos sembrados por fase en lugar de depender solo de lo que ya haya en la base; el archivo anterior era una muestra de 8 presets sin cobertura sistemática.

**Áreas**

- `RealFenixFailures.Domain` — `Interfaces/IPresetsLoader.cs`, `Services/FailureEngineSettings.cs`, `Services/TestPresetsLoader.cs`, `RealFenixFailures.Domain.csproj` (EmbeddedResource), `Resources/realistic_presets.json`.
- `RealFenixFailures.Infrastructure` — `Services/PresetsLoader.cs`.
- `RealFenixFailures.Application` — `Services/PresetService.cs`.
- `RealFenixFailures.UI` — `appsettings.json`.
- `RealFenixFailures.TestUnits` — referencia a Infrastructure; nuevos `Services/PresetsLoaderTests.cs` y `Services/PresetServiceTests.cs`.
- `.gitignore` — excepción específica para que el catálogo no quede excluido por la regla general de recursos JSON (ya existente).

**Pruebas**

- `dotnet test RealFenixFailures.TestUnits/RealFenixFailures.TestUnits.csproj`: **38/38 OK**, incluidos los nuevos focalizados: `PresetsLoaderTests.GetRealisticPresetsAsync_LoadsCatalog_AllRealisticMode_WithFailuresAndFullPhaseCoverage` (loader de producción con `Options.Create(new FailureEngineSettings { RealisticPresetsJson = "realistic_presets.json" })`: 44 presets, todos `RealisticMode`, todos con definiciones de falla, cobertura de fases 1–9; abre el recurso embebido en runtime) y `PresetServiceTests` (orden Training→Realistic y omisión por tipo ya existente, con repositorio/loader mockeados; no se asserta nada sobre la base).
- `dotnet build RealFenixFailures.slnx -c Debug`: 0 errores.
- `git diff --check` sin errores.
- JSON parseado con python; los 108 IDs únicos referenciados existen en `Resources/failures.json`; verificado conteo de presets/fases/definiciones y pesos.
- No verificado: integración real MSFS/Fenix.

**Pendientes**

- Limitación del seed: si en la base ya existe **cualquier** preset `RealisticMode`, el catálogo no se mezcla ni se actualiza (no hay merge ni refresh del catálogo contra la DB).
- Relación componente-desgaste: el catálogo no define desgaste; sigue pendiente.

### 2026-10-14 — Corrección de columna duplicada `PresetType` en `FailurePresets`

**Qué**

- `RealFenixDbContext`: `FailurePreset.PresetType` (propiedad enum de conveniencia) pasa a `Ignore`; la única columna de tipo es `PresetTypeId`, mapeada a `PresetType`.
- `PresetRepository`: las consultas filtran por `PresetTypeId == (int)presetType` (una propiedad ignorada no es traducible a SQL).
- Nueva migración `20261014215307_FixFailurePresetDuplicateColumn`: elimina la columna `PresetType1`. `Up` = `DropColumn`; `Down` = `AddColumn` (INTEGER NOT NULL, default 0). `InitialCreate` no se editó (ya aplicada).

**Por qué**

- La tabla `FailurePresets` tenía dos columnas de tipo: `PresetType` (FK a `FailurePresetTypes`) y `PresetType1` (la propiedad enum, renombrada por el `ToTable` del modelo).

**Áreas**

- `RealFenixFailures.Infrastructure` — `Persistence/RealFenixDbContext.cs`, `Repositories/PresetRepository.cs`, `Migrations/`.

**Pruebas**

- `dotnet build RealFenixFailures.Infrastructure`: 0 errores.
- `dotnet test RealFenixFailures.TestUnits`: 34/34 OK.
- SQL de la migración aplicado sobre una copia de la base de la UI: la tabla queda con una sola columna `PresetType`, los 7 presets se conservan y `__EFMigrationsHistory` registra la migración. No se aplicó sobre la base real.
- No verificado: arranque de la app con la migración aplicada (la app estaba en ejecución y bloqueaba `RealFenixFailures.UI/bin`).

**Pendientes**

- `App.xaml.cs` llama a `Database.MigrateAsync()` al arrancar: la migración se aplica sola en la próxima ejecución de la app. Verificar ese arranque antes de dar el cambio por cerrado.

### 2026-10-14 — Paneles de modo translúcidos sobre el fondo de la ventana

**Qué**

- `Themes/DesignTokens.xaml` y `Themes/Brushes.xaml`: nuevos colores/pinceles translúcidos `Brush.Background.Glass.Card` (contenedor del modo, 40 % de transparencia) e `Brush.Background.Glass.Inner` (tarjetas internas, 55 %).
- `Views/MainWindow.xaml`: el contenedor del modo usa `Glass.Card`.
- `Views/Panels/{Training,Custom,Realistic}ModePanel.xaml`: las tarjetas internas (`#0D0F14`, incluido el `CardButtonStyle` de Realistic) usan `Glass.Inner`.
- El log en tiempo real, la barra de título y la barra de estado siguen opacos a propósito.

**Por qué**

- Los paneles de los modos tapaban casi todo el fondo de la ventana; se quería que se viera más.

**Áreas**

- `RealFenixFailures.UI` — `Themes/`, `Views/MainWindow.xaml`, `Views/Panels/`.

**Pruebas**

- `dotnet build RealFenixFailures.slnx -c Debug --no-restore`: 0 errores, 0 advertencias.
- Ejecución en Debug con captura de la ventana (modo Realista): el fondo se ve a través del contenedor y de las tarjetas. No verificado en Training ni Custom; no revisé legibilidad del texto sobre zonas claras del fondo.

**Pendientes**

- Ajustar los valores de transparencia si el texto pierde contraste en zonas claras.

### 2026-10-14 — Identidad visual: icono de aplicación y fondo de ventana

**Qué**

- Icono de aplicación (`Assets/Branding/app-icon.ico`, multi-tamaño 16–256 px): avión en silueta con triángulo de advertencia ámbar, sobre el fondo `#0D0F14`.
- `RealFenixFailures.UI.csproj`: `<ApplicationIcon>` (icono del `.exe`) y `<Resource>` para el icono y el fondo.
- `Views/MainWindow.xaml`: `Icon=` de la ventana y `ImageBrush` (`Brush.Background.AppImage`, `Assets/Branding/background.png`) como fondo del borde raíz. Los paneles y la barra de estado siguen siendo opacos y tapan el fondo en sus zonas.

**Por qué**

- Dar identidad visual a la app. Primera propuesta para revisión; el diseño puede cambiar.

**Áreas**

- `RealFenixFailures.UI` — `RealFenixFailures.UI.csproj`, `Views/MainWindow.xaml`, `Assets/Branding/`.

**Pruebas**

- `dotnet build RealFenixFailures.slnx -c Debug --no-restore`: 0 errores, 0 advertencias.
- Ejecución en Debug con captura de la ventana: el fondo se muestra y el layout se mantiene.
- No verificado: icono en la barra de tareas y en el explorador de archivos (no se revisó en Windows Shell); el icono de la barra de título no se ve porque la ventana usa `WindowStyle="None"`; la barra de título personalizada todavía muestra el carácter ✈.

**Pendientes**

- Decidir si el icono reemplaza el ✈ de la barra de título personalizada.
- Ajustar la intensidad del fondo si resulta demasiado azul frente a los paneles.

### 2026-10-14 — Correcciones finales de review del modo Realistic

**Qué**

- Sesión huérfana por presets resuelta: `RealisticSessionEngine.TryStartSession` ahora carga los presets (`GetRealisticPresetsAsync`) **antes** de persistir la sesión (`StartNewAsync`) y asigna la lista ya cargada a `Session.AvailablePresets`; si la carga lanza, no se persiste la sesión y `Session` queda `null`.
- Reanudación manual: `RealisticModeViewModel` expone `IsSessionPaused` y `ResumeSessionCommand` (invoca `IRealisticSessionManager.ResumeAsync(CancellationToken.None)` y se suscribe a su `PropertyChanged`); `RealisticModePanel.xaml` agrega el botón **"REANUDAR SESIÓN"** junto a la acción de detener, visible solo con la sesión pausada. Sin auto-resume.
- Fallo fatal de evaluación sin estado activo residual: `EvaluateAndExecuteAsync` devuelve una señal con la que termina `RunEvaluationLoopAsync`; al salir por fallo fatal, el manager limpia engine/scope/timer/cts, deja `IsPaused=false` y notifica `SessionError`, sin auto-esperarse (`StopAsync`/`StopLoopAsync` no se invocan desde la task del loop). Un `OperationCanceledException` y la desconexión no se tratan como fallo fatal (la desconexión sigue siendo pausa reanudable).
- Compensación distingue cancelación: si `AddTriggeredFailureAsync` arroja `OperationCanceledException`, se loguea distinto y se intenta **igual el desarme** (con `CancellationToken.None`) antes de propagar la cancelación; las demás excepciones de persistencia mantienen la compensación existente.
- Documentación corregida: `docs/integraciones/fenix-api.md` ya no menciona un campo inexistente `armed`; explica la semántica real `(Failed=false, Condition=null)` usada por reset/desarme.
- Manager testeable sin sleeps frágiles: el intervalo de evaluación es inyectable (`RealisticSessionManager`, opcional, default 30 s); los nuevos tests esperan por eventos deterministas (`TaskCompletionSource` + timeout), sin `Thread.Sleep`.

**Por qué**

- Review final antes del commit: el arranque persistía la sesión antes de cargar presets (dejaba sesión huérfana si la carga fallaba), no existía vía UI para reanudar una sesión pausada por desconexión, un fallo fatal de evaluación dejaba al manager con engine/scope y apariencia de modo activo, la compensación no distinguía cancelación y la doc de Fenix citaba un campo `armed` inexistente.

**Áreas**

- `RealFenixFailures.Application` — `RealisticSessionManager`, `RealisticSessionEngine`.
- `RealFenixFailures.UI` — `RealisticModeViewModel`, `RealisticModePanel.xaml`.
- `RealFenixFailures.TestUnits` — `RealisticSessionEngineTests`, `RealisticSessionManagerTests`.
- `docs/` — `integraciones/fenix-api.md`, `realistic-mode/estado-y-diseno.md`, `CHANGELOG.md`.

**Pruebas**

- `dotnet build RealFenixFailures.slnx -c Debug --no-restore`: 0 errores, 0 advertencias.
- `dotnet build RealFenixFailures.slnx -c Release --no-restore`: 0 errores, 0 advertencias.
- `dotnet test RealFenixFailures.TestUnits/RealFenixFailures.TestUnits.csproj --no-restore`: **34/34**.
- `git diff --check`: sin errores.
- Integración real con MSFS/Fenix: **no verificada**.

**Pendientes**

- Auto-resume de la sesión (reanudar automáticamente tras la interrupción) sigue sin implementar.
- Verificación manual de la integración real MSFS/Fenix.

### 2026-10-14 — Estabilización del modo Realistic: correcciones del WIP y línea base limpia

**Qué**

- Validación **antes de persistir la sesión**: `RealisticSessionEngine.TryStartSession` comprueba conexión y condiciones (gate con motores apagados) y solo entonces crea la `FlightSession`; un inicio fallido ya no deja estado ni sesión residual.
- Null safety: `TryStartSession` y la obtención de datos de sim comprueban `IsSuccess` y null antes de usar el valor (sin `NullReferenceException`).
- Scope por sesión: `RealisticSessionManager` (singleton) ya no recibe el `IFlightSessionService` scoped en el ctor; crea un `IServiceScope` por sesión en `StartNewSessionAsync` y lo descarta al parar.
- Pausa/reanudación idempotentes (`PauseAsync`/`ResumeAsync` solo actúan si cambia el estado) y stop que **espera el loop** de evaluación antes de cerrar la sesión.
- `FlightEventDetector` **thread-safe**: estado mutable bajo `lock` en `ProcessSnapshot`, `Reset` y propiedades.
- Fase `Landing` alcanzable: `DeterminePhase` devuelve `Landing` al aterrizar desde una fase aérea y el evento de aterrizaje se emite.
- `StopFlightSimulation` garantizado: `EngineOrchestrator.StopCurrentModeAsync` lo invoca siempre al parar cualquier modo (bloque `finally`), junto con el reset del detector.
- `finishedAt` respetado: `FlightSessionService.StopAsync` persiste el `FinishedAt` recibido.
- Compensación por desarme individual: si falla la persistencia de una falla recién armada, `TryTriggerFailureAsync` intenta desarmar esa falla en Fenix (`ISimulatorConnectionService.DisarmFailureAsync`); si ambos fallan, loguea `Critical` y la falla queda armada sin registro.
- Feed de logs de vuelo acotado a **200 entradas (FIFO)** con `Dispatcher.InvokeAsync` (`MainWindowViewModel.MaxLogEntries`).
- Advertencias de nulabilidad, obsolescencia (`ExecutionEngineException`) y variables sin uso corregidas; builds a **0 advertencias**.
- Arquitectura **x64** coherente (`PlatformTarget=x64` en UI, TestUnits e Integrations.SimConnect); resuelve **MSB3270**.
- Paquetes vulnerables actualizados (`SQLitePCLRaw.bundle_e_sqlite3` 3.0.5 y `System.Security.Cryptography.Xml` 10.0.13); resuelve **NU1903**.

**Por qué**

- El WIP de `031a9e7` dejó hallazgos de review que impedían usarlo como base firme (inicio fallido con estado residual, nulls, dependencia scoped capturada, detector no thread-safe, Landing inalcanzable, parada incompleta, logs sin límite, warnings y paquetes vulnerables). Esta estabilización los resuelve para dejar una línea base limpia a futuras tareas.

**Áreas**

- `RealFenixFailures.Application` — `RealisticSessionManager`, `RealisticSessionEngine`, `FlightEventDetector`, `FlightSessionService`, `EngineOrchestrator`, `SimulatorConnectionService`, DI e interfaces.
- `RealFenixFailures.Domain` — entidades y DTOs de fallas.
- `RealFenixFailures.Infrastructure` — `FailuresPersistenceService` y csproj (paquetes actualizados).
- `RealFenixFailures.Integrations.Fenix` — `FenixApiClient`, `FenixApiFailureService` (`DisarmFailureAsync`).
- `RealFenixFailures.Integrations.SimConnect` — csproj x64.
- `RealFenixFailures.UI` — viewmodels (feed acotado, `InvokeAsync`) y csproj x64.
- `RealFenixFailures.TestUnits` — nuevas suites de engine, manager, detector y connection service.

**Pruebas**

- `dotnet build RealFenixFailures.slnx -c Debug --no-restore`: 0 errores, 0 advertencias.
- `dotnet build RealFenixFailures.slnx -c Release --no-restore`: 0 errores, 0 advertencias.
- `dotnet test RealFenixFailures.TestUnits/RealFenixFailures.TestUnits.csproj --no-restore`: **29/29**.
- `dotnet list RealFenixFailures.slnx package --vulnerable --include-transitive`: **ningún paquete vulnerable**.
- `git diff --check`: sin errores; enlaces locales verificados: 0 rotos.
- Integración real con MSFS/Fenix: **no verificada**.

**Pendientes**

- Fallas de los modos **Training/Custom** no llegan al feed de eventos de vuelo (el log en pantalla solo recibe eventos del detector).
- No hay test de UI/WPF del límite del feed (200 entradas).
- `FenixFailures.GetAllFailureIds`/`Exists` son helpers sin uso cuya reflexión no recorre clases anidadas.
- `RemoveTemporaryFailuresAsync`, cálculo/persistencia de desgaste, incompatibilidades de fallas y configuración de constantes siguen pendientes.
- Auto-resume de la sesión no implementado.
- Verificación manual de la integración real MSFS/Fenix.

### 2026-10-14 — Reorganización de documentación y contexto para agentes

**Qué**

- Crea `AGENTS.md` como guía operativa y agrega un índice documental con fuentes únicas por tema.
- Consolida arquitectura, desarrollo, integraciones, estado del modo Realistic e historial técnico bajo `docs/`.
- Define mantenimiento documental obligatorio y el formato de ramas y commits.
- Clasifica las skills disponibles sin moverlas ni tratarlas como contexto del dominio.
- Habilita el versionado de los EmbeddedResource JSON y elimina un temporal trackeado de `.abacusai`.

**Por qué**

- Dar a futuras tareas contexto vigente y reducir contradicciones entre prompts, README y código.

**Áreas**

- `AGENTS.md`, `README.md`, `.agents/README.md`, `.gitignore`, `docs/`.

**Pruebas**

- 50 enlaces locales verificados; 0 rotos.
- `git diff --check`: sin errores.
- `dotnet build RealFenixFailures.slnx -c Debug --no-restore`: 0 errores.
- `dotnet build RealFenixFailures.slnx -c Release --no-restore`: 0 errores, 18 advertencias (ejecutado ese día; luego 0, ver la entrada de estabilización).
- `dotnet test RealFenixFailures.TestUnits/RealFenixFailures.TestUnits.csproj --no-restore`: 13/13 (ejecutado ese día; luego 29/29, ver la entrada de estabilización).
- Integración real con MSFS/Fenix: no verificada.

**Pendientes**

- Resolver los riesgos funcionales del WIP y las advertencias de dependencias/arquitectura indicadas en la guía de desarrollo; resueltas el 2026-10-14 (ver la entrada de estabilización).

### 2026-10-14 — WIP modo Realistic: manager de sesión, motor de evaluación, detector de eventos, simulación mock y UI

**Qué**

- `RealisticSessionManager` (Application): ciclo de vida de la sesión realista (start/stop/pause/resume), timer periódico de evaluación (30 s), despacho de eventos `FailureTriggered`/`SessionError`.
- `RealisticSessionEngine` (Application/Session): evaluación probabilística por tick — cooldown 60 s, máximo 3 fallas simultáneas, probabilidad base 0,10 con multiplicadores por nivel de riesgo (Low 0,5 / Moderate 1,0 / High 1,75), una falla por tick; valida conexión y avión estacionado con motores apagados al iniciar.
- `FlightEventDetector` (Application): clasificación transversal de fase de vuelo y eventos (motores, cambios de fase, cruces de altitud FL100/180/240/350); fuente única de la fase para los tres modos.
- Simulación mock de SimConnect (flight de 5 minutos con keyframes interpolados) y mock de la API de Fenix, seleccionados por DI en DEBUG.
- UI: panel y viewmodel del modo Realistic, log en pantalla de eventos de vuelo, entidades/aeronaves y desgaste.

**Por qué**

- Separar el ciclo de vida (timer, pausa, eventos) de la lógica de evaluación, para que el modo realista sea testeable e independiente del `EngineOrchestrator`.
- Tener data realista para desarrollar el motor sin depender de MSFS/Fenix corriendo.

**Áreas**

- `RealFenixFailures.Application` (Session, Services, Interfaces, DTOs)
- `RealFenixFailures.Domain` (evaluador de reglas, trigger, interfaces)
- `RealFenixFailures.Infrastructure` (repositorios de sesión/presets)
- `RealFenixFailures.Integrations.SimConnect` (mock) y `.Fenix` (mock)
- `RealFenixFailures.TestUnits` (pruebas de `FailureTrigger` y `MockSimConnectClient`)
- `RealFenixFailures.UI` (panel Realistic, log, estilos)

**Pruebas**

- `dotnet build --no-restore` desde `Realistic Failures Solution`: 0 errores y 18 advertencias (ejecutado el 2026-10-14).
- `dotnet test RealFenixFailures.TestUnits --no-restore`: 13/13 (ejecutado el 2026-10-14).
- Integración real con MSFS/Fenix: **no verificada** (pendiente de prueba manual).

**Pendientes**

- Revisión y corrección de los riesgos conocidos listados en [docs/realistic-mode/estado-y-diseno.md](realistic-mode/estado-y-diseno.md) (inicio fallido, nulls de datos de sim, persistencia posterior al armado, thread safety del detector, dependencia capturada, pause, `StopFlightSimulation`, feed de logs, fase Landing, cobertura) — **resueltos** el 2026-10-14 (ver la entrada de estabilización).
- `RemoveTemporaryFailuresAsync`, cálculo/persistencia de desgaste, incompatibilidades de fallas y configuración de constantes del motor.

---

## Plantilla para futuras entradas

```
### AAAA-MM-DD — Título breve del cambio

**Qué**
- ...

**Por qué**
- ...

**Áreas**
- ...

**Pruebas**
- (comandos y resultados reales ejecutados; si no se probó, "no verificado")

**Pendientes**
- ...
```
