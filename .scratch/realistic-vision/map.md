# Mapa — Visión del modo Realistic

## Destination

Documento de diseño nuevo (`docs/realistic-mode/diseno-objetivo.md`, al final del camino) que define el modo Realistic de punta a punta desde cero, con **tu visión**: qué ve e ingresa el usuario en la app (UX e inputs), cómo piensa y decide el engine en segundo plano (objetivo y lógica), y qué ejecuta y devuelve (outputs, persistencia, notificaciones), con el detalle de qué entra, qué sale y en qué momento exacto de cada flujo. La viabilidad se analiza en un paso posterior (idea primero, factibilidad después).

## Notes

- Dominio: aviación comercial / Fenix A320 (A319/A320/A321, futuras NEO). Vuelos siempre operativos (simbrief).
- Skills: grilling y domain-modeling al resolver cada ticket.
- `docs/realistic-mode/estado-y-diseno.md` es referencia de lo ya implementado, **no** restricción para este diseño.
- Preferencias: documento en español; decisiones del usuario final; implementación fuera de alcance.

## Decisions so far

- [Destino](issues/01-perfiles-riesgo.md): el documento plasma la visión, no la implementación actual.
- Q1 → Artefacto final: documento de diseño nuevo en `docs/realistic-mode/diseno-objetivo.md`.
- Q2 → Primero la idea y lo que se quiere lograr; la viabilidad se ve después.
- Q3 → El documento detalla qué entra/sale y cuándo en cada flujo (p. ej. armar → confirmar Fenix → persistir → mostrar).
- Q4 → Inicio exige: SimConnect + Fenix activos, aeronave registrada, OFP SimBrief descargado y validado (ID configurado; si no, pedirlo; validar hora de salida UTC vs. hora actual), perfil de riesgo elegido. La fase del vuelo **no** restringe iniciar/pausar/detener. *(Corregido parcialmente por Q36/Ticket 12: Fenix ya no es imprescindible para iniciar.)*
- Q5 → Panel de fallas con estados por color (enviada vs. activa confirmada); log izquierdo solo con eventos validados; ventana del modo muestra el "pensamiento" del engine.
- Q6 → Botón para apagar todas las fallas (por VATSIM, distinto de terminar sesión); sin ayudas; historial minuto a minuto al terminar.
- Q7 → Perfil de riesgo = perfil de comportamiento completo del engine (cadencia, límites, tipos, agresividad); no es solo un multiplicador. (Corregido por Q27: **sin** exclusión de fallas; corregido por Q21: valen también para todo perfil las fallas inofensivas frecuentes.)
- Q8 → Narrativa emergente sin guion previo, pero con **conciencia del horizonte del vuelo**: no atomizar, no concentrar al inicio (vuelo de 6 h ≠ fallas a los 5 min); a resolver bien en el ticket del motor.
- Q9 → Plausibilidad = contexto + historial de sesión + reglas del catálogo + perfil de riesgo (el más importante). OFP **no** condiciona ventanas de ejecución; las fases salen del detector.
- Q10 → Severidades: Minor/Major/Critical. Perfiles: Bajo (nunca Critical, Major muy rara), Moderado (Major normal, Critical muy rara), Alto (todas, críticas baja-media), 7700 (todas, críticas media-alta). Valores cualitativos iniciales; requieren calibración (ticket 01).
- Q11 → Orden estricto: armar → confirmación Fenix → persistencia atómica → notificación. Rechazo = no-evento (no se persiste como activa; puede reelegir).
- Q12 → La sesión termina con botón de cierre o al cerrar la app (obliga a terminar primero: muestra resultados y desgaste).
- Q13 → Historial: fallas + eventos + desgaste acumulado + acción del usuario por falla (solucionada/ignorada), distinguiendo quién la desactivó (engine / avión).
- Q14 → "Sesión" ≈ "gran parte del vuelo" (idealmente de subida a aterrizaje), no obligatorio iniciarla en tierra; **pausa del motor de fallas** (desactiva activas y bloquea nuevas, la sesión sigue viva) en lugar de terminar/reiniciar seguido; cerrar en vuelo pide confirmación; se puede iniciar otra sesión después.
- Q15 → Al reanudar el motor: por defecto continúa sin fallas activas; con un **checkbox** el usuario restaura las fallas activas previas a la pausa. Historial registra cada pausa/reanudación y la elección.
- Q16 → Pensamiento del engine en la pantalla del modo, oculto con un "ojito" por defecto, visible a voluntad (debug); log técnico completo en modo debug sin repetir ruido.
- Q17 → Plan de intensidad: el engine usa el OFP solo para duración estimada y fija en qué tramos hay más presión; se reescala en vivo con la telemetría real; no es libreto.
- Q18 → Fallas temporales: auto-resolución por tiempo y/o condición; el tiempo puede ser aleatorio; depende de cada falla (validación manual del catálogo).
- Q19 → Verificación de confirmación: 2-3 ticks de gracia antes de descartar como no-evento.
- Q20 → Si la app se cierra con sesión abierta: al reabrir se detecta la sesión sin finalizar, se finaliza manualmente y se muestran los resultados.
- [Ticket 01](issues/01-perfiles-riesgo.md): cadencia por hora por severidad (valores → calibración posterior); fallas de tierra frecuentes en todo perfil (ticket 11); concurrencia = tope de seguridad por severidad configurable (Bajo 5/2/0, Moderado 7/4/1, Alto 10/5/2, 7700 ∞); cooldown = solo tope de seguridad, el diseño no depende de él; temporales decrecen con la dureza; sesgo por fase + prohibiciones físicas (`FlightPhase`) + preplaneo por OFP; distribución de críticas por fase → análisis abierto; perfil fijo por sesión; **sin exclusión de fallas**.
- [Ticket 02](issues/02-configuracion-sesion.md): aeronave por ABM existente (Registration + IcaoTypeCode); OFP validado por (1) ICAO del tipo = ICAO seleccionada, (2) campos mínimos, (3) ventana temporal [offblock − 2 h, aterrizaje est. + 2 h] (UTC); sin OFP válido no se arranca (bloqueante); ID SimBrief en `%AppData%\RealFenixFailures\userprefs.json` (solo PC del usuario); OFP extrae Flight log, Fuel, Pesajes, TLR (muy útiles), Routing/Weather/Encabezado/ATC (secundarios), NOTAMs no prioritarios.
- [Ticket 03](issues/03-ciclo-vida-sesion.md): estados `Inactiva → Activa ⇄ Motor pausado → Terminada`; desconexión en vuelo = espera reanudable (re-consultar fallas al volver); **cada sesión = historial aparte** (Q72 corrige Q35: sin agrupación en vuelo físico); **cada sesión descarga su OFP**; terminar limpia fallas activas. Nuevo requisito → Ticket 12.
- [Ticket 04](issues/04-ciclo-vida-falla.md): estados `Seleccionada → En cola → En preparación (sin Fenix) → Enviada → Activa confirmada → resuelta` o `Rechazada`; snapshot completo al activarse; enfriamiento de seguridad tras críticas (configurable); rechazo = no-evento (2-3 ticks) con caso especial de fallas iniciales (espera infinita en fase, descarte al cambiar fase); umbrales físicos + `FlightPhase` como red de seguridad; razonamiento solo a log técnico debug.
- [Ticket 11](issues/11-fallas-tierra-parking.md): fallas iniciales aleatorias (Minor/Major reparables) por tope de perfil (1-2/1-3/2-4/3-5); no reparar = sigue activa (registro "no resuelta al despegue"); **aparte** de la cuota del vuelo; cuentan en historial; solo en parking; desgaste: lo genera el tiempo de vuelo, no las fallas (mantenimiento FMGC o manual en UI resetea a cero — ticket 08).
- [Ticket 05](issues/05-fallas-temporales.md): `PuedeSerTemporal` **por falla dentro del preset**, probabilística (a veces se auto-repara, a veces no); auto-resolución **por tiempo** configurable/aleatorio en cada armado; desactivar → confirmar → persistir `Auto-resuelta (engine)`; historial con 3 estados inequívocos (engine/piloto/no resuelta); duración escalada por perfil (Bajo ×0.5 → 7700 ×2.5); proporción decrece con la dureza; respiro tras crítica solo para críticas.
- [Ticket 06](issues/06-resolucion-piloto.md): detección por observación (Fenix reporta inactiva sin orden del engine); registro hora/fase/duración; **no puede ocurrir durante la pausa** (el engine desactiva todo forzoso); re-armado posible más tarde (otra instancia, con cooldown/plausibilidad); "ignorada" = `No resuelta (sesión terminó)`, sin inferir intención.
- [Ticket 07](issues/07-motor-pipeline.md): pipeline `Observar → Contextualizar → Elegir candidatos → Decidir → Encolar/armar → Registrar`; plan de intensidad con curva por fase (hot: takeoff/approach; frío: cruise repartido) reescalable en vivo; **críticas ponderadas por distribución histórica real** (Aterrizaje/Approach > Despegue/Ascenso > Cruise > Taxi) y por tipo de falla (motor/aves → despegue; sistemas → cruise; tren/hidráulica → aterrizaje); `FlightPhase` = red de seguridad ← perfil + cuota ponderada decide si; pensamiento público de nivel "decisión", oculto por defecto; memoria de sesión con contador de fallas por sistema.
- [Ticket 08](issues/08-desgaste.md): desgaste por sistema — tabla `FenixFailureSystems` **poblada desde `failures.json`** (19 ATAs: Air, AutoFlight, Communications, Elec, Fire, Flight controls, Fuel, Hydraulic, Ice/Rain, Indicating/Recording, Gear, Navigation, Oxygen, Pneumatic, Information, APU, Doors, Engines, Start Faults); lo genera el tiempo de vuelo (no las fallas); tasas distintas configurales + matiz de fase; amplifica elegibilidad pero nunca cruza severidad del perfil; atributo de aeronave registrada en BD, reseteable por FMGC o manual en UI; escala 0-100 % con aviso al umbral alto.
- [Ticket 09](issues/09-historial-resultados.md): **cada sesión = historial aparte**; cabecera (inicio, duración, origen→destino, conteo por severidad, perfil); detalle de falla (snapshot completo); desgaste en cabecera del vuelo (por sistema, nuevo + total + aviso mantenimiento); **desgaste solo con motor corriendo**; reabrir con sesión sin finalizar → tarjeta + "Finalizar y ver resultados" (limpia fallas activas).
- [Ticket 10](issues/10-ux-ventana-realistic.md): layout adaptado al código actual (4 zonas); colores por severidad (enviada=amarillo, activa Major/Minor=naranja, activa Critical=rojo, resuelta=verde, rechazada=gris); **un solo botón de pausa** que desactiva fallas activas de Fenix (el motor no se pausa, sigue pensando; bloquea armado nuevo; checkbox de restauración al volver); modal de resultados al terminar; conexión = lo implementado hoy; **sin notificaciones del sistema** — fallas solo en log del modo con ojito (debug).
- [Ticket 12](issues/12-modo-sin-fenix.md): motor corre pipeline normal y fallas van a `En preparación` (sin contar cadencia); espera vivo sin tope (colas máx. N configurable, deja de elegir al llegar); al aparecer Fenix envía en orden + respeta topes de simultáneas/cooldown (iniciales de tierra primero); botón pausa deshabilitado con aviso + acción secundaria "Descartar fallas preparadas"; fallas iniciales descartadas si Fenix tardío (sin reintento); historial idéntico (solo log técnico marca preparación).

