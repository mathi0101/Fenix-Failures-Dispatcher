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
