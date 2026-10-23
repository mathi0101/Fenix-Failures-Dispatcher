# Horas en UTC en toda la app

Type: grilling
Status: resolved

## Question

¿Qué alcance exacto tiene el pase de todas las fechas y horas mostradas a hora Zulu/UTC — «incluso los logs» — y qué formato llevan?

## Answer

Decidido por el usuario (2026-10-23): **todo en UTC — lo que se muestra en la app y todo lo que sale de ella, incluidos los archivos `.log` en disco** (`realfenix-<AAAAMMDD>.log` y `simconnect-diagnostic-<AAAAMMDD>.log`; los nombres de archivo quedan como están).

⚠️ **Cuidado del usuario**: **todo lo que viene del OFP ya está en hora UTC** (SimBrief). Al mostrarlo, renderizar tal cual — sin `.ToLocalTime()` ni `.ToUniversalTime()` (los valores pueden llegar con `Kind=Unspecified`; reconvertir los corrompería). Hoy la UI muestra «Salida programada» en hora local con UTC en tooltip: eso desaparece y pasa a UTC directo.

**Formatos** (asunción aceptada, no objetada):
- Solo-hora: `HH:mm:ss UTC` (el reloj de la status bar usa `HH:MM:SS UTC`, ticket 06).
- Fecha+hora: `dd/MM/yyyy HH:mm:ss UTC`.
- Filas del OFP («Salida programada», «Llegada estimada», «Duración estimada»): UTC directo, sin hora local ni tooltip dual.

Superficie a tocar (verificada 2026-10-23):
- Feed de fallas (`LiveFailureEntryViewModel.TimestampText`, hoy `HH:mm:ss` local) y estados.
- Historial del paso 2 (`FlightSessionItemViewModel`: inicio/fin, duración, `TriggeredAt`).
- Bloque OFP del paso 3 (§8.1) y tooltips (`OfpValidationToolTip`, etc.) — **OFP: sin reconversión**.
- Pensamiento del motor (líneas `a T+hh:mm:ss`, tiempo de escenario en DEBUG; se muestran igual, sin distinguir).
- `LogPanel` (log en tiempo real de la UI).
- Logs técnicos en disco: timestamps de línea en UTC.
