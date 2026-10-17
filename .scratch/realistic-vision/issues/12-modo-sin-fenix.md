# Ticket 12 — Iniciar el modo sin Fenix operativo (motor en modo preparación)

Type: grilling
Status: resolved
Blocked by: 03, 04 (resueltos)

## Question

Nuevo requisito (Q36): iniciar el modo Realistic con SimConnect conectado **sin exigir Fenix operativo**. Mientras Fenix no responde, la app avisa y el botón de pausa indica que las fallas no se pueden activar; el usuario inicia el modo mientras el simulador carga el avión. FALTAN definir: ¿el motor empieza a "pensar" (evaluar candidatos, sembrar fallas del ticket 11) y las retiene en espera hasta que Fenix responda (200)? ¿Qué estados/UX hay "Fenix no disponible"? ¿Qué pasa si Fenix nunca responde? ¿Cómo se reconcilian las fallas preparadas al aparecer Fenix?

## Answer

- **Comportamiento (Q84)**: el motor corre su **pipeline normal** (observa, contextualiza, elige candidatos) y las fallas decididas entran a **`En preparación`** (ticket 04) — se mantienen seleccionadas/en cola sin enviar a ningún lado. No cuentan para cadencia hasta armarlas. Cuando Fenix responde (200), las envía.
- **Espera sin límite (Q85)**: el motor espera **mientras la sesión viva**, pero **sin acumular sin tope**: si la cola de preparación llega a N fallas (configurable, p. ej. 5), deja de elegir candidatas hasta que Fenix aparezca o la cola se descongele.
- **Envío al aparecer (Q86)**: **no todas juntas** — el motor envía en **orden de prioridad** y **respetando el tope de simultáneas del perfil (Q22) y cooldowns**: primero **fallas iniciales de tierra** (si aplica, ticket 11), luego el resto en el orden decidido; lo que no tiene cupo queda en `En preparación`.
- **Botón de pausa con Fenix off (Q87)**: se muestra **deshabilitado con aviso** ("Fenix no disponible: las fallas no se pueden activar") y ofrece una **acción secundaria "Descartar fallas preparadas"** (limpia la cola; el piloto la reinicia).
- **Fallas iniciales y Fenix tardío (Q88)**: si las fallas iniciales se prepararon y Fenix apareció cuando el avión ya está en taxi/despegue, se **descartan definitivamente** (log técnico); **sin reintento** — no se rearman como fallas de vuelo. El motor sigue con la narrativa normal.
- **Cadencia/historial (Q89)**: las fallas enviadas desde preparación son **idénticas en el historial** (una falla es una falla); el "preparada con Fenix no disponible" solo queda en el **log técnico** (debug).
