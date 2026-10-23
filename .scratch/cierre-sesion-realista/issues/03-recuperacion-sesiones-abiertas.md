# Recuperación de sesiones sin finalizar

Type: grilling
Status: resolved
Blocked by: 01 (resuelto)

## Question

¿Cómo se detecta, se muestra y se resuelve una sesión Realista que quedó abierta porque la app se cerró con sesión viva (§6/§13)?

## Answer (usuario, 2026-10-23)

1. **Cuándo se ofrece**: diálogo al arrancar la app listando las sesiones abiertas; si se ignora, quedan visibles como «sin finalizar» en el paso 2.
2. **Acciones** (tres):
   - «**Finalizar y ver resultados**»: consolida (camino único `FinalizeSessionAsync`, ticket 01) + muestra el modal de resultados.
   - «**Descartar**»: queda en el historial como **sesión descartada** y **no aplica desgaste de sistemas**.
   - «**Dejar para después**»: no toca nada; la sesión sigue abierta y visible.
3. **Desgaste de la huérfana**: se calcula usando la **hora estimada de llegada (ETA) del OFP** como fin del vuelo (decisión del usuario, sustituye la recomendación de no acumular). Alternativa del usuario ante la duda: descartar la sesión. *Asunción a validar al implementar: si la ETA ya pasó, se usa la ETA igual (sin clampear a «ahora»).*
4. **Limpieza en Fenix**: best-effort al finalizar (si hay conexión, reset de fallas; si no, se omite con log).
5. Pasa por el mismo camino del ticket 01 y muestra el mismo modal de resultados (ticket 02).

## Pendiente de definición al implementar

- Qué consolidación reciben las fallas al **descartar** (asunción: mismo cierre `NotResolved`/`SessionEnded`, pero **sin** desgaste; la marca «descartada» necesita un indicador en `FlightSession`, p. ej. evento de sesión `SESSION_DISCARDED`).
- Marcador de «descartada» visible en el historial del paso 2.
