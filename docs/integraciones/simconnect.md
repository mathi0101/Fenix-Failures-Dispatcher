# Integración SimConnect

Cliente de SimConnect para MSFS 2024 en `RealFenixFailures.Integrations.SimConnect`.

## Real y mock

- **`SimConnectClient`** (`Services/SimConnectClient.cs`) — cliente real sobre `Microsoft.FlightSimulator.SimConnect` (DLL del SDK oficial). Implementa `ISimConnectClient` e `IFlightSimulationController`; sus métodos `StartFlightSimulation`/`StopFlightSimulation` siguen siendo no-ops, pero `EngineOrchestrator.StopCurrentModeAsync` **garantiza** la invocación de `StopFlightSimulation` al detener cualquier modo (bloque `finally`).
- **`MockSimConnectClient`** — simula un vuelo sintético de **~2h35 puerta-a-puerta** con keyframes interpolados y deterministas (gate → pushback → arranque de motores → taxi → despegue → ascenso → crucero FL350 → descenso → aproximación → aterrizaje → taxi-in → shutdown → gate cold&dark que se sostiene); útil para desarrollar y probar el motor sin MSFS ni horas de vuelo. Su `StopFlightSimulation` resetea el vuelo sintético.
- **Reloj escalable**: el mock recibe un `TimeProvider` opcional por constructor. En DEBUG la DI resuelve el singleton **`ScaledTimeProvider`** (`Domain/Services/ScaledTimeProvider.cs`, registrado en `AddApplication` y compartido por mocks y motor): `GetUtcNow` avanza a ×escala (1 = tiempo real), `SetScale` cambia la velocidad conservando el tiempo de escenario ya transcurrido y `Advance(TimeSpan)` salta el escenario hacia adelante. El knob vive en `IEngineOrchestrator` (`SimulatedTimeScale`, `SetSimulatedTimeScale`, `AdvanceSimulatedTime`) y en la UI en un bloque «RELOJ DE SIMULACIÓN (DEBUG)» del panel Realistic (1x/5x/15x/60x y saltos +10min/+30min/+1h). El motor Realistic y el timer automático esperan sus ticks sobre ese mismo reloj, así que **todo** (vuelo, cadencia, desgaste, tregua) acelera junto.
- Selección por DI según compilación: **DEBUG → mock; Release → real** (`DependencyInjection.cs`).

## Contratos

- `ISimConnectClient` (`Interfaces/ISimConnectClient.cs`): `IsConnectedAsync`, `GetAircraftStateAsync`, eventos `OnAircraftStateChanged`/`OnConnectionStateChanged`.
- `ISimFlightDataProvider` (`Application/Interfaces`): `IsConnectedAsync`, `GetAircraftRawData` → `SimulatorAircraftStateSnapshot`; implementado por **`SimulatorFlightDataProvider`** (health check cacheado 10 s con timeout de 2 s).

## Message pump y polling

- **Message pump**: `RunMessagePumpAsync` procesa `ReceiveMessage()` cada 10 ms en un hilo separado; sin esto SimConnect no dispara `OnRecvSimobjectDataBytype` (problema histórico, ver `docs/historial/2026-simconnect-fixes.md`).
- **Data request loop**: `RunDataRequestLoopAsync` pide datos cada 100 ms (10 Hz) con `RequestDataOnSimObjectType` sobre el objeto `USER`.
- El orden de las definiciones de datos debe coincidir exactamente con el struct `AircraftStateData` (`RegisterDataDefinitions`).
- `RegisterDataDefinitions` asigna a cada `AddToDataDefinition` un ID de datum único (`uint datumId` con `datumId++`, 0..17) en el orden del struct. Antes las 18 definiciones pasaban `0` explícito como último argumento (DatumID), lo que repetía el mismo ID: SimConnect devolvía `SIMCONNECT_EXCEPTION_DUPLICATE_ID` (29) al registrar y el mapeo de campos quedaba corrupto (longitud/altitudes/velocidades fuera de rango, combustión con enteros enormes, N1/throttle absurdos). Con IDs únicos la excepción queda eliminada por construcción.

