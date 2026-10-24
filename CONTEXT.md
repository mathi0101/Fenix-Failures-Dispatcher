# Contexto del dominio — Real Fenix Failures

Glosario y decisiones de dominio. Sin detalles de implementación.

## Glosario

- **Modo Realistic**: modo de la app que genera fallas con narrativa propia durante un vuelo en aviones Fenix (A319/A320/A321, futuras NEO). Los vuelos son siempre operativos (plan de vuelo SimBrief).
- **Sesión Realistic**: intervalo en que el motor de fallas está activo con un perfil de riesgo y un OFP. **Cada sesión es su propio historial/vuelo por separado** (no se agrupan bajo un "vuelo físico").
- **Vuelo físico**: trayecto real en el simulador, de la subida al avión al apagado final. Puede contener varias sesiones.
- **Motor en preparación**: estado del motor al iniciar la sesión con SimConnect conectado pero Fenix aún no operativo: piensa, analiza y prepara fallas desde el arranque, y las envía cuando la API de Fenix responde (200).
- **Motor de fallas (engine)**: componente que decide, tick a tick, si arma una falla y cuál, según contexto, historial de la sesión, plausibilidad y perfil de riesgo.
- **Pausa del sistema de fallas**: un solo botón que **desactiva las fallas activas en Fenix** y bloquea el armado de nuevas; la sesión sigue viva y el motor sigue pensando (el avión vuela). Al reactivar, opción de restaurar el conjunto previo (checkbox). No es terminar la sesión.
- **Restauración de fallas**: al reactivar el sistema de fallas, devolver (por decisión explícita del usuario con un checkbox) el conjunto de fallas que estaban activas al momento de pausar.
- **Perfil de riesgo**: comportamiento completo del engine para una sesión (severidades permitidas, cadencia, concurrencia, límites de seguridad, fallas temporales, distribución temporal). No es un multiplicador de probabilidad. Perfiles: Bajo, Moderado, Alto, 7700.
- **Severidad**: clasificación de una falla: Minor (se resuelve sola o con respaldos), Major (obliga a ECAM actions/checklist, resoluble a mano), Critical (obliga a desvío o emergencia).
- **Falla temporal**: falla con `PuedeSerTemporal=true` (por falla dentro del preset, probabilística): el engine a veces la auto-resuelve por tiempo y a veces no. Falla permanente: `PuedeSerTemporal=false`, nunca se arregla sola (p. ej. daño de motor).
- **Falla de tierra (falla inicial)**: falla Minor/Major activada en parking con todo apagado, reparable antes de encender motores; crea la sensación de tomar un avión con fallas (mantenimiento vía FMGC de Fenix). Presente y frecuente en todos los perfiles. Se carga idealmente antes de conectar GPU o prender APU; si la fase avanza antes de que Fenix confirme, se descartan.
- **Confirmación de Fenix**: la falla armada aparece como activa en la API de Fenix. Distingue "enviada a Fenix" de "activa confirmada"; sin confirmación, es un no-evento.
- **Pensamiento del engine**: estados breves que muestran en vivo el razonamiento del engine; oculto por defecto (ícono de ojo), visible a voluntad para debugging.
- **Plan de intensidad**: dosificación temporal que el engine se fija a partir del OFP (duración estimada) y reescala en vivo con la telemetría real; decide cuándo y con qué presión, no qué falla. El peso de las fallas críticas por fase sigue la distribución histórica real (Aterrizaje/Approach > Despegue/Ascenso > Cruise > Taxi) y el tipo de falla (motor/aves → despegue; sistemas → cruise; tren/hidráulica → aterrizaje).
- **OFP**: plan de vuelo operativo de SimBrief. Requerido para iniciar una sesión; usado solo como horizonte, nunca como condicionante de ejecución (las fases salen del detector de vuelo).

## Decisiones de dominio

- La fase de vuelo no restringe iniciar, pausar o detener el modo Realistic.
- El historial registra quién terminó una falla: el engine (temporal) o el piloto (desde el avión), detectado por observación de Fenix.
- Apagar todas las fallas (por ej. por indicación de un controlador VATSIM) es distinto de terminar la sesión.
- El usuario no puede excluir ni conocer las fallas posibles de una sesión: inmersión total, "esperar cualquier cosa".
- El desgaste de un sistema lo genera el tiempo de vuelo de cada sesión (no las fallas); reparar con mantenimiento (FMGC en tierra, o manual desde la UI) resetea ese desgaste a cero. Los sistemas son los 19 ATAs de `FenixFailureSystems` poblado desde `failures.json` (Domain): Air, AutoFlight, Communications, Elec, Fire, Flight controls, Fuel, Hydraulic, Ice/Rain, Indicating/Recording, Gear, Navigation, Oxygen, Pneumatic, Information, APU, Doors, Engines, Start Faults.
- La concurrencia y el cooldown son topes de seguridad configurables; la cadencia real la dicta la narrativa y la plausibilidad, no un reloj.
- Una sesión tiene un único perfil de riesgo (1:N).
