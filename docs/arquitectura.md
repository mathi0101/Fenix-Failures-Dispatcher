# Arquitectura

Documento vigente del estado real del código (2026-10). No describe intenciones: describe componentes y flujos verificados en el árbol actual.

## Capas y dependencias

```
RealFenixFailures.Domain  ── sin dependencias de infraestructura
        ▲
RealFenixFailures.Application ── casos de uso, orquestación, DTOs, contratos
        ▲
Infrastructure · Integrations.Fenix · Integrations.SimConnect ── concretos
        ▲
RealFenixFailures.UI ── WPF + MVVM, compone el host y la DI
RealFenixFailures.TestUnits ── xUnit (Domain + Integrations.SimConnect)
```

- Domain: entidades (FlightSession, SessionEventLog, TriggeredFailure, UserAircraft, AircraftSystemWear, FailurePreset, PresetFailureDefinition, FenixFailureDefinition…), enums (RiskLevel, ComplexFlightPhaseEnum, FailureSeverity, SessionEventCategory…), reglas puras (`FailureRuleEvaluator`, `FailureTrigger`), `IFailureTrigger`, `IFailureRuleEvaluator`, repositorios (contratos), `EmbeddedJsonLoader`. `TriggeredFailure` guarda un **snapshot histórico** del preset/condición al disparar (nombre/severidad de la falla, preset, probabilidad/grupo, IAS, altitudes, tiempo, after-event) para que el historial no dependa del catálogo mutable.
- Application: `EngineOrchestrator`, `RealisticSessionManager`, `FlightEventDetector`, `FlightSessionService`, `SimulatorConnectionService`, DTOs, interfaces (`IFlightSessionService`, `IFlightEventDetector`, `IFlightSimulationController`, `ISimulatorConnectionService`, `IRealisticSessionManager`…).
- Infrastructure: `RealFenixDbContext` (EF Core + SQLite), repositorios, `Migrations/`, `SeedData`, `FailuresPersistenceService`, `PresetsLoader`, `UserAircraftService`, logging Serilog.
- Integrations.Fenix: `FenixApiClient` (HTTP), `MockFenixApiClient`, `FenixApiFailureService` (implementa `IFenixFailureApiDispatcher`), modelos del payload y mappers.
- Integrations.SimConnect: `SimConnectClient` (real, `IFlightSimulationController`), `MockSimConnectClient`, `SimulatorFlightDataProvider` (implementa `ISimFlightDataProvider`), `AircraftStateData`/`SimConnectAircraftState`.
- UI: `App` arranca `IHost`, aplica `MigrateAsync()`, corre cada `IInitializerService.InitializeAsync()` resolviendo `IEnumerable<IInitializerService>` (en orden de registro: `DatabaseSeeder`, punto único de orquestación del catálogo de fallas y presets; y, **solo en DEBUG**, `DebugSampleDataSeeder`, que siembra la aeronave de ejemplo con sesiones, fallas y desgaste) y muestra `MainWindow`. `MainWindowViewModel` inicia el timer automático del orquestador y suscribe eventos del detector.

En `FailurePresets`, la única columna de tipo es `PresetType`: mapea `PresetTypeId` como FK a `FailurePresetTypes`; EF ignora la propiedad enum de conveniencia `FailurePreset.PresetType`. La migración inicial `InitialRelease` crea directamente este modelo sin columna duplicada.

## DI

`HostBuilderExtensions.ConfigureRealFenixFailures()` compone: `AddDomain()`, `AddApplication()`, `AddInfrastructure(config)`, `AddSimConnectIntegration()`, `AddFenixIntegration(config)`; registra ventanas y viewmodels.

Lifetimes relevantes (Application DI):

- Scoped: `IPresetService`, `IInitializerService` (implementado por `DatabaseSeeder` y, solo en DEBUG, por `DebugSampleDataSeeder`), **`IFlightSessionService`**, `IEngineOrchestrator`.
- Singleton: `ISimulatorConnectionService`, **`IFlightEventDetector`**, **`IRealisticSessionManager`**.

