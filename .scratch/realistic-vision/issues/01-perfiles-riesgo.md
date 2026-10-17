# Ticket 01 — Perfiles de riesgo como comportamiento completo

Type: grilling
Status: resolved
Blocked by:

## Question

Definir cada perfil de riesgo (Bajo, Moderado, Alto, 7700) como un perfil de comportamiento completo del engine, no como un multiplicador: severidades permitidas por perfil, cadencia de fallas (esperadas por hora o por vuelo), concurrencia simultánea, cooldown entre activaciones, uso y proporción de fallas temporales, distribución temporal por fases, relación con el desgaste, perfil fijo o no por sesión y exclusión de fallas por sesión. Incluye traducir los valores cualitativos ya aportados (1/200 h, 2/7 vuelos, +5 simultáneas...) a una mecánica concreta que el engine pueda ejecutar.

## Answer

- **Cadencia (Q21)**: método = fallas esperadas por hora de vuelo por severidad, repartidas por el plan de intensidad (ticket 07). Los **valores numéricos quedan para una calibración posterior sobre papel** (abierta, no fija). Principio rector: en TODO perfil el usuario debe sentir el riesgo — fallas inofensivas (Minor/Major) frecuentes; al iniciar en parking con todo apagado, el engine puede sembrar **fallas de tierra** ("tomar un avión con fallas", reparable vía el mantenimiento del FMGC de Fenix) en todos los perfiles; solo consumen tiempo antes de salir. Ver ticket 11.
- **Concurrencia (Q22)**: tope de **seguridad** por severidad por perfil, configurable en `appsettings` — NO es la estimación de fallas por vuelo. Bajo: (Minor 5, Major 2, Critical 0); Moderado: (Minor 7, Major 4, Critical 1); Alto: (Minor 10, Major 5, Critical 2); 7700: sin límites. Doble crítica simultánea en Alto es caso borde (≈1 cada 50 vuelos).
- **Cooldown (Q23)**: solo tope de **seguridad mínimo** configurable; por diseño el engine **no depende de un cooldown real** — la credibilidad de la selección hace que las fallas "tengan sentido" (la cadencia la impone la narrativa/plausibilidad, no un reloj). Valores definitivos → calibración posterior.
- **Fallas temporales (Q24)**: proporción decrece con la dureza — Bajo: mayoría temporales; Moderado: mitad y mitad; Alto: solo algunas menos críticas; 7700: casi ninguna y más largas.
- **Distribución temporal (Q25)**: sesgo por fase + prohibiciones físicas + **preplaneo según la duración estimada del OFP**. Las prohibiciones físicas ya existen en el catálogo (campo `FlightPhase` en `failurePresets`: cada falla solo se activa en su fase). Matiz por analizar con pinzas: en perfiles con críticas, la probabilidad de una crítica debe ser ~igual en cualquier fase (crucero concentra el mayor % de duración), pero el impacto para el piloto difiere enormemente por fase (falla de motor en despegue vs crucero vs aterrizaje) → distribución por severidad abierta (niebla; ticket 07 al momento de definir el pipeline).
- **Perfil fijo por sesión (Q26)**: sí, 1 sesión = 1 perfil de riesgo, elegido al iniciar.
- **Exclusión de fallas (Q27)**: **eliminada del diseño** — el usuario no puede excluir ni conocer las fallas posibles de una sesión (inmersión total, "esperar cualquier cosa"). Invalida la opción de exclusión mencionada en la ronda anterior y en el ticket 02.
