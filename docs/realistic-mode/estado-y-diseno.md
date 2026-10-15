# Modo Realistic — estado y diseño

Documento **vigente** del modo Realistic (2026-10). Reemplaza el diseño inicial que vivía en `Agent Prompts/prompt1.md`: ese archivo quedó **reemplazado por este documento** y no debe usarse como fuente (está sin versionar/ignorado; conservar solo como antecedente histórico, no como especificación).

Estado conjunto: el WIP original quedó committeado como `031a9e7` (branch `dev`); la estabilización de 2026-10-14 está aplicada en el árbol de trabajo y committeada, y las correcciones de telemetría y estabilización de fase de **2026-10-15** (detalladas abajo) quedaron committeadas en `dev` con el resto del working tree.

## Implementado (verificado en código)

- **`RealisticSessionManager`** (`RealFenixFailures.Application/Services/RealisticSessionManager.cs`) — singleton; **responsable del ciclo de vida y el timer**: `StartNewSessionAsync`, `StopAsync`, `PauseAsync`, `ResumeAsync`; `PeriodicTimer` de evaluación de **30 s** (`EvaluationIntervalSeconds`); despacha `FailureTriggered` y `SessionError` hacia la UI.
- **`RealisticSessionEngine`** (`RealFenixFailures.Application/Session/RealisticSessionEngine.cs`) — nueva instancia por sesión; **responsable de la evaluación**: `TryStartSession` (valida conexión Fenix+SimConnect y avión en gate con motores apagados), `EvaluateAsync` por tick, `TryTriggerFailureAsync`.
- **Reanudación manual desde la UI**: `RealisticModeViewModel` expone `IsSessionPaused` y `ResumeSessionCommand` (invoca `IRealisticSessionManager.ResumeAsync(CancellationToken.None)`, suscrito a su `PropertyChanged`); `RealisticModePanel.xaml` muestra el botón **"REANUDAR SESIÓN"** junto a la acción de detener, visible solo cuando la sesión está pausada. No hay auto-resume.
- **`RealisticSession`** (`RealFenixFailures.Application/Session/RealisticSession.cs`) — estado mutado por el engine: fase actual, datos del sim, fallas Fenix armadas/falladas, `LastTriggeredAtUtc`, presets disponibles, `ActiveFailureCount`.
- **`FlightEventDetector`** (singleton, transversal) — clasifica fase y eventos; fuente única de fase.
- **Persistencia del WIP**: `IFlightSessionService` crea/cierra sesión (`FlightSession` con `RiskLevel` y `UserAircraftId`), registra `TriggeredFailure` por falla disparada y persiste además **`SessionEventLog` append-only** durante las sesiones Realistic: `SESSION_STARTED`, `SESSION_STOPPED`, eventos de motores, cambios de fase, cruces de altitud y fallas disparadas (detalle en la sección "Historial de sesiones y totales derivados (2026-10-15)").
- **Provisión de presets realistas (seed)** — `PresetService.InitializeAsync` siembra por tipo de forma **condicional e independiente**: si no existe ningún preset `TrainingMode`, carga `training_presets.json` (embedded resource) vía `IPresetsLoader.GetTrainingPresetsAsync` y los inserta; después hace el mismo chequeo para `RealisticMode` con `realistic_presets.json` (`GetRealisticPresetsAsync`). La secuencia es siempre primero Training y luego Realistic; por tipo es idempotente (si ya existe alguno, no recarga ni reinserta). El catálogo realista (44 presets) queda en la base como candidatos y la sesión Realistic los consume desde el repositorio (`GetRealisticPresetsAsync` lee de la DB).
- **Arranque desde UI**: `EngineOrchestrator.StartRealisticModeAsync` → manager; requiere conexión previa.
- **Mock de SimConnect** en DEBUG (vuelo sintético de 5 min) y **mock de Fenix** en DEBUG.

### Constantes verificadas

| Constante | Valor | Lugar |
|---|---|---|
| Probabilidad base por tick | `BaseProbability = 0.10` | `RealisticSessionEngine` |
| Cooldown entre fallas | `MinCooldownSeconds = 60` | `RealisticSessionEngine` |
| Máx. fallas simultáneas | `MaxSimultaneousActiveFailures = 3` | `RealisticSessionEngine` |
| Multiplicador Low | 0.5 | `RiskProbabilityMultiplier` |
| Multiplicador Moderate | 1.0 | `RiskProbabilityMultiplier` |
| Multiplicador High | 1.75 | `RiskProbabilityMultiplier` |
| Intervalo de evaluación | 30 s | `RealisticSessionManager` |