El `RealisticSessionManager` (singleton) **no captura servicios scoped**: en `StartNewSessionAsync` crea un `IServiceScope` por sesión (`IServiceScopeFactory.CreateScope()`) y resuelve de ahí `IFlightSessionService`, `IPresetService` e `IFailureTrigger` para construir cada `RealisticSessionEngine`; el scope se descarta al parar (`StopAsync`) — **scope por sesión**.

Selección de integraciones por configuración de compilación (DEBUG/Release):

- SimConnect: DEBUG → `MockSimConnectClient`; Release → `SimConnectClient`. Ambos registrados como `ISimConnectClient` y `IFlightSimulationController` (ver [integraciones/simconnect.md](integraciones/simconnect.md)).
- Fenix: DEBUG → `MockFenixApiClient`; Release → `HttpClient` con `FenixApiClient` (ver [integraciones/fenix-api.md](integraciones/fenix-api.md)).

## Componentes y flujo vigente

1. **Arranque**: `App.OnStartup` primero toma `SingleInstanceGuard` (Mutex con nombre fijo); si ya hay una instancia corriendo, señala a esa instancia (`EventWaitHandle`) y cierra el proceso nuevo sin tocar el host ni la base. Si es la primera instancia, recién ahí aplica migraciones de EF y corre el initializer; `MainWindowViewModel.InitializeAsync` carga escenarios Training, Realistic y los presets Custom heredados (sin controles expuestos; el panel Custom muestra Próximamente), y arranca `StartAutomaticTimerAsync`. Tras mostrar `MainWindow`, `App` crea `TrayIconService` (icono de bandeja) y registra el listener de la señal de instancia única para restaurar la ventana. Detalle completo: [ui/instancia-unica-y-bandeja.md](ui/instancia-unica-y-bandeja.md).
2. **Timer automático** (`EngineOrchestrator`): cada `FailureEngine:CheckConnectionIntervalSeconds` (5 s) actualiza `ConnectionStatus` y, fuera del modo Realistic, alimenta al detector de eventos para no perder transiciones.
3. **Modo Realistic**:
   - `EngineOrchestrator.StartRealisticModeAsync(aircraftId, risk, ct)` valida conexión y arranca `RealisticSessionManager.StartNewSessionAsync`.
   - `RealisticSessionManager` (singleton) **maneja el ciclo de vida y el timer**: crea `RealisticSessionEngine`, arranca sesión, corre un `PeriodicTimer` de **30 s** (`EvaluationIntervalSeconds`) y despacha eventos (`FailureTriggered`, `SessionError`). Pausa/resume crean/descartan el timer.
   - `RealisticSessionEngine` (nueva instancia por sesión) **es quien evalúa**: `TryStartSession` valida conexión y avión estacionado con motores apagados, crea la `FlightSession` y carga presets realistas; `EvaluateAsync` por tick lee datos del simulador y fallas de Fenix, procesa el snapshot en el detector y ejecuta `TryTriggerFailureAsync` (cooldown 60 s, máx. 3 fallas simultáneas, probabilidad base 0,10 con multiplicador por riesgo). Cada tick dispara como máximo una falla; si la persistencia de una falla armada falla, se intenta **desarmar esa falla individualmente** en Fenix (compensación, ver [integraciones/fenix-api.md](integraciones/fenix-api.md)).
   - `FlightEventDetector` (singleton, transversal a los 3 modos) **clasifica fase y eventos**: transforma snapshots en fase actual (`ComplexFlightPhaseEnum`) y eventos de motores, cambios de fase y cruces de altitud (FL100/180/240/350); es la fuente única de la fase de vuelo y emite `FlightEventDetected` que la UI muestra en el log. Durante una sesión Realistic, `RealisticSessionEngine` se suscribe al detector **solo mientras la sesión está activa**: los eventos llegan **síncronos** a una cola interna (`_pendingFlightEvents`, con lock) y se **persisten async en cada tick** (drain en `EvaluateAsync`).
   - `FailureTrigger` + `FailureRuleEvaluator` (Domain): filtran candidatos por fase, severidad permitida por riesgo y no duplicados; selección ponderada por `Probability`. El armado real de la falla se delega a Fenix vía `ISimulatorConnectionService.ExecuteFailureAsync` (que arma la condición IAS/altitud/tiempo/AfterEvent al disparar).
   - **Persistencia**: `IFlightSessionService` crea la sesión **después** de validar las condiciones de inicio y, al cerrarla (`FlightSessionService.StopAsync`), **respeta el `finishedAt` recibido**; registra cada falla disparada (`TriggeredFailure`) en SQLite vía `FlightSessionRepository`. Con el historial de sesiones, además persiste `SessionEventLog` **append-only**: `SESSION_STARTED` al iniciar (si su registro falla, la sesión se cierra y no queda activa), `SESSION_STOPPED` al cerrar y, en cada tick, los eventos de motores/cambios de fase/cruces de altitud encolados por el detector. Cada falla se guarda **atómicamente** como `TriggeredFailure` + su evento `FAILURE_TRIGGERED` enlazado, en una misma unidad de trabajo (`AddTriggeredFailureAsync`, un único `SaveChangesAsync`).