## Thread safety

- `ReaderWriterLockSlim _stateReadWriteLock` protege `_lastKnownState`; `SemaphoreSlim _connectionLock` protege la conexión.
- `GetAircraftStateAsync` devuelve un clon bajo read lock.

## Datos

`AircraftStateData` (struct sequential): latitud, longitud, altitud de la aeronave (SimVar `PLANE ALTITUDE`, que se mapea a `AltitudeMSL`), altitud indicada, heading, ground velocity, `AIRSPEED TRUE` y `AIRSPEED INDICATED`, vertical speed, `SIM ON GROUND` (INT32), flaps, combustión motores (INT32), N1 y throttle (double), radio altitud. Se mapea a `SimConnectAircraftState` (con algunas conversiones a `int`, por ejemplo `AltitudeMSL`, `Heading`, `GroundSpeed`). El orden del struct debe coincidir exactamente con el de `AddToDataDefinition`.

`SimConnectClient` arranca con `_lastKnownState = new()` (`IsConnected=false`) y lo limpia a un estado nuevo (`IsConnected=false`, `_lastDataReceivedUtc=DateTime.MinValue`) bajo `_stateReadWriteLock` al iniciar una nueva conexión, al desconectar y al recibir el evento Quit de SimConnect, para que una muestra obsoleta de un vuelo anterior no sobreviva a la reconexión. `SimulatorFlightDataProvider` marca `SimulatorAircraftStateSnapshot.HasSimulatorData` desde `SimConnectAircraftState.IsConnected`. En el cliente real `IsConnected` ya no se pone en `true` incondicionalmente al recibir un paquete: `AircraftStateDataValidator.IsValid` valida rangos físicos razonables (posición, altitudes MSL/AGL, heading, velocidades, VS, flaps, N1, throttle), booleanos int estrictos 0/1 (`SIM ON GROUND`, combustión), rechaza NaN/Infinity/denormales y un frame completamente en cero (permite aeronave estacionada a altitud 0 si posición y otros campos identifican datos reales). Un paquete inválido conserva sus valores crudos en el objeto para diagnóstico pero con `IsConnected=false`. El callback `OnRecvSimobjectDataBytype` actualiza `_lastDataReceivedUtc` por cualquier paquete recibido (no reconecta en bucle cuando SimConnect responde sin avión cargado) y sobrescribe siempre el último estado, de modo que una muestra inválida nunca deja la telemetría anterior como actual. El mock (DEBUG) ya representa snapshots conectados.

## Reset de datos en ciclos de conexión

- **Nueva conexión** (`ConnectAsync`), **desconexión** (`Disconnect`) y **Quit de SimConnect** (`OnRecvQuit`): el último estado se reemplaza por `new()` con `IsConnected=false` y `_lastDataReceivedUtc` se reinicia a `DateTime.MinValue`, bajo el write lock (`ReaderWriterLockSlim`). Así `GetAircraftStateAsync` nunca devuelve telemetría vieja como si fuera actual mientras no llega una muestra nueva.

## Log de diagnóstico (SimConnect + estados calculados)

Activable con el flag `SimConnectDiagnostics:Enabled` en `appsettings.json` (default `false`). Pensado para comparar en desarrollo lo que SimConnect reporta contra lo que la app calcula (fase de vuelo, motores, etc.) durante pruebas en vivo.