Significado de `Probability` por definición (realistic): **peso relativo de selección** entre las fallas elegibles del preset; `FailureTrigger.TryTriggerFailure` elige por acumulación ponderada sobre la suma de pesos (`totalWeight`). No es la probabilidad de disparo de la sesión: esa es `BaseProbability` (0.10) × multiplicador de riesgo, aplicada por preset candidato en cada tick; el multiplicador de riesgo no modifica los `Probability` individuales.

`Difficulty` es metadato de clasificación (Easy=1, Medium=2, Hard=3, Insane=4); no está conectado al filtro de riesgo del modo Realistic (el filtro opera por severidad de la falla Fenix en `FailureRuleEvaluator`, no por `Difficulty` del preset).

Regla de severidad (`FailureRuleEvaluator`): Low → solo `Minor`; Moderate → `Minor`/`Major`; High → todas.

## Estabilización verificada (2026-10-14)

Correcciones aplicadas sobre el WIP (`031a9e7`) y **verificadas por build y tests** (aun **no committeadas**). Referencias con ruta/símbolo.

- **Validación antes de persistir sesión** — `RealisticSessionEngine.TryStartSession` (`Application/Session/RealisticSessionEngine.cs`) valida conexión y condiciones (gate con motores apagados) y solo después crea la `FlightSession`; si el inicio falla, `RealisticSessionManager.StartNewSessionAsync` descarta el scope y deja `SessionState == null` (sin estado residual).
- **Presets antes de persistir la sesión (sesión huérfana resuelta)** — `TryStartSession` carga `GetRealisticPresetsAsync` ANTES de `StartNewAsync` y asigna esa lista ya cargada a `Session.AvailablePresets`; si la carga de presets lanza, `StartNewAsync` no se llama y `Session` queda `null` (no queda sesión persistida huérfana).
- **Fallo fatal de evaluación sin estado activo residual** — `EvaluateAndExecuteAsync` devuelve una señal para que `RunEvaluationLoopAsync` termine; al salir por fallo fatal, el manager limpia engine/scope/timer/cts, deja `IsPaused=false` y notifica `SessionError`. El cierre se ejecuta dentro de la task del loop sin auto-esperarse (no invoca `StopAsync`/`StopLoopAsync`, que esperarían `_evaluationLoopTask`); un `OperationCanceledException` o la desconexión no se tratan como fallo fatal (la desconexión sigue siendo pausa reanudable).
- **Compensación distingue cancelación** — en `TryTriggerFailureAsync`, si `AddTriggeredFailureAsync` arroja `OperationCanceledException` se loguea distinto y se intenta **igual el desarme** (con `CancellationToken.None`) antes de propagar la cancelación; el resto de errores de persistencia mantienen la compensación existente.
- **Documentación de desarme sin campo `armed`** — [integraciones/fenix-api.md](../integraciones/fenix-api.md) ya no menciona un campo inexistente; la semántica real es `(Failed=false, Condition=null)` (una falla está armada cuando `FailureCondition != null` o `Failed == true`).
- **Null safety** — `TryStartSession` y la obtención de datos de sim comprueban `IsSuccess` y null antes de usar el valor (no hay accesos `Value!` sin verificar).
- **Scope por sesión** — `RealisticSessionManager` (singleton) crea un `IServiceScope` por sesión (`IServiceScopeFactory.CreateScope()`) y resuelve de ahí `IFlightSessionService`/`IPresetService`/`IFailureTrigger`; el scope se descarta en `StopAsync`. Ya no captura el servicio scoped en el ctor.
- **Pausa/reanudación idempotentes** — `PauseAsync`/`ResumeAsync` no actúan si el estado no cambia; `StopLoopAsync` cancela el `PeriodicTimer` y **espera** la tarea del loop antes de cerrar la sesión.
- **Detector thread-safe** — `FlightEventDetector` (`Application/Services/FlightEventDetector.cs`): estado mutable protegido con `lock (_sync)` en `ProcessSnapshot`, `Reset` y propiedades.
- **Landing alcanzable** — `DeterminePhase` devuelve `ComplexFlightPhaseEnum.Landing` al aterrizar desde una fase aérea y el evento de aterrizaje se emite.
- **StopFlightSimulation garantizado** — `EngineOrchestrator.StopCurrentModeAsync` invoca `IFlightSimulationController.StopFlightSimulation` en el bloque `finally` para cualquier modo (antes solo se llamaba al iniciar).
- **finishedAt respetado** — `FlightSessionService.StopAsync(sessionId, finishedAt, ct)` persiste ese `FinishedAt`; `RealisticSessionEngine.StopSession` cierra la sesión con `DateTime.UtcNow`.
- **Compensación de persistencia** — `RealisticSessionEngine.TryTriggerFailureAsync`: si `AddTriggeredFailureAsync` falla tras armar la falla, se intenta el **desarme individual** (`ISimulatorConnectionService.DisarmFailureAsync`); si ambos fallan, loguea `Critical` (la falla queda armada sin registrar). Detalle en [integraciones/fenix-api.md](../integraciones/fenix-api.md).
- **Feed de logs acotado** — `MainWindowViewModel.FlightEventDetector_FlightEventDetected` inserta en `LogEntries` vía `Dispatcher.InvokeAsync` y recorta el excedente sobre `MaxLogEntries = 200` (FIFO).
- **Warnings, arquitectura y paquetes** — nulabilidad, `ExecutionEngineException` obsoleta y variables sin uso corregidas; `PlatformTarget=x64` en UI, TestUnits e Integrations.SimConnect (resuelve MSB3270); dependencias actualizadas (resuelve NU1903).