4. **Stop**: `EngineOrchestrator.StopCurrentModeAsync` detiene el manager **esperando el loop de evaluación**, **resetea fallas activas en Fenix** (`ResetAllFailuresAsync`) y, en el bloque `finally`, **garantiza `StopFlightSimulation`** y el reset del detector.
5. **Logging y errores**: Serilog (`SerilogExtensions`) escribe a `%AppData%\RealFenixFailures\logs\realfenix-*.log` con rolling diario (ruta centralizada en `Infrastructure/AppPaths`); la UI mantiene el log de eventos del detector. En la composición de UI, `UnexpectedErrorSink` deriva excepciones inesperadas Warning/Error y niveles Fatal al `CrashReporter` común, sin dependencia WPF en las demás capas. Reportes individuales en `logs/crash`; Fatal cierra después del diálogo y Error permite continuar. Las cancelaciones/conectividad esperadas se filtran. Ver [manejo de errores y arranque](ui/manejo-de-errores-y-arranque.md).

## Decisiones verificadas

- `FlightEventDetector` recibe snapshots en el loop del modo activo (Realistic usa su loop de 30 s; el timer automático lo alimenta en el resto) y `Reset()` se llama al detener cualquier modo. Es **thread-safe**: el estado mutable (`_previousPhase`, `_enginesWereRunning`, `_hasLandedThisFlight`, `_lastAltitudeMSL`) está protegido con `lock` en `ProcessSnapshot`, `Reset` y propiedades.
- El modo Realistic requiere avión en gate con motores apagados para iniciar (verificado en `TryStartSession`).
- `UserAircraft` ya no persiste `TotalFlights`/`TotalFlightHours`: los totales son **agregados derivados** que los DTOs (`UserAircraftService.ToDto`) calculan al vuelo desde las `FlightSessions` **finalizadas** (`FinishedAt != null`) de cada aeronave; las sesiones abiertas no cuentan. La migración inicial `InitialRelease` crea el esquema sin esas columnas.
- `SessionEventLog.EventCode` es un identificador **semántico** string (p. ej. `TAXI`, `LANDING`, `ENGINES_STARTED`), no único en la tabla: puede repetirse entre eventos y sesiones; la identidad de fila es `Id`.
- Riesgos conocidos del modo Realistic y del flujo: [realistic-mode/estado-y-diseno.md](realistic-mode/estado-y-diseno.md).
