# Ticket 09 — Historial y pantalla de resultados

Type: grilling
Status: resolved
Blocked by: 04, 06, 08 (resueltos)

## Question

Qué se ve al terminar una sesión (y en el historial persistido): timeline minuto a minuto de eventos y fallas, detalle por falla (qué, cuándo, fase, presets, acción del usuario: solucionada/ignorada/auto-resuelta, quién la desactivó), desgaste acumulado por sistema, duración, perfil de riesgo, OFP usado. Agrupación: vuelo físico → sus sesiones. Pantallas/ubicación en la app (modo → historial) y qué muestra la app al reabrir si quedó una sesión sin finalizar.

## Answer

- **Agrupación (Q72)**: **cada sesión es un vuelo e historial por separado** — NO se agrupan bajo un "vuelo físico". Dos sesiones pueden referenciar el mismo OFP sin problema, pero a la sesión no le importa eso. *(Invalida la decisión de Q35 en el ticket 03: el vuelo físico ya no agrupa sesiones; el historial lista sesiones independientes.)*
- **Cabecera de sesión (Q73)**: hora de inicio, duración, origen→destino (visual, del OFP), conteo de fallas por severidad (Minor/Major/Critical), perfil de riesgo usado.
- **Detalle de falla (Q74)**: preset (qué/sistema/severidad), fase de armado y resolución, momentos, duración, quién la terminó, contexto (altitud/IAS/motores). El snapshot del ticket 04.
- **Desgaste en resultados (Q75)**: en la cabecera del **vuelo** (la sesión): % desgaste por sistema (`FenixFailureSystems`, 19 ATAs de `failures.json`) nuevo acumulado + total de la aeronave hasta ahí + aviso de mantenimiento al pasar umbral.
- **Tiempo de vuelo y desgaste (Q76)**: sí — el desgaste de cada sesión es proporcional al tiempo de la sesión, y **solo corre cuando el motor de fallas está corriendo** (no cuando está pausado).
- **Sesión sin finalizar al reabrir (Q77)**: al reabrir la app, tarjeta "Tienes una sesión sin finalizar (aeronave, perfil, iniciada a Z)" → botón "Finalizar y ver resultados" (marca fin, persiste historial, muestra resultados + desgaste, y limpia fallas activas en Fenix — Q37).
