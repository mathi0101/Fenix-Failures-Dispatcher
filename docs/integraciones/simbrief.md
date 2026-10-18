# SimBrief — lectura del OFP

Estado del parser (2026-10-18). `SimBriefOfpReader` transforma el XML en `FlightPlan`; la validación de aeronave, campos mínimos y ventana de operación pertenece a `SimbriefOfpService` (Application).

## Referencia para futuros desarrollos

El [OFP SBSP–SUMU suministrado el 2026-10-18](ejemplos/simbrief-ofp-SBSP-SUMU-2026-10-18.txt) se conserva completo y sin modificaciones, desde `<OFP>` hasta `</OFP>`, en un archivo independiente. Es una captura estática de referencia, no un OFP vigente ni un recurso de ejecución.

**Advertencia de formato:** el texto suministrado contiene HTML con etiquetas `<br>` sin cerrar dentro de `notam_html`, por lo que no es XML bien formado y `XDocument.Parse` lo rechaza. La extensión `.txt` es intencional: no se corrigió ni escapó la captura. Para pruebas automatizadas del horario se usa su bloque `times` exacto (mismos elementos y valores, con indentación reducida), envuelto en un OFP mínimo válido. No se agregó tolerancia a XML mal formado ni se verificó una descarga HTTP real.

## Fechas y duraciones

- Los instantes numéricos del bloque `times` son **segundos Unix**, no milisegundos ni horas locales. Se convierten con `DateTimeOffset.FromUnixTimeSeconds(...).UtcDateTime`. Los offsets `orig_timezone`/`dest_timezone` no se aplican a esos instantes.
- `OffBlockUtc` y `ValidFromUtc`: `times/sched_out` (salida del gate). Si el elemento falta, se conserva compatibilidad con `sched_off` y luego `orig`.
- `EstimatedArrivalUtc` y `ValidUntilUtc`: `times/est_on` (aterrizaje estimado); si falta, `sched_on` y luego `dest`.
- `out` = salida del gate; `off` = despegue; `on` = aterrizaje; `in` = llegada al gate. No son intercambiables. El fallback a `sched_off` solo cubre respuestas sin `sched_out`.
- La selección de fallback anterior es por **elemento ausente**, no por valor vacío o inválido: no se oculta un timestamp mal formado usando otro horario.
- `est_time_enroute`, `sched_block`, `taxi_out`, etc. son **duraciones en segundos**, no timestamps. Solo `est_time_enroute` se mapea a `EstimatedDurationMinutes`, con división entera por 60. Si no aporta una duración positiva, se conserva el cálculo existente por diferencia entre llegada y off-block (que incluye taxi de salida).
- Se conserva el soporte de fechas textuales UTC y fechas con offset explícito, normalizadas a UTC. Valores vacíos, inválidos o segundos Unix fuera del rango de `DateTimeOffset` devuelven `default` sin excepción; se mantienen los fallbacks existentes del lector (`UtcNow` / llegada +30 minutos como mínimo, con límites de validez en `default`). No se modifica la validación de Application.
- El ICAO se lee de `aircraft/icaocode`; se mantiene `general/aircraft_icao` como compatibilidad con el mock sintético anterior. `RawData` conserva el XML de entrada.

### Resultado del bloque de ejemplo

| Campo | Entrada | Resultado UTC |
|---|---|---|
| `OffBlockUtc` / `ValidFromUtc` | `sched_out = 1792335900` | `2026-10-18T15:05:00Z` |
| `EstimatedArrivalUtc` / `ValidUntilUtc` | `est_on = 1792344908` | `2026-10-18T17:35:08Z` |
| `EstimatedDurationMinutes` | `est_time_enroute = 7808` | 130 minutos (truncado) |

## Verificación

`RealFenixFailures.TestUnits/Integrations/SimBriefOfpReaderTests.cs` cubre el bloque real de horarios, duración, aterrizaje programado alternativo, fechas textuales del mock, epoch cero/negativo, espacios, offsets explícitos y entradas inválidas/fuera de rango. Los resultados de ejecución y build se registran en [CHANGELOG](../CHANGELOG.md). Integración HTTP en vivo y sesión real MSFS/Fenix: **no verificadas**.
