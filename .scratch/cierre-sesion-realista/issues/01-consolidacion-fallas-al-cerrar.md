# Consolidación de fallas al cerrar

Type: grilling
Status: resolved

## Question

Al terminar la sesión Realista, ¿qué estado final reciben en BD las fallas que quedaron en curso y dónde vive esa consolidación?

Contexto verificado: hoy `RealisticSessionEngine.StopSession` (Application/Session/RealisticSessionEngine.cs:156) registra `SESSION_STOPPED`, pone `FinishedAt`, acumula desgaste y descarta preparadas; después el orquestador hace reset global de fallas en Fenix (`EngineOrchestrator.StopCurrentModeAsync` → `ResetAllFailuresAsync`). Las `TriggeredFailure` en `Sent`/`Active`/`Preparing`/`Queued` quedan sin resolver: los valores `FailureLifecycleState.NotResolved` y `ResolutionSource.SessionEnded` existen en los enums pero nadie los asigna. El diseño (§9) dice «No resuelta (sesión terminó)».

Sub-decisiones a resolver:
1. ¿Toda falla no resuelta pasa a `NotResolved` con `ResolutionSource=SessionEnded` y `ResolvedAtUtc` = momento de cierre? ¿Las `Sent`/`Preparing`/`Queued` también, o las rechazadas/no-eventos quedan como están (`Rejected`)?
2. ¿La consolidación vive en `StopSession`, o se centraliza en `FlightSessionService.FinalizeSessionAsync` para que la reutilice la finalización de sesiones huérfanas (ticket «Recuperación de sesiones sin finalizar»)? Nota: `FinalizeSessionAsync` ya existe y devuelve el `SessionResultDto`.
3. ¿El desarme en Fenix queda como está (reset global posterior) o el cierre debe desarmar falla por falla antes de consolidar? Considerar la sesión real: tras cerrar la app, ¿queda alguna falla armada en Fenix?
4. ¿El resultado devuelto al cierre (`SessionResultDto`) debe calcularse después de consolidar para que «activas al final» sea 0?

Recomendación inicial del agente: consolidar todo lo no resuelto → `NotResolved` + `SessionEnded` dentro de `FinalizeSessionAsync` (único camino de cierre, reutilizable por la recuperación), con desarme por falla en Fenix antes de consolidar (best-effort) y el reset global del orquestador como red de seguridad.

## Answer

Decidido por el usuario (2026-10-23) — confirmó las cuatro recomendaciones:

1. **Estados finales**: toda falla no resuelta al cerrar (`Preparing`/`Queued`/`Sent`/`Active`) pasa a `NotResolved` con `ResolutionSource=SessionEnded` y `ResolvedAtUtc` = momento de cierre. Las `Rejected` quedan como están («No resuelta (sesión terminó)» del diseño §9).
2. **Dónde vive**: único camino de cierre centralizado en `FlightSessionService.FinalizeSessionAsync` — pone `FinishedAt`, consolida fallas y calcula el resultado. El stop del botón «DETENER» llama a ese mismo camino; la recuperación de huérfanas (ticket 03) lo reutilizará.
3. **Desarme en Fenix**: antes de consolidar, desarme falla por falla (best-effort, log de las que fallen); el reset global actual (`ResetAllFailuresAsync` en el orquestador) queda como red de seguridad.
4. **Resultado**: el `SessionResultDto` se calcula después de consolidar → `ActiveAtEnd` queda 0 y las no resueltas cuentan como tales en el resumen.
