# Ticket 04 — Ciclo de vida de una falla

Type: grilling
Status: resolved
Blocked by:

## Question

Flujo de ejecución de una falla: armar en Fenix → confirmación (2-3 ticks de verificación) → persistencia atómica (falla + evento) → notificación/UI. Estados posibles de una falla: enviada a Fenix, activa confirmada, rechazada (no-evento), auto-resuelta (temporal), desactivada por pausa, solucionada por piloto. Qué acción del usuario/UI en cada estado. Qué se persiste de cada falla (snapshot del preset/condición). Relación con cooldown y máx. simultáneas.

## Answer

- **Flujo y estados (Q38)**: `Seleccionada (motor pensando)` → `En cola` → `En preparación (Fenix no disponible)` → `Enviada a Fenix` → `Activa confirmada` → (resuelta: `Auto-resuelta por engine` / `Solucionada por piloto` / `Desactivada por pausa`). Rechazo → `Rechazada (no-evento)`. Confirmado con la agregación de `En preparación` (candidata elegida con Fenix no disponible; se envía cuando Fenix responde 200 — conecta con el ticket 12) y `En cola`.
- **Persistencia (Q39)**: snapshot completo al volverse activa: preset (qué falla, sistema, severidad), perfil de riesgo de la sesión, momento (timestamp + fase + horas del avión/OFP), condiciones en que se armó (altitud/IAS/motores), y después la resolución (quién la terminó).
- **Cooldown / respiro tras críticas (Q40)**: no hay cooldown real (Q23); pero sí **enfriamiento de seguridad tras fallas críticas** (configurable; p. ej. 10-15 min en Alto, 5 min en 7700) para que el piloto resuelva y por narrativa. Minor/Major sin cooldown (depende de la narrativa).
- **Rechazo por Fenix (Q41)**: 2-3 ticks de gracia mostrando "enviada"; al descartarse, se quita de la UI **sin persistir como evento de falla** (solo queda en el log técnico como intento). **Caso especial**: si la falla es de las **"fallas iniciales"** (solo aplicables a la primera fase, antes de taxi) y Fenix no conectó, se espera **indefinidamente** a que Fenix responda mientras el avión siga en tierra en la misma fase; si el sim detecta taxi/despegue (la fase ya no aplica), esas fallas se **descartan** y el engine sigue planeando fallas para las fases nuevas. Nunca se inyecta una falla pensada para una fase anterior. (Caso muy raro.)
- **Umbrales físicos (Q42)**: avión en tierra con motores apagados (salvo falla de tierra), avión no configurado (spoilers, puertas), sim pausado (no arma), reglas del preset (altitud, IAS, condiciones) y `FlightPhase` como red de seguridad principal. **Nunca** armar una falla absurda (p. ej. incendio de motor en V1). El engine **solo** arma si el estado lo hace plausible. **"Fallas iniciales"**: conjunto que se carga idealmente **antes** de que el usuario conecte la GPU o prenda la APU — recibe el avión con fallas prontas, debe cambiar su método de encendido y decidir entre mantenimiento o salir volando con ellas.
- **Razonamiento en la persistencia (Q43)**: el razonamiento del engine **no** va al historial (no se ve en resultados); solo al **log técnico en modo debug** (descartable). El snapshot en el historial queda limpio.
