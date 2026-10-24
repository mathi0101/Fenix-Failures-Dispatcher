# Ticket 05 — Fallas temporales y auto-resolución

Type: grilling
Status: resolved
Blocked by: 04 (resuelto)

## Question

Reglas de auto-resolución de fallas temporales: por tiempo (duración con componente aleatorio según la falla) y/o por condición (evento/estado que las resuelve). Cómo se indica en el catálogo qué fallas son temporales y cuáles nunca (daño de motor no se arregla solo; background sí). Proporción y uso por perfil de riesgo. Interacción con el desgaste y con el historial (quién la desactivó: engine).

## Answer

- **Declaración en el catálogo (Q50)**: la propiedad es **`PuedeSerTemporal`** y está **por falla individual dentro del preset** (no a nivel preset completo) y es **probabilística**: una misma falla puede a veces auto-repararse y a veces no. El engine al armar decide (según perfil y probabilidad declarada) si esta instancia es temporal. Las que `PuedeSerTemporal=false` (daño de motor, críticas) el engine nunca las auto-resuelve.
- **Regla de auto-resolución (Q51)**: **solo por tiempo** — la falla dura un tiempo **configurable/aleatorio en cada armado** (dentro de parámetros del perfil). Sin condiciones de evento.
- **Ejecución (Q52)**: al vencer, el engine envía la desactivación a Fenix, espera confirmación, y recién entonces la persiste como `Auto-resuelta por engine`. Si Fenix la reporta ya inactiva antes, se registra igual atribuida al engine. Misma disciplina de Q19 (verificación).
- **Historial (Q53)**: tres estados inequívocos: `Auto-resuelta (engine)` / `Solucionada por piloto` / `No resuelta (sesión terminó)`, cada una con su ícono. Distingue claramente quién cerró cada falla.
- **Perfil (Q54)**: la duración la declara/elige la falla en cada armado, y el **perfil escala** la duración: Bajo ×0.5, Moderado ×1, Alto ×1.5, 7700 ×2.5. Temporales proporcionalmente: Bajo → mayoría; Moderado → mitad; Alto → solo algunas; 7700 → casi ninguna y mucho más largas.
- **Respiro tras crítica (Q55)**: el respiro (Q40) aplica **solo a críticas**, sean temporales o no. Minor/Major nunca generan respiro.