**Verificación (2026-10-14)**

- `dotnet build RealFenixFailures.slnx -c Debug --no-restore`: 0 errores, 0 advertencias.
- `dotnet build RealFenixFailures.slnx -c Release --no-restore`: 0 errores, 0 advertencias.
- `dotnet test RealFenixFailures.TestUnits/RealFenixFailures.TestUnits.csproj --no-restore`: **34/34** (se suman los focalizados de esta revisión: presets-huérfanos, cierre fatal del manager, pausa por desconexión reanudable y compensación por cancelación).
- `dotnet list RealFenixFailures.slnx package --vulnerable --include-transitive`: ningún paquete vulnerable.
- Integración real MSFS/Fenix: **no verificada**.

## Estabilización de telemetría y fase (2026-10-15)

Correcciones aplicadas y **verificadas por build y tests** (49/49) en `dev`. Referencias con ruta/símbolo.

- **SimVars correctos para altitud y velocidad indicada** — el cliente real (`RealFenixFailures.Integrations.SimConnect/Services/SimConnectClient.cs`) solicita ahora `PLANE ALTITUDE` (altitud de la aeronave en pies, mapeada a `AltitudeMSL`) en lugar de `PRESSURE ALTITUDE`, y agrega `AIRSPEED INDICATED` (knots, FLOAT64) como telemetría adicional; el campo del struct se renombra a `PlaneAltitude` y `AirspeedIndicated` se propaga a `SimConnectAircraftState`, `SimulatorAircraftStateSnapshot`, clones, provider y logs de diagnóstico. El orden del struct y de `AddToDataDefinition` se mantiene sincronizado. **No** se usan `L:Vars` ni se captura combustible (sin base).
- **Redondeo solo en la representación de diagnóstico** — `SimulatorFlightDataProvider.GetAircraftRawData` redondea lat/lon a 5 decimales y N1/throttle a 1 decimal en el log de diagnóstico; los valores internos conservan su precisión (no se redondean/alteran arbitrariamente).
- **Flag `HasSimulatorData`** — `SimulatorAircraftStateSnapshot.HasSimulatorData` (default `false`, `init`, copiado en `Clone`) lo marca `SimulatorFlightDataProvider` desde `SimConnectAircraftState.IsConnected`. `SimConnectClient` limpia el último estado a `new()` y `_lastDataReceivedUtc = DateTime.MinValue` bajo el lock al iniciar una nueva conexión, al desconectar y al recibir Quit, para que no sobreviva una muestra obsoleta a la reconexión.
- **Detector sin datos** — `FlightEventDetector.ProcessSnapshot` con `HasSimulatorData=false` no procesa transiciones/eventos/altitud y devuelve la fase confirmada actual (limpia el candidato de debounce pendiente), evitando que una muestra inicial o intermedia de datos inválidos genere fase espuria.
- **Bandas VS simétricas** — `DeterminePhase`: debajo de 5000 ft AGL, `VS>200` Climb, `VS<-200` Approach, resto Cruise; a/por encima de 5000 ft AGL, `VS>200` Climb, `VS<-200` Descent, resto Cruise (antes la banda `|VS| 100..200` por encima de 5000 clasificaba ascenso suave como Descent).
- **Debounce temporal por `ObservedAt`** — transiciones aéreas (Climb/Cruise/Descent/Approach) requieren lecturas consistentes: Cruise 60 s, Descent 45 s, Climb/Approach 10 s. La primera fase conocida se acepta al instante; las fases de tierra y Landing se aplican inmediatamente; dos candidatos distintos limpian/reinician el tiempo; al perder datos se borra el candidato. La cadencia de evaluación (5 s general / 30 s Realistic) no cambia: en modo 30 s la confirmación cae en el tick que supera el límite.
- **Carrera de despegue** — estando en tierra: `throttle>80` y `GS>50` ⇒ Takeoff; `GS>5` ⇒ Taxi (antes Takeoff requiría `GS<=5` con throttle alto y era inalcanzable en carrera).
- **Latch de aterrizaje** — tras Landing, mientras `_hasLandedThisFlight` y no se observa Takeoff, una lectura aérea espuria de nivel bajo no vuelve a Cruise: se mantiene la fase terrestre confirmada. El despegue real pasa por la fase Takeoff, que limpia el latch (se conserva el texto "Taxi final hacia el gate").
- **Engine sin datos** — `RealisticSessionEngine.EvaluateAsync` con `HasSimulatorData=false` no actualiza `RealisticSession` ni intenta disparar una falla en ese tick: loguea debug y devuelve `EvaluationResult.NoAction()`.

