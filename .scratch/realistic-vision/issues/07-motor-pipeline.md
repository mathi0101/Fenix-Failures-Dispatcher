# Ticket 07 — Motor: pipeline de decisión y plan de intensidad

Type: grilling
Status: resolved
Blocked by: 01 (resuelto)

## Question

Cómo piensa el engine: pipeline tick a tick (contexto → elegibilidad → ponderación → decisión → ejecución → persistencia → replanificación). Plan de intensidad: el engine usa el OFP solo para la duración estimada y fija en qué tramos del vuelo hay más presión (sin libreto de qué falla), y lo reescala en vivo con la telemetría real. Cómo dosifica para evitar concentrar fallas al inicio de vuelos largos. Memoria de la sesión. Interacción con pausa de motor (duerme) y con el desgaste. Pensamiento público y log técnico en debug sin ruido repetitivo.

## Answer

- **Pipeline (Q61)**: `Observar → Contextualizar → Elegir candidatos → Decidir → Encolar/armar → Registrar` (confirmado; coincide con la visión del proyecto en `estado-y-diseno.md`).
- **Plan de intensidad (Q62)**: curva de intensidad por fase con el OFP solo para la duración estimada; takeoff/initial climb y approach/landing = ventanas calientes (más cadencia Minor/Major por minuto); cruise = menor intensidad por minuto repartida uniforme a lo largo del tramo restante. La cuota del perfil (fallas/hora) se proyecta sobre esa curva. Reescala en vivo si la realidad se desvía del plan.
- **Peso de críticas por fase (Q63, datos del usuario)**: **rechaza la distribución uniforme** — el peso de las críticas debe seguir la distribución histórica de eventos: **Aterrizaje/Aproximación (la más peligrosa, concentra más accidentes), Despegue/Ascenso (segunda, baja altura + alta carga), Cruise (mucho menor), Taxi (la menor)**. Fuentes: Airbus Accident Statistics, Flight Safety Foundation. Además, **el peso depende del tipo de falla**: motor / bird strike / FOD → despegue y ascenso inicial (máxima potencia, exposición cerca del aeropuerto); fallas de sistemas detectadas en vuelo → cruise (mayor tiempo, sistemas trabajando sostenido); aterrizaje/aproximación → eventos operacionales (no mecánica pura — tren, frenos, hidráulica de alta carga). **Conclusión**: los pesos de probabilidad de cada tipo de falla crítica deben modelarse con esa distribución por fase y por tipo de falla (análisis de calibración en el plano de intensidad).
- **Prohibiciones por fase (Q64)**: `FlightPhase` del preset = red de seguridad (dónde puede dispararse); la cuota de probabilidad uniforme/ponderada decide si dispara. O sea: `FlightPhase` decide *dónde*, la cuota decide *si*; la ponderación por fase aporta el sesgo estadístico real.
- **Pensamiento público (Q65)**: nivel "decisión": `evaluando (fase, N candidatos)`, `considerando HYD-01 (perfil: cruise)`, `armando ENG-02`, `Fenix confirmó`, `rechazado (Fenix no listo)`, `auto-resuelta HYD-01`, `esperando Fenix...`, `motor pausado`. El razonamiento completo (puntuaciones) va al log técnico debug. Oculto por defecto (ojito), visible a voluntad.
- **Memoria de sesión (Q66)**: fallas activadas (sistema, severidad, momento), cooldowns de seguridad, resolución de cada una (engine/piloto/no), historial de pausas/reanudaciones, perfil activo, desgaste actual de la aeronave, **y contador de fallas por sistema en la sesión** (para narrativa coherente: no repetir 3 veces el mismo sistema seguido; alternar sistemas y severidades dentro del perfil).