- `SerilogExtensions.ApplyDefaultConfiguration` (`RealFenixFailures.Infrastructure/Logging/SerilogExtensions.cs`) separa el pipeline de Serilog en dos: el logger principal excluye (vía `Matching.FromSource`) todo evento cuya categoría sea `SimConnect.Diagnostics`, y, solo si el flag está en `true`, agrega un segundo `WriteTo.Logger` que incluye únicamente esa categoría y escribe en `%AppData%\RealFenixFailures\logs\simconnect-diagnostic-.log` (rolling diario, igual convención que el log principal `realfenix-.log`, ambos centralizados en `Infrastructure/AppPaths`). En ese caso aplica además un `MinimumLevel.Override` a `Debug` **solo** para la categoría `SimConnect.Diagnostics` sobre el logger raíz, de modo que los eventos `Debug` de esa categoría llegan al archivo de diagnóstico aun si el nivel mínimo general del logger raíz es más restrictivo (p. ej. `Information` en la configuración publicada); el resto de categorías conserva sus niveles generales. El flag se lee una sola vez al arrancar: cambiarlo exige reiniciar la aplicación.
- Dos emisores usan esa categoría fija (`ILoggerFactory.CreateLogger("SimConnect.Diagnostics")`, no `ILogger<T>`, para no acoplar Integrations/Application a Infrastructure):
  - `SimulatorFlightDataProvider.GetAircraftRawData` (Integrations.SimConnect) — loguea el dato crudo recibido de SimConnect en cada poll (posición, altitudes, velocidades —incluida la IAS—, motores, throttle). En esta representación de diagnóstico se redondea la latitud/longitud a 5 decimales y N1/throttle a 1 decimal; los valores internos (snapshot/estado) conservan su precisión original y no se alteran.
  - `FlightEventDetector.ProcessSnapshot` (Application) — loguea la fase de vuelo calculada (`ComplexFlightPhaseEnum`) y el estado derivado (motores corriendo, AGL/MSL, VS, GS/TAS/IAS).
- Si el flag está en `false`, los `LogDebug` igual se ejecutan (costo mínimo) pero no tienen sink de destino, así que no generan archivo ni ruido en el log principal.
- La categoría `"SimConnect.Diagnostics"` está duplicada como literal en Infrastructure, Integrations.SimConnect y Application (no hay dependencia compartida entre esas capas); si se renombra, actualizar los tres lugares.
- No reemplaza los logs operativos de `SimConnectClient`/`SimulatorFlightDataProvider` (estado de conexión, errores no previstos), que siguen yendo al log principal (ver sección siguiente).

## Logs operativos de conexión (niveles y transiciones)

Desde 2026-10-15 el estado de conexión se informa una sola vez por transición, no por intento:

- `SimulatorFlightDataProvider.IsConnectedAsync` emite una única línea `Information` corta por cambio de estado: **`SimConnect conectado`** al pasar a conectado y **`SimConnect desconectado`** al pasar a desconectado. El primer chequeo con resultado `false` no se loguea como desconexión (todavía no hubo conexión) y los resultados repetidos (incluida la ventana de caché de 10 s) no generan líneas. La inicialización del provider es `Debug` y la comparación de transición se hace contra el estado previo **antes** de actualizarlo.
- Timeout esperado del health check (2 s) o simulator fuera de servicio: no emiten `Warning` ni stacktrace por intento; se convierten en la notificación de transición. Solo las excepciones auténticamente no previstas loguean `Error` (`Error during health check`).
- `SimConnectClient.ConnectAsync` silencia el `COMException` esperado con HRESULT `0x80004005` al intentar conectar (el simulador no está disponible; sin mensaje repetido ni stacktrace — la transición la reporta el provider). `Successfully connected to SimConnect`, `Disconnected from SimConnect`, `SimConnect Open` y `Timeout: Reconnecting SimConnect` son `Debug`, para no duplicar la transición INFO única del provider.
- Se conservan el `Error` del message pump (`Exception in SimConnect Message Pump`) y del procesamiento de datos (`Error processing SimConnect data`), el `Warning` de `OnRecvException` (con `dwException`, `dwSendID`, `dwIndex`) y el `Debug` del data request loop (`Error requesting data`). No esconder `OnRecvException`: registraciones inusuales tienen que seguir visibles.
- Los overrides de appsettings (`Microsoft: Warning`, `System: Warning`, `System.Net.Http.HttpClient: Warning`) se aplican en el logger raíz (ver `SerilogExtensions`), de modo que el chatter INFO del framework HTTP queda suprimido en el archivo principal.