## Destination reached — resumen del camino

Los 12 tickets se resolvieron en diálogo Q1–Q89 y este mapa quedó completo. El artefacto final (documento `docs/realistic-mode/diseno-objetivo.md`) se redacta a partir de la suma de las decisiones aquí registradas. Pendientes técnicos post-diseño (investigación de viabilidad, valores numéricos, catálogo): ver "Not yet specified".

## Not yet specified

- Capacidades exactas de la API Fenix (qué fallas se pueden inyectar, cómo reporta estado activo/armado) → research al completar la visión.
- Detalle técnico de la API SimBrief (descarga de OFP, campos) → research después.
- Calibración posterior sobre papel de los valores numéricos de perfiles (cadencia exacta, cooldown de seguridad) y de los topes de concurrencia.
- Distribución de probabilidad de fallas críticas por fase (impacto piloto difiere por fase; análisis con pinzas).
- Inventario del catálogo de fallas: cuáles son temporales/permanentes, condiciones, severidad → validación manual por falla.
- Esquema de datos del timeline minuto a minuto del historial (ticket 09).
- Qué pasa si el usuario desactiva una falla desde el avión *sin* pausar el motor (ticket 06).
- Mecánica exacta del "motor en modo preparación" sin Fenix (ticket 12).

## Out of scope

- La implementación (nada de código del engine en este esfuerzo).
- Ayudas en vivo dentro de la sesión ("qué falló" / "cómo se resuelve").
- Exclusión o configuración de fallas por el usuario (conocer las posibles rompe la inmersión; Q27).
- Auto-reanudación de sesiones tras cierre de la app.
