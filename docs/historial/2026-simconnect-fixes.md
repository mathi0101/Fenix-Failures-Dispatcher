# 2026 — Correcciones históricas de la conexión SimConnect (MSFS 2024)

> **Documento histórico.** Fotografía del estado de la integración SimConnect en 2026, tomada de la resolución de un problema de conexión (se detectaba la conexión pero no llegaban datos). No es fuente del estado actual: el flujo vigente se describe en [docs/arquitectura.md](../arquitectura.md) y [docs/integraciones/simconnect.md](../integraciones/simconnect.md). Las afirmaciones de este documento no se deben reutilizar sin re-verificar contra el código actual.

## Problema identificado (histórico)

La conexión a SimConnect para MSFS2024 detectaba la conexión pero no obtenía datos del simulador. Causas raíz registradas:

1. **Sin message pump**: SimConnect requiere un loop que procese mensajes periódicamente para disparar eventos como `OnRecvSimobjectDataBytype`.
2. **Acceso no thread-safe**: el estado de la aeronave se accedía desde múltiples hilos sin sincronización.
3. **Datos no actualizados**: `GetAircraftStateAsync` retornaba datos sin esperar la respuesta del simulador.
4. **Tipos de datos incorrectos**: campos decimales declarados como enteros en lugar de `FLOAT64`.
5. **Inconsistencia en el flujo de datos** hacia los servicios superiores.

## Correcciones aplicadas en su momento

### SimConnectClient.cs — reescrito

- Message pump asincrónico (`ReceiveMessage()` cada 10 ms en hilo separado).
- Sincronización: `ReaderWriterLockSlim` para el estado y `SemaphoreSlim` para la conexión.
- Data request loop: solicitud de datos cada 100 ms (10 Hz).
- Detección de ausencia de datos: `_lastDataReceivedUtc` con timeout de 5–10 s.
- Manejo de excepciones específico en ambos loops.

### AircraftStateData.cs — tipos

Se pasaron varios campos a `double` para coincidir con lo que SimConnect retorna (latitud, longitud, altitudes, heading, velocidades, N1, throttle, flaps, radio altura).

**Nota histórica**: no todos los campos actuales son `double`: `SIM ON GROUND` y la combustión de motores se definen como `INT32`, y en el estado mapeado (`SimConnectAircraftState`) campos como `AltitudeMSL`, `Heading`, `GroundSpeed`, `VerticalSpeed` o `FlapsHandleIndex` son `int`.

### SimulatorFlightDataProvider.cs — mejorado

- Logging adicional (cache hits, cambios de estado).
- Manejo de timeouts en health checks y de `OperationCanceledException`.
- Logging de cambios del estado de conexión.

## Flujo de datos en ese momento

El diagrama de flujo original apuntaba a `EngineOrchestrator → SimulatorConnectionService → Fenix API`. Esa cadena quedó reemplazada por el flujo vigente que se documenta en [docs/arquitectura.md](../arquitectura.md) (manager/engine/detector del modo Realistic incluidos).

## Beneficios registrados (históricos)

- Recepción de datos en tiempo real (message pump).
- Lecturas thread-safe.
- Mejores logs para debugging.
- Flujo de datos consistente hacia los servicios.

## Reconexión

El documento original afirmaba **reconexión automática** ante 10 s sin datos. Eso **no está verificado**: hoy existe un intento de reconexión por timeout en `SimConnectClient.IsConnectedAsync`, pero no está probado manualmente (ver [docs/integraciones/simconnect.md](../integraciones/simconnect.md)).

## Warnings

El warning **MSB3270** (arquitectura AMD64 vs MSIL) fue registrado como "no afecta runtime". Sigue siendo el caso, pero la advertencia puede variar entre builds y versiones del SDK: no citarla como condición estable.

## Pruebas pendientes (siguen pendientes hoy)

- Conexión real con MSFS 2024 y recepción periódica de datos.
- Transiciones de fase reales (Parked → Taxi → Takeoff → Climb, etc.).
- Reconexión tras cerrar/abrir MSFS.
- Performance en sesión larga.