**Verificación (2026-10-15)**

- `dotnet build RealFenixFailures.slnx -c Debug`: 0 errores, 0 advertencias.
- `dotnet test RealFenixFailures.TestUnits/RealFenixFailures.TestUnits.csproj`: **49/49** (se suman: snapshot inválido inicial y entre muestras, VS +150 >5000 no es Descent, debounce Cruise 60 s y Descent 45 s, Landing inmediata, lectura aérea tras aterrizaje sin retorno a Cruise, Takeoff alcanzable, y engine con snapshot sin datos).
- Integración real MSFS/Fenix: **no verificada**; los ceros de `PLANE ALTITUDE`/`AIRSPEED INDICATED` en el simulador real siguen siendo una hipótesis hasta revisar un log real de diagnóstico.

## Historial de sesiones y totales derivados (2026-10-15)

Cambio aplicado y verificado por builds focalizados y tests (55/55); verificación al final de la sección. Referencias con ruta/símbolo.

- **`SessionEventLog` append-only por sesión** — `IFlightSessionService` persiste la nueva entidad `SessionEventLog` (`Domain/Entities/SessionEventLog.cs`) durante las sesiones Realistic: `SESSION_STARTED` al iniciar, `SESSION_STOPPED` al cerrar (antes de `StopAsync`), eventos de motores, cambios de fase y cruces de altitud, y `FAILURE_TRIGGERED` por cada falla disparada. Cada evento guarda `OccurredAt`, categoría (`SessionEventCategory`), descripción, fase de vuelo y el `TriggeredFailureId` enlazado cuando corresponde. `EventCode` es un identificador semántico string (p. ej. `TAXI`, `LANDING`) que puede repetirse entre eventos/sesiones; la identidad de fila es `Id`.
- **Suscripción al detector solo durante la sesión activa** — `RealisticSessionEngine` (`Application/Session/RealisticSessionEngine.cs`) se suscribe a `FlightEventDetector.FlightEventDetected` al iniciar la sesión y se desuscribe al cerrarla; los eventos llegan **síncronos** a una cola `_pendingFlightEvents` (protegida con lock) y se **persisten async en cada tick** (`DrainFlightEventsAsync` dentro de `EvaluateAsync`). El `SESSION_STARTED` se registra antes de suscribirse; si su registro falla, la sesión se cierra y no queda activa.
- **Falla + evento en una unidad de trabajo** — `FlightSessionService.AddTriggeredFailureAsync` (`Application/Services/FlightSessionService.cs`) guarda el `TriggeredFailure` y su evento `FAILURE_TRIGGERED` enlazado y persiste ambos con un único `SaveChangesAsync` (atómico).
- **Snapshot histórico en `TriggeredFailure`** — la entidad (`Domain/Entities/TriggeredFailure.cs`) ahora copia al disparar el detalle completo de la condición del preset: nombre y severidad de la falla, `PresetId`/`PresetName`, `Probability`/`ProbabilityGroup`, `Ias`, altitudes (`AboveAltitude`/`BelowAltitude`), `Time` y `AfterEvent`/`AfterEventSeconds`. El historial no depende del catálogo mutable.
- **Totales de aeronave derivados** — `UserAircraft.TotalFlights` y `UserAircraft.TotalFlightHours` ya **no se almacenan** (columnas eliminadas por la migración): los DTOs (`UserAircraftService.ToDto`) los calculan desde las `FlightSessions` **finalizadas** de la aeronave (`FinishedAt != null`); las sesiones abiertas no cuentan.

