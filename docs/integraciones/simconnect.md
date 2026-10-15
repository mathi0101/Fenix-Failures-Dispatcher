# Integración SimConnect

Cliente de SimConnect para MSFS 2024 en `RealFenixFailures.Integrations.SimConnect`.

## Real y mock

- **`SimConnectClient`** (`Services/SimConnectClient.cs`) — cliente real sobre `Microsoft.FlightSimulator.SimConnect` (DLL del SDK oficial). Implementa `ISimConnectClient` e `IFlightSimulationController`; sus métodos `StartFlightSimulation`/`StopFlightSimulation` siguen siendo no-ops, pero `EngineOrchestrator.StopCurrentModeAsync` **garantiza** la invocación de `StopFlightSimulation` al detener cualquier modo (bloque `finally`).
- **`MockSimConnectClient`** — simula un vuelo sintético de 5 minutos con keyframes interpolados (gate → rodaje → despegue → ascenso → crucero → descenso → aproximación → aterrizaje → gate); útil para desarrollar sin MSFS. Su `StopFlightSimulation` resetea el vuelo sintético.
- Selección por DI según compilación: **DEBUG → mock; Release → real** (`DependencyInjection.cs`).

## Contratos

- `ISimConnectClient` (`Interfaces/ISimConnectClient.cs`): `IsConnectedAsync`, `GetAircraftStateAsync`, eventos `OnAircraftStateChanged`/`OnConnectionStateChanged`.
- `ISimFlightDataProvider` (`Application/Interfaces`): `IsConnectedAsync`, `GetAircraftRawData` → `SimulatorAircraftStateSnapshot`; implementado por **`SimulatorFlightDataProvider`** (health check cacheado 10 s con timeout de 2 s).

## Message pump y polling

- **Message pump**: `RunMessagePumpAsync` procesa `ReceiveMessage()` cada 10 ms en un hilo separado; sin esto SimConnect no dispara `OnRecvSimobjectDataBytype` (problema histórico, ver `docs/historial/2026-simconnect-fixes.md`).
- **Data request loop**: `RunDataRequestLoopAsync` pide datos cada 100 ms (10 Hz) con `RequestDataOnSimObjectType` sobre el objeto `USER`.
- El orden de las definiciones de datos debe coincidir exactamente con el struct `AircraftStateData` (`RegisterDataDefinitions`).

## Thread safety

- `ReaderWriterLockSlim _stateReadWriteLock` protege `_lastKnownState`; `SemaphoreSlim _connectionLock` protege la conexión.
- `GetAircraftStateAsync` devuelve un clon bajo read lock.

## Datos

`AircraftStateData` (struct sequential): latitud, longitud, altitud presión/indicada, heading, velocidades, vertical speed, `SIM ON GROUND` (INT32), flaps, combustión motores (INT32), N1 y throttle (double), radio altitud. Se mapea a `SimConnectAircraftState` (con algunas conversiones a `int`, por ejemplo `AltitudeMSL`, `Heading`, `GroundSpeed`).

## Log de diagnóstico (SimConnect + estados calculados)

Activable con el flag `SimConnectDiagnostics:Enabled` en `appsettings.json` (default `false`). Pensado para comparar en desarrollo lo que SimConnect reporta contra lo que la app calcula (fase de vuelo, motores, etc.) durante pruebas en vivo.

- `SerilogExtensions.ApplyDefaultConfiguration` (`RealFenixFailures.Infrastructure/Logging/SerilogExtensions.cs`) separa el pipeline de Serilog en dos: el logger principal excluye (vía `Matching.FromSource`) todo evento cuya categoría sea `SimConnect.Diagnostics`, y, solo si el flag está en `true`, agrega un segundo `WriteTo.Logger` que incluye únicamente esa categoría y escribe en `logs/simconnect-diagnostic-.log` (rolling diario, igual convención que el log principal `logs/realfenix-.log`). En ese caso aplica además un `MinimumLevel.Override` a `Debug` **solo** para la categoría `SimConnect.Diagnostics` sobre el logger raíz, de modo que los eventos `Debug` de esa categoría llegan al archivo de diagnóstico aun si el nivel mínimo general del logger raíz es más restrictivo (p. ej. `Information` en la configuración publicada); el resto de categorías conserva sus niveles generales. El flag se lee una sola vez al arrancar: cambiarlo exige reiniciar la aplicación.
- Dos emisores usan esa categoría fija (`ILoggerFactory.CreateLogger("SimConnect.Diagnostics")`, no `ILogger<T>`, para no acoplar Integrations/Application a Infrastructure):
  - `SimulatorFlightDataProvider.GetAircraftRawData` (Integrations.SimConnect) — loguea el dato crudo recibido de SimConnect en cada poll (posición, altitudes, velocidades, motores, throttle).
  - `FlightEventDetector.ProcessSnapshot` (Application) — loguea la fase de vuelo calculada (`ComplexFlightPhaseEnum`) y el estado derivado (motores corriendo, AGL/MSL, VS, GS/TAS).
- Si el flag está en `false`, los `LogDebug` igual se ejecutan (costo mínimo) pero no tienen sink de destino, así que no generan archivo ni ruido en el log principal.
- La categoría `"SimConnect.Diagnostics"` está duplicada como literal en Infrastructure, Integrations.SimConnect y Application (no hay dependencia compartida entre esas capas); si se renombra, actualizar los tres lugares.
- No reemplaza los logs operativos existentes de `SimConnectClient`/`SimulatorFlightDataProvider` (conexión, errores, timeouts), que siguen yendo al log principal.

## Obtención de la DLL del SDK

La referencia se resuelve contra `MSFS 2024 SDK\SimConnect SDK\lib\managed\Microsoft.FlightSimulator.SimConnect.dll` (ruta relativa desde el csproj) o `libs\Microsoft.FlightSimulator.SimConnect.dll` local si existe. Instalación del SDK: MSFS → Developer Mode → Tools → SDK Installer. La DLL `SimConnect.dll` (nativa, sin manager) se copia a salida con `CopyToOutputDirectory=PreserveNewest`; no moverla ni eliminarla.

## Advertencias verificadas

- **MSB3270 (AMD64 vs MSIL)**: **resuelto** al fijar `PlatformTarget=x64` en el csproj de la integración (y en UI/TestUnits); los builds actuales no lo reportan (0 advertencias). Al tocar la integración, no volver a MSIL ni desalinear la arquitectura objetivo.
- **Reconexión**: `IsConnectedAsync` detecta timeout de datos (> 10 s) y **dispara un intento** de reconexión, pero esto **no está verificado manualmente**; no afirmar reconexión automática garantizada sin prueba real.

## Pruebas manuales pendientes

- Conexión real con MSFS 2024 corriendo.
- Recepción periódica de datos ("Aircraft state updated" en logs).
- Transiciones de fase reales (Parked → Taxi → Takeoff → Climb, etc.).
- Reconexión tras cerrar/abrir MSFS.
- Performance (CPU/memoria) en sesión larga.

Historial del problema de conexión (message pump, tipos, thread safety): [docs/historial/2026-simconnect-fixes.md](../historial/2026-simconnect-fixes.md).
