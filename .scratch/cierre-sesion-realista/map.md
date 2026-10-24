# Mapa wayfinder — Cierre de sesión Realista completo

Label: wayfinder:map

## Destination

El flujo de cierre de sesión Realista completo según `docs/realistic-mode/diseno-objetivo.md` §6/§9/§13 — consolidación de fallas activas en BD (`NotResolved`/`SessionEnded`), modal de resultados automático al terminar, y recuperación de sesiones sin finalizar al reabrir («Finalizar y ver resultados») — implementado y verificado con build + suite de tests. Los cambios de front que pida el usuario entran como tickets propios del mismo mapa.

## Notes

- Dominio: WPF .NET 10, MVVM, capas Domain/Application/Infrastructure/Integrations/UI (ver `docs/arquitectura.md`).
- Flujo de cierre actual verificado en código (2026-10-23): `RealisticModeViewModel.ActivateOrStopAsync` (línea ~835) → `EngineOrchestrator.StopCurrentModeAsync` (línea ~399) → `RealisticSessionEngine.StopSession` (línea ~156: evento `SESSION_STOPPED`, `FinishedAt` vía `FlightSessionService.StopAsync`, acumulación de desgaste `AccumulateWearAsync`, descarte de preparadas) → reset global de fallas en Fenix (`ResetAllFailuresAsync`). Las fallas activas quedan en BD sin consolidar (`Sent`/`Active`).
- Contratos listos en Application, sin consumo en UI: `IFlightSessionService.GetResultAsync` / `GetUnfinishedSessionsAsync` / `FinalizeSessionAsync` (con tests en `FlightSessionServiceTests`). Enums sin uso relevante: `FailureLifecycleState.NotResolved`, `ResolutionSource.SessionEnded`.
- Skills a consultar en cada ticket de decisión: `/grilling` + `/domain-modeling`; para el modal, `/prototype`. Documentos fuente: `docs/realistic-mode/diseno-objetivo.md` (§6, §8, §9, §13, §14) y `docs/realistic-mode/estado-y-diseno.md` (estado vigente).
- Preferencias del usuario: **sin sub-agentes** (todo lo resuelve el agente principal); trabajo multi-agente en la misma rama (`dev`) — commits selectivos, no commitear salvo pedido explícito; al implementar, actualizar doc de estado + `docs/CHANGELOG.md` en el mismo trabajo.
- Verificación alcanzable desde acá: build Debug/Release + suite xUnit. La sesión real MSFS/Fenix se prueba en la PC de testing (`\\PC-MATHIAS\RealFenixFailures`); no declarar modos funcionales sin esa prueba.

## Decisions so far

- [Destino del mapa](../../docs/realistic-mode/diseno-objetivo.md) — confirmado por el usuario (2026-10-23): flujo de cierre completo (consolidación + modal de resultados + recuperación de sesiones abiertas) y los cambios de front que pida como tickets propios.
- [Cambios de front deseados](issues/04-cambios-de-front-deseados.md) — dos cambios nombrados: horas en UTC en toda la app («incluso los logs») y reloj UTC en la status bar; graduados a los tickets 05 y 06.
- [Reloj UTC en la barra de estado](issues/06-reloj-utc-status-bar.md) — resuelto: al lado de la píldora «FASE:» en la status bar de la MainWindow, formato `HH:MM:SS UTC`, actualización cada segundo, hora UTC real de pared.
- [Horas en UTC en toda la app](issues/05-horas-en-utc.md) — resuelto: todo en UTC (app + `.log` en disco); formato `HH:mm:ss UTC` / `dd/MM/yyyy HH:mm:ss UTC`; **las horas del OFP ya vienen en UTC de SimBrief: renderizarlas sin reconversión** (fin de la dualidad local/tooltip).
- [Consolidación de fallas al cerrar](issues/01-consolidacion-fallas-al-cerrar.md) — resuelto: único camino de cierre en `FinalizeSessionAsync` (FinishedAt + consolidación + resultado); no resueltas → `NotResolved`/`SessionEnded` con hora de cierre; desarme por falla best-effort antes del reset global; resultado calculado después de consolidar.
- [Recuperación de sesiones sin finalizar](issues/03-recuperacion-sesiones-abiertas.md) — resuelto: diálogo al arrancar; acciones «Finalizar y ver resultados» / «Descartar» (queda como sesión descartada en historial, **sin desgaste**) / «Dejar para después»; desgaste de huérfanas con la **ETA del OFP** como fin del vuelo; limpieza Fenix best-effort.
- [Modal de resultados](issues/02-modal-de-resultados.md) — decidido: primero **mock HTML publicado** para iterar, después WPF; contenido = resumen + **todos los eventos de vuelo detallados** + fallas lanzadas + **delta de desgaste por sistema**; re-abrible desde el historial; aparece al detener y al finalizar huérfana.
- [Feed de fallas integrado al pensamiento del motor](issues/07-feed-integrado-al-pensamiento.md) — decisión nueva del usuario: el feed de fallas en vivo se integra al bloque del ojito (oculto por defecto; detalles de motor en vivo a futuro, fallas y estados de hoy tal cual). **Implementado (2026-10-23)**: bloque unificado, aviso sin Fenix y descarte de preparadas siempre visibles.
- [Modal de resultados](issues/02-modal-de-resultados.md) — **implementado (2026-10-23)** sobre el mock aprobado: `SessionResultWindow` con resumen, cronología completa, fallas consolidadas y delta de desgaste por sistema (`SessionSystemWearDelta` persistido); abierto tras «DETENER», al finalizar huérfana y re-abrible desde el historial.

## Not yet specified

- Al **descartar** una sesión huérfana: consolidación exacta de sus fallas y marcador «descartada» (asunción registrada en el ticket 03). — **Resuelto al implementar: mismas consolidaciones `NotResolved`/`SessionEnded`, sin desgaste, badge «Descartada» en historial.**
- Detalles en vivo del pensamiento del motor (ticket 07): implementación futura; hoy solo se conserva el feed de fallas dentro del bloque. — **Permanece: el lugar queda reservado dentro del bloque expandido.**
- Contenido visual fino del modal: se afila iterando sobre el mock HTML (ticket 02). — **Cerrado con el mock aprobado; ajustes finos serán tarea nueva si el usuario los pide tras probar.**
- Mapa completo: los 6 tickets de decisión + 07 están `resolved` y implementados con build Debug/Release y 193/193 tests. Falta la verificación real en la PC de testing (fuera de este mapa).

## Out of scope

- Pausa manual de fallas («Pausar/Reactivar fallas», §14): hoy botón deshabilitado con explicación; es otro esfuerzo con su propio diseño.
- Arranque sin Fenix operativo (§15, modo preparación): ya parcialmente implementado; no se toca acá.
- Perfiles completos con topes/cadencias objetivos (§5), pensamiento real del motor, temporales por tiempo (§9 aparte de lo que ya exista).
- Verificación en sesión real MSFS/Fenix (se hace en la PC de testing, fuera de este mapa).
