# CHANGELOG

Bitácora técnica del proyecto. No es un changelog de release: registra **qué** cambió, **por qué**, **qué áreas** tocó, **qué pruebas** se corrieron y **qué queda pendiente**. Solo se agregan entradas; no se reescriben ni se eliminan entradas pasadas.

## [Sin publicar]

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