## Obtención de la DLL del SDK

El wrapper managed `Microsoft.FlightSimulator.SimConnect.dll` está **versionado en `libs\`** del proyecto de integración (fuente determinista) y la referencia lo resuelve con fallback a la instalación local del SDK (`MSFS 2024 SDK\SimConnect SDK\lib\managed\...`; la propiedad `SimConnectManagedDll` del csproj elige libs\ si existe, si no el SDK). Instalación del SDK: MSFS → Developer Mode → Tools → SDK Installer; su carpeta la elige quien lo instala, por eso no se depende de ella en runtime ni para distribuir.

Ambas DLLs viajan como **archivos sueltos** a la salida y a la carpeta de publish (un `None Include` con `Link`, `CopyToOutputDirectory`/`CopyToPublishDirectory=PreserveNewest` y `ExcludeFromSingleFile=true` para el wrapper managed): con `PublishSingleFile=true` quedan al lado del exe único en vez de empaquetarse dentro de él. La nativa `SimConnect.dll` (nativa, sin manager, versionada en la raíz del proyecto) ya se copiaba con `CopyToOutputDirectory=PreserveNewest`; no moverlas ni eliminarlas. En `RealFenixFailures.UI.csproj`, el target `QuitarSimConnectWrapperDeCopyLocal` quita el wrapper de `ReferenceCopyLocalPaths` (RAR lo detecta además como dependencia del ensamblado de la integración): esa segunda vía duplicaría el archivo en el publish (NETSDK1152) y con single-file lo empaquetaría dentro del exe.

## Advertencias verificadas

- **MSB3270 (AMD64 vs MSIL)**: **resuelto** al fijar `PlatformTarget=x64` en el csproj de la integración (y en UI/TestUnits); los builds actuales no lo reportan (0 advertencias). Al tocar la integración, no volver a MSIL ni desalinear la arquitectura objetivo.
- **DuplicateID 29 en `AddToDataDefinition`**: **corregido y verificado manualmente en producción**. La firma del SDK toma `DatumID` opcional (default `SIMCONNECT_UNUSED`); el cliente pasaba `0` como último argumento en las 18 definiciones y SimConnect devolvía `SIMCONNECT_EXCEPTION_DUPLICATE_ID` (29) al registrar, con el mapeo de campos corrupto (log de diagnóstico con `Lon=6.8e38`, `AltAGL=-2147483648`, `Flaps=2147483647`, combustión con enteros enormes, `IndAlt≈1e-313`, N1/throttle absurdos). Con IDs únicos (0..17) la excepción queda eliminada por construcción. El usuario probó la publicación en producción y reporta que la telemetría corregida funciona; la revisión directa de los logs posteriores muestra datos coherentes.

- **Reconexión**: `IsConnectedAsync` detecta timeout de datos (> 10 s) y **dispara un intento** de reconexión, pero esto **no está verificado manualmente**; no afirmar reconexión automática garantizada sin prueba real.

## Pruebas manuales pendientes

- Publicación en producción con el fix de datum IDs (DuplicateID 29): el usuario comprobó telemetría coherente (posición, altitudes, velocidades y motores) y reportó que funciona correctamente.
- Recepción periódica de datos ("Aircraft state updated" en logs).
- Transiciones de fase reales (Parked → Taxi → Takeoff → Climb, etc.).
- Reconexión tras cerrar/abrir MSFS.
- Performance (CPU/memoria) en sesión larga.

Historial del problema de conexión (message pump, tipos, thread safety): [docs/historial/2026-simconnect-fixes.md](../historial/2026-simconnect-fixes.md).