**Verificación (2026-10-15 — historial y totales derivados)**

- `dotnet test RealFenixFailures.TestUnits/RealFenixFailures.TestUnits.csproj -c Debug --no-restore`: **55/55**.
- `dotnet build RealFenixFailures.slnx -c Debug --no-restore`: 0 errores, 0 advertencias.
- `dotnet build RealFenixFailures.slnx -c Release --no-restore`: 0 errores, 0 advertencias.
- La migración se aplicó sobre una base temporal y se verificaron `Up`, `Down` y una nueva aplicación de `Up`; la base real no se modificó. Sin verificación manual; las integraciones reales MSFS/Fenix siguen sin verificar (ver "Limitaciones restantes").

## En desarrollo

- Verificación manual de la integración real MSFS/Fenix (los mocks de DEBUG y los 29 tests no reemplazan la prueba real).

## Pendiente

- **`RemoveTemporaryFailuresAsync`** — eliminación de fallas temporales de emergencia (prevista en el diseño, no implementada).
- **Provisión de presets realistas — limitación del seed** — el sembrado es de una sola vía: si en la base ya existe **cualquier** preset `RealisticMode` (propio o de otra fuente), el catálogo no se mezcla ni se actualiza (no hay merge ni refresh del catálogo contra la DB). El catálogo tampoco define relación componente-desgaste (sigue pendiente).
- **Cálculo y persistencia de desgaste** — `AircraftSystemWear`/delta por vuelo, amplificación de probabilidad por desgaste (`probabilidadFinal = base × (1 + factor × wear/100)`); no implementado.
- **Incompatibilidades de fallas** — evitar fallas mutuamente excluyentes simultáneas; no implementado.
- **Configuración de constantes** — probabilidad base, cooldown, máx. simultáneas y multiplicadores están como `const` en código; se prevé moverlas a configuración.
- Completar el uso funcional del modelado de aeronaves y desgaste ya existente (`UserAircraft`, `AircraftWearableSystem`, `AircraftSystemWear`), especialmente la acumulación de **desgaste** por vuelo y su efecto sobre las probabilidades (los totales de horas/vuelos ya no son pendiente: se calculan desde las sesiones finalizadas, ver sección de 2026-10-15).
- **Auto-resume** — reanudar automáticamente la sesión tras una interrupción; no implementado.

## Decisiones abiertas

- Valores numéricos finales del factor de desgaste por nivel de riesgo.
- Mecanismo de notificación manager → UI (hoy eventos `EventHandler` + `INotifyPropertyChanged`).
- Vincular fallas Fenix ↔ sistema desgastable: FK directa vs tabla de mapeo.

## Limitaciones restantes (no resueltas)

1. **Fallas de Training/Custom fuera del feed de eventos** — el log en pantalla (`MainWindowViewModel.LogEntries`) solo recibe eventos del detector; las fallas disparadas por los modos Training/Custom no llegan a ese feed. Referencia: `MainWindowViewModel.FlightEventDetector_FlightEventDetected`.
2. **Sin test de UI/WPF del límite del feed** — el tope de 200 entradas (`MaxLogEntries`) no tiene cobertura a nivel de viewmodel/WPF.
3. **Helpers sin uso con reflexión parcial** — `FenixFailures.GetAllFailureIds()` y `FenixFailures.Exists(id)` (`Domain/DTOs/FenixFailures.cs`) no se usan en la app y su reflexión no recorre clases anidadas (puede omitir fallas agrupadas).
4. **Backlog de diseño** — `RemoveTemporaryFailuresAsync`, desgaste, incompatibilidades de fallas y configuración de constantes siguen pendientes (ver "Pendiente").
5. **Auto-resume** — no implementado (ver "Pendiente").
6. **Integración real MSFS/Fenix sin verificar** — builds y tests no reemplazan la prueba manual.
7. **Historial persistido solo en el modo Realistic** — `FlightSession`, `SessionEventLog` y `TriggeredFailure` solo se crean desde `RealisticSessionEngine` durante sesiones Realistic; los modos Training/Custom no persisten sesiones ni historial (igual que antes de este cambio).

## Notas

- El `prompt1.md` original era diseño inicial; ante cualquier conflicto entre ambos, manda este documento + el código.
- Detalles de integraciones: [integraciones/simconnect.md](../integraciones/simconnect.md) y [integraciones/fenix-api.md](../integraciones/fenix-api.md).
