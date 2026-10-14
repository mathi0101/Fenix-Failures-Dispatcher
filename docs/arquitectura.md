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

- Domain: entidades (FlightSession, UserAircraft, AircraftSystemWear, FailurePreset, PresetFailureDefinition, FenixFailureDefinition…), enums (RiskLevel, ComplexFlightPhaseEnum, FailureSeverity…), reglas puras (`FailureRuleEvaluator`, `FailureTrigger`), `IFailureTrigger`, `IFailureRuleEvaluator`, repositorios (contratos), `EmbeddedJsonLoader`.
- Application: `EngineOrchestrator`, `RealisticSessionManager`, `FlightEventDetector`, `FlightSessionService`, `SimulatorConnectionService`, DTOs, interfaces (`IFlightSessionService`, `IFlightEventDetector`, `IFlightSimulationController`, `ISimulatorConnectionService`, `IRealisticSessionManager`…).
- Infrastructure: `RealFenixDbContext` (EF Core + SQLite), repositorios, `Migrations/`, `SeedData`, `FailuresPersistenceService`, `PresetsLoader`, `UserAircraftService`, logging Serilog.
- Integrations.Fenix: `FenixApiClient` (HTTP), `MockFenixApiClient`, `FenixApiFailureService` (implementa `IFenixFailureApiDispatcher`), modelos del payload y mappers.
- Integrations.SimConnect: `SimConnectClient` (real, `IFlightSimulationController`), `MockSimConnectClient`, `SimulatorFlightDataProvider` (implementa `ISimFlightDataProvider`), `AircraftStateData`/`SimConnectAircraftState`.
- UI: `App` arranca `IHost`, aplica `MigrateAsync()`, corre `IInitializerService.InitializeAsync()` (carga fallas iniciales y presets seed) y muestra `MainWindow`. `MainWindowViewModel` inicia el timer automático del orquestador y suscribe eventos del detector.

## DI

`HostBuilderExtensions.ConfigureRealFenixFailures()` compone: `AddDomain()`, `AddApplication()`, `AddInfrastructure(config)`, `AddSimConnectIntegration()`, `AddFenixIntegration(config)`; registra ventanas y viewmodels.

Lifetimes relevantes (Application DI):

- Scoped: `IPresetService`, `IInitializerService`, **`IFlightSessionService`**, `IEngineOrchestrator`.
- Singleton: `ISimulatorConnectionService`, **`IFlightEventDetector`**, **`IRealisticSessionManager`**.

El `RealisticSessionManager` (singleton) **no captura servicios scoped**: en `StartNewSessionAsync` crea un `IServiceScope` por sesión (`IServiceScopeFactory.CreateScope()`) y resuelve de ahí `IFlightSessionService`, `IPresetService` e `IFailureTrigger` para construir cada `RealisticSessionEngine`; el scope se descarta al parar (`StopAsync`) — **scope por sesión**.

Selección de integraciones por configuración de compilación (DEBUG/Release):

- SimConnect: DEBUG → `MockSimConnectClient`; Release → `SimConnectClient`. Ambos registrados como `ISimConnectClient` y `IFlightSimulationController` (ver [integraciones/simconnect.md](integraciones/simconnect.md)).
- Fenix: DEBUG → `MockFenixApiClient`; Release → `HttpClient` con `FenixApiClient` (ver [integraciones/fenix-api.md](integraciones/fenix-api.md)).

## Componentes y flujo vigente

1. **Arranque**: `App` aplica migraciones de EF y corre el initializer; `MainWindowViewModel.InitializeAsync` carga escenarios Training, Realistic y Custom, y arranca `StartAutomaticTimerAsync`.
2. **Timer automático** (`EngineOrchestrator`): cada `FailureEngine:CheckConnectionIntervalSeconds` (5 s) actualiza `ConnectionStatus` y, fuera del modo Realistic, alimenta al detector de eventos para no perder transiciones.
3. **Modo Realistic**:
   - `EngineOrchestrator.StartRealisticModeAsync(aircraftId, risk, ct)` valida conexión y arranca `RealisticSessionManager.StartNewSessionAsync`.
   - `RealisticSessionManager` (singleton) **maneja el ciclo de vida y el timer**: crea `RealisticSessionEngine`, arranca sesión, corre un `PeriodicTimer` de **30 s** (`EvaluationIntervalSeconds`) y despacha eventos (`FailureTriggered`, `SessionError`). Pausa/resume crean/descartan el timer.
   - `RealisticSessionEngine` (nueva instancia por sesión) **es quien evalúa**: `TryStartSession` valida conexión y avión estacionado con motores apagados, crea la `FlightSession` y carga presets realistas; `EvaluateAsync` por tick lee datos del simulador y fallas de Fenix, procesa el snapshot en el detector y ejecuta `TryTriggerFailureAsync` (cooldown 60 s, máx. 3 fallas simultáneas, probabilidad base 0,10 con multiplicador por riesgo). Cada tick dispara como máximo una falla; si la persistencia de una falla armada falla, se intenta **desarmar esa falla individualmente** en Fenix (compensación, ver [integraciones/fenix-api.md](integraciones/fenix-api.md)).
   - `FlightEventDetector` (singleton, transversal a los 3 modos) **clasifica fase y eventos**: transforma snapshots en fase actual (`ComplexFlightPhaseEnum`) y eventos de motores, cambios de fase y cruces de altitud (FL100/180/240/350); es la fuente única de la fase de vuelo y emite `FlightEventDetected` que la UI muestra en el log.
   - `FailureTrigger` + `FailureRuleEvaluator` (Domain): filtran candidatos por fase, severidad permitida por riesgo y no duplicados; selección ponderada por `Probability`. El armado real de la falla se delega a Fenix vía `ISimulatorConnectionService.ExecuteFailureAsync` (que arma la condición IAS/altitud/tiempo/AfterEvent al disparar).
   - **Persistencia**: `IFlightSessionService` crea la sesión **después** de validar las condiciones de inicio y, al cerrarla (`FlightSessionService.StopAsync`), **respeta el `finishedAt` recibido**; registra cada falla disparada (`TriggeredFailure`) en SQLite vía `FlightSessionRepository`.
4. **Stop**: `EngineOrchestrator.StopCurrentModeAsync` detiene el manager **esperando el loop de evaluación**, **resetea fallas activas en Fenix** (`ResetAllFailuresAsync`) y, en el bloque `finally`, **garantiza `StopFlightSimulation`** y el reset del detector.
5. **Logging**: Serilog (`SerilogExtensions`) escribe a `logs/realfenix-*.log` con rolling diario; la UI mantiene un log en pantalla alimentado por eventos del detector.

## Decisiones verificadas

- `FlightEventDetector` recibe snapshots en el loop del modo activo (Realistic usa su loop de 30 s; el timer automático lo alimenta en el resto) y `Reset()` se llama al detener cualquier modo. Es **thread-safe**: el estado mutable (`_previousPhase`, `_enginesWereRunning`, `_hasLandedThisFlight`, `_lastAltitudeMSL`) está protegido con `lock` en `ProcessSnapshot`, `Reset` y propiedades.
- El modo Realistic requiere avión en gate con motores apagados para iniciar (verificado en `TryStartSession`).
- Riesgos conocidos del modo Realistic y del flujo: [realistic-mode/estado-y-diseno.md](realistic-mode/estado-y-diseno.md).
