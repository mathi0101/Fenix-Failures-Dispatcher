# Modo Realistic — Diseño objetivo

> Documento de diseño del modo Realistic **tal como el usuario lo concibe** (visión), redactado a partir del intercambio de definición (Q1–Q89). Plasma qué ve e ingresa el usuario, cómo piensa y decide el motor, y qué ejecuta y devuelve, con el detalle de qué entra, qué sale y en qué momento. La **implementación actual** se documenta en `estado-y-diseno.md`; **este documento no es restricción de lo ya hecho**, es la fuente de la intención.

## 1. Visión

El modo Realistic genera **fallas con narrativa propia** durante un vuelo en aviones Fenix Simulations (A319/A320/A321 — vuelos siempre operativos, respaldados por un plan de vuelo de SimBrief). El usuario debe **sentir el riesgo real**: el avión "envejece" con desgaste, puede recibirse un avión con fallas en tierra, y el motor decide en cada momento con **plausibilidad y conciencia del horizonte del vuelo** (no un aluvión al inicio, no fallas atomizadas cada 5 minutos; las fases, el historial de la sesión y el perfil de riesgo lo guían). Todo lo que no rompa la inmersión queda fuera; no hay ayudas ni notificaciones.

## 2. Piezas del dominio (glosario breve)

- **Sesión Realistic**: intervalo en que el sistema de fallas está activo. **Cada sesión es su propio historial/vuelo por separado** (dos sesiones pueden usar el mismo OFP sin importarle a la sesión).
- **Sistema de fallas (motor)**: decide, tick a tick, si arma una falla y cuál.
- **Perfil de riesgo**: comportamiento completo del motor (severidades permitidas, cadencia, concurrencia, límites, temporales, distribución temporal). No es un multiplicador.
- **Pausa del sistema de fallas**: un solo botón que **desactiva las fallas activas en Fenix** y bloquea armado nuevo; la sesión sigue viva y el motor sigue pensando.
- **Falla inicial (de tierra)**: falla Minor/Major que se carga en parking, reparable antes de salir (mantenimiento FMGC de Fenix).
- **Falla temporal**: falla con `PuedeSerTemporal` (por falla dentro del preset, probabilística): a veces la auto-resuelve el motor por tiempo, a veces no.
- **Confirmación de Fenix**: distingue "enviada a Fenix" de "activa confirmada". Sin confirmación → no-evento.
- **Pensamiento del motor**: líneas breves de razonamiento en vivo, ocultas por defecto (ícono de ojo), para debugging.
- **Plan de intensidad**: dosificación temporal desde el OFP, reescalada en vivo con la telemetría.
- **OFP**: plan de vuelo de SimBrief; requerido para iniciar; es **horizonte**, no condiciona la ejecución.

## 3. Flujo de alto nivel (qué entra y sale, y cuándo)

```
[Usuario elige aeronave + perfil] → [SimConnect conectado; OFP descargado y válido]
→ SESIÓN ACTIVA (motor corriendo)
    → en cada tick: Observar → Contextualizar → Elegir candidatos → Decidir → Encolar/armar → Registrar
    → si decide armar: falla → En cola → Enviar a Fenix → Confirmar (2-3 ticks) → Persistir (falla + evento, atómico) → Mostrar en UI
    → rechazo: no-evento (solo log técnico)
→ Pausa del sistema (botón): desactiva activas en Fenix, bloquea nuevas (sesión viva; motor sigue pensando)
→ Reactivar: por defecto sin fallas; con checkbox, restaura el conjunto previo.
→ Terminar sesión (confirma si está en vuelo): limpia fallas activas en Fenix, persiste historial + desgaste → modal de resultados.
→ App cerrada con sesión viva: al reabrir, tarjeta "sesión sin finalizar" → "Finalizar y ver resultados" (limpia activas).
```

## 4. Configuración y arranque

Requisitos para iniciar una sesión:
1. **SimConnect conectado** (obligatorio). **Fenix NO imprescindible** (ver §15).
2. **Aeronave registrada** elegida (ABM existente: `Registration` + `IcaoTypeCode`).
3. **OFP de SimBrief** descargado y válido:
   - ICAO del tipo de aeronave del OFP = ICAO del tipo seleccionado.
   - Campos mínimos presentes (origen, destino, duración estimada).
   - **Ventana de tiempo** (lo más importante, UTC): el usuario puede iniciar hasta **2 h antes del offblock estimado** y el OFP vale hasta **2 h después de la hora de aterrizaje estimada**.
4. **Perfil de riesgo** elegido (1 sesión = 1 perfil, fijo).

La fase de vuelo **no restringe** iniciar/pausar/detener. Sin ID de SimBrief: se pide, y no arranca sin OFP válido (bloqueante). ID SimBrief solo en `%AppData%\RealFenixFailures\userprefs.json` (PC del usuario, privacidad); el resto persiste en BD. Los topes de seguridad son configurables en appsettings.

**Del OFP se extrae** (documentado en `estado-y-diseno.md` §SimBrief/OFP): muy útiles = Flight log (FL, MN, viento, OAT, EFOB, TAS/GS, EET, distancias), Fuel, Pesajes, TLR; secundarios = Routing, Weather, Encabezado, ATC; no prioritario = NOTAMs.

## 5. Perfiles de riesgo (comportamiento completo)

Severidades: **Minor** (se resuelve sola o con respaldos), **Major** (obliga a ECAM actions/checklist, resoluble a mano), **Critical** (obliga a desvío o emergencia).

| | Bajo | Moderado | Alto | 7700 |
|---|---|---|---|---|
| Severidades | Crit. nunca; Major muy rara | Major normal; Crit. muy rara | Todas (crít. baja-media) | Todas (crít. media-alta) |
| Cadencia | Minor/Mayor inofensivas frecuentes | Minor 2/7 vuelos aprox, con simultáneas | Major y Minor a menudo | Hasta +5 de cada tipo a la vez |
| Tope simultáneas (seguridad) | Minor 5, Major 2, Crit. 0 | Minor 7, Major 4, Crit. 1 | Minor 10, Major 5, Crit. 2 | Sin límites |
| Temporales | Mayoría | Mitad | Solo algunas | Casi ninguna, más largas |
| Escala duración temporal | ×0.5 | ×1 | ×1.5 | ×2.5 |
| Respiro tras crítica | — | — | 10-15 min (configurable) | 5 min (configurable) |
| Público | Principiantes, inmersión segura (VATSIM ok) | Sólidos en sistemas; VATSIM ok | Expertos; manejo de emergencias | "Documentales de accidentes"; **VATSIM prohibido** |

Notas:
- **Cadencia** = fallas esperadas por hora por severidad, repartidas según el plan de intensidad y **los pesos por fase históricos** (§11). Los **valores numéricos requieren calibración sobre papel** (a definir al implementar); los expuestos son orientativos.
- **Cooldown**: no existe un cooldown real como diseño; la credibilidad de la selección dicta la cadencia. Solo hay **topes de seguridad** (concurrencia, respiro tras crítica).
- **En todo perfil**: el usuario debe sentir el riesgo — fallas inofensivas frecuentes y **fallas iniciales en parking** (§7).
- El usuario **no puede excluir ni conocer las fallas posibles** de una sesión (inmersión total).

## 6. Ciclo de vida de la sesión

Estados: `Inactiva → Activa (motor corriendo) ⇄ Pausa del sistema → Terminada`.

- **Desconexión en pleno vuelo** (SimConnect/Fenix): la sesión **aguanta**; el motor entra en espera (no evalúa ni ejecuta) y al reconectarse **reanuda solo**; al volver se **re-consulta Fenix** para reconciliar el estado real de las fallas (qué siguió activa, qué ya no).
- **Pausa del sistema (un solo botón)**: desactiva las fallas activas en Fenix y bloquea el armado de nuevas; el motor sigue pensando (el avión vuela). Al reactivar: por defecto sin fallas; **checkbox** para restaurar el conjunto previo. Cada pausa/reactivación y elección se registra en el historial.
- **Cierre de app con sesión viva**: al reabrir, tarjeta "sesión sin finalizar" → "Finalizar y ver resultados" (persiste, muestra, y **limpia fallas activas** en Fenix).
- Terminar la sesión en vuelo pide confirmación. Terminar **siempre limpia las fallas activas** en Fenix.

## 7. Fallas iniciales (tierra)

- Sembradas **solo si la sesión empieza en parking** (fase inicial, todo apagado), idealmente **antes de conectar GPU/APU**: "recibir un avión con fallas" (mantenimiento vía FMGC de Fenix como opción).
- Cantidad por tope de perfil (configurable): Bajo 1-2, Moderado 1-3, Alto 2-4, 7700 3-5. Composición aleatoria del catálogo de fallas de tierra (Minor/Major reparables), respetando coherencia (no dos del mismo sistema, sin contradicciones).
- **No reparar y salir igual**: la falla sigue activa (largar con un sistema fallado); el historial la marca "no resuelta al despegue".
- Son **aparte** de la cuota del vuelo (la cuota corre al dejar parking), pero **cuentan** en el historial/sesión; el tope de simultáneas aplica en conjunto.
- Si Fenix tardó y la fase ya cambió (taxi/despegue) → se **descartan definitivamente** (sin reintento como fallas de vuelo; log técnico).

## 8. Ciclo de vida de una falla

Estados: `Seleccionada (motor pensando) → En cola → En preparación (Fenix no disponible) → Enviada a Fenix → Activa confirmada → (Auto-resuelta por engine / Solucionada por piloto / No resuelta)`; o `Rechazada (no-evento)`.

Flujo: armar → Fenix confirma (2-3 ticks de verificación) → persistir **atómico** (falla + evento) → mostrar en UI. Rechazo → no-evento (solo log técnico; puede reelegir).

- **Snapshot persistido al activarse**: preset (qué/sistema/severidad), perfil de la sesión, momento (timestamp + fase + horas avión/OFP), condiciones (altitud/IAS/motores) y luego la resolución (quién). El razonamiento del motor **solo** al log técnico (debug).
- **Umbrales físicos**: avión en tierra con motores apagados (salvo falla inicial), avión configurado, sim no pausado, reglas del preset (altitud/IAS/condiciones) y **`FlightPhase` como red de seguridad** (dónde puede dispararse). Nunca fallas absurdas en el momento.
- **Respiro tras crítica**: enfriamiento de seguridad configurable (las críticas, sean temporales o no). Minor/Major nunca generan respiro.

## 9. Fallas temporales

- `PuedeSerTemporal` **por falla dentro del preset** (probabilística: a veces se auto-repara, a veces no).
- Auto-resolución **por tiempo**: duración configurable/aleatoria en cada armado, **escalada por perfil** (§5).
- Al vencer: el motor desactiva en Fenix → confirma → persiste `Auto-resuelta (engine)`.
- Historial distingue inequívocamente: `Auto-resuelta (engine)` / `Solucionada por piloto` / `No resuelta (sesión terminó)`.

## 10. Resolución por el piloto

- Detección **por observación**: Fenix reporta inactiva una falla que no desactivó el motor (ni temporal vencida) → `Solucionada por piloto` en ese instante.
- Registro: hora/fase/duración. No se infieren intenciones (la "ignorada" es `No resuelta (sesión terminó)`).
- **Durante la pausa no ocurre**: la pausa desactiva todo forzoso (el piloto las ve desaparecer del ECAM).
- Re-armado posible más tarde (otra instancia, con cooldown/plausibilidad) — "si algo falló y se reparó en vuelo, puede volver a fallar".

## 11. El motor: pipeline y plan de intensidad

Pipeline por tick: `Observar → Contextualizar → Elegir candidatos → Decidir → Encolar/armar → Registrar`.

- **Plan de intensidad**: curva por fase desde el OFP (solo duración estimada). Ventanas calientes: takeoff/initial climb y approach/landing (más cadencia Minor/Major por minuto). Cruise: menor intensidad repartida por el tramo restante. **Reescala en vivo** con la telemetría real. La cuota del perfil se proyecta sobre esa curva: así un vuelo de 6 h no concentra fallas al inicio.
- **Peso de críticas por fase (distribución histórica real, fuentes: Airbus, FSF)**: Aterrizaje/Aproximación > Despegue/Ascenso > Cruise > Taxi. Y **por tipo de falla**: motor/bird strike/FOD → despegue/ascenso inicial; fallas de sistemas → cruise (mayor tiempo); tren/frenos/hidráulica → aterrizaje/aproximación. Los pesos del plan de intensidad se modelan con esto (calibración a implementar).
- `FlightPhase` = red de seguridad (dónde); el perfil + cuota ponderada decide **si** dispara.
- **Pensamiento público**: nivel "decisión" (`evaluando (fase, N candidatos)`, `considerando HYD-01`, `armando ENG-02`, `Fenix confirmó`, `esperando Fenix...`, `motor pausado`), oculto por defecto (ojito), visible a voluntad. Razonamiento completo → log técnico (debug), sin ruido repetitivo.
- **Memoria de sesión**: fallas activadas (sistema/severidad/momento), cooldowns de seguridad, resoluciones, pausas/reactivaciones, perfil, desgaste de la aeronave y **contador de fallas por sistema** (narrativa coherente: no repetir el mismo sistema seguido).

## 12. Desgaste de sistemas

- **Lo genera el tiempo de vuelo de la sesión, solo cuando el motor corre** (las fallas no generan desgaste).
- **Por sistema**: tabla `FenixFailureSystems` poblada desde `failures.json` (Domain) — 19 ATAs (Air, AutoFlight, Communications, Elec, Fire, Flight controls, Fuel, Hydraulic, Ice/Rain, Indicating/Recording, Gear, Navigation, Oxygen, Pneumatic, Information, APU, Doors, Engines, Start Faults).
- Tasas **distintas por sistema** (configurables en appsettings) + matiz de fase (tren en aproximación, motores en despegue).
- Amplifica la **elegibilidad** de las fallas del sistema, **pero nunca cruza la severidad máxima del perfil** (el techo lo pone el perfil).
- Persistido por aeronave registrada en BD, acumulado entre vuelos. **Reset**: reparar con mantenimiento (FMGC en tierra) o **manual desde la UI** (por sistema, inmediato) — incluso críticas no reparables desde mantenimiento.
- Escala 0-100 %; al pasar el umbral alto (configurable, p. ej. 80 %) la app avisa "sistema requiere mantenimiento".

## 13. Historial y resultados

- **Cada sesión = historial/vuelo por separado** (lista de sesiones; dos pueden referenciar el mismo OFP).
- **Cabecera de sesión**: hora de inicio, duración, origen→destino (visual), conteo de fallas por severidad, perfil usado.
- **Detalle de falla**: snapshot completo (§8), fase de resolución, quién la terminó, contexto.
- **Desgaste**: en la cabecera, por sistema (nuevo acumulado + total de la aeronave + aviso mantenimiento).
- **Al terminar**: modal de resultados automático (resumen + acceso al historial completo y al desgaste); el historial queda persistido y disponible.
- **Sesión sin finalizar** (cierre de app): tarjeta al reabrir → "Finalizar y ver resultados" (limpia fallas activas en Fenix).

## 14. UX de la ventana del modo Realistic

Flujo objetivo de **tres pasos** (sustituye el paso independiente de riesgo y el de activación del prototipo anterior):

1. **Aeronave**: seleccionar o registrar. Cada tarjeta muestra matrícula, ICAO, horas, vuelos y promedio simple del desgaste disponible por sistema; si no hay registros de desgaste, muestra «Sin datos», nunca 0 % supuesto. Matrícula: máximo 10 caracteres, quitar espacios al principio y al final y convertir a mayúsculas al guardar; tipo ICAO elegible exclusivamente de A319, A320 o A321.
2. **Resumen de aeronave**: conservar estadísticas, desgaste desglosado e historial expandible; mostrar origen→destino, OFP, perfil, fallas y resultados de cada sesión cuando existan. Las sesiones previas sin OFP muestran «No registrado»; una sesión sin finalizar se identifica aparte y se podrá finalizar con limpieza y resultados cuando exista soporte real.
3. **Preparación y sesión en una misma pantalla**: cabecera con aeronave clicable para ver desgaste por sistema, perfil y estado de sesión. SimConnect, Fenix y fase se consultan en la barra de estado global, **sin duplicarlos** en una tarjeta del paso. Dos bloques compactos ocupan el ancho disponible: ruta/OFP y requisitos de activación (incluidos SimConnect, datos coherentes, OFP validado y perfil). El botón del requisito «Perfil» abre **una ventana modal** con los cuatro perfiles expandibles. Cada ficha muestra severidades, cadencia, concurrencia/topes, temporales, público («Ideal para pilotos que…») y uso en VATSIM. El usuario selecciona un perfil, confirma abajo o cancela sin alterar su elección previa; puede cambiarlo hasta iniciar la sesión. 7700 es un **perfil de riesgo**, no una orden de transpondedor, y no es apto para VATSIM.

Al intentar iniciar con requisitos válidos aparece una **confirmación VATSIM** en vez de un aviso permanente: el controlador tiene la última palabra; si pide quitar emergencias, el usuario debe pausar las fallas sin terminar la sesión cuando esa función exista. Hasta entonces, debe detener y comprobar el estado real de las fallas en Fenix. Es preferible terminar la sesión en tierra, pero se permite hacerlo en vuelo con confirmación. La pausa voluntaria y la espera por desconexión se distinguen explícitamente.

Dentro del paso 3, el contenido central se desplaza con rueda/teclado sin mostrar la barra nativa blanca y la franja de acciones permanece accesible en el tamaño mínimo de la ventana. La interfaz comunica trabajo asíncrono sin duplicar comandos ni bloquear el hilo de WPF. Las fallas muestran colores por estado/severidad: **enviada** amarillo, **activa Minor/Major** naranja, **activa Critical** rojo, **resuelta** verde, **rechazada/no-evento** gris breve. El pensamiento del motor **solo aparece con una sesión activa**; permanece oculto por defecto detrás del ojo, con líneas de nivel decisión al abrirlo. Acciones inferiores separadas: **Activar / Terminar sesión** (confirmación en vuelo) y **Pausar / Reactivar fallas** (pausa desarma fallas en Fenix sin cerrar sesión; restauración optativa); sin Fenix, pausa deshabilitada con explicación y acción secundaria para descartar preparadas.

Hasta disponer de contratos reales para OFP, estados de falla, pensamiento, perfiles completos y pausa/desarme, los controles de esas capacidades se muestran pendientes o deshabilitados: no se simula confirmación, no se inventan rutas ni se habilita iniciar sin OFP. Los topes numéricos objetivos son los de §5; la cadencia precisa sigue pendiente de calibración. **Sin notificaciones del sistema** (ni toast ni sonidos); el feed izquierdo no muestra fallas. El estado de implementación y las limitaciones actuales están en `estado-y-diseno.md`.

## 15. Iniciar sin Fenix operativo (modo preparación)

- Se puede iniciar el modo con **SimConnect conectado aunque Fenix no responda** (p. ej. mientras el simulador carga el avión).
- El motor corre su pipeline normal y las fallas decididas van a **`En preparación`** (sin contar cadencia). **Espera mientras la sesión viva**, pero sin acumular sin tope: al llegar a N fallas en cola (configurable, p. ej. 5) deja de elegir candidatas hasta que Fenix aparezca o se descongele.
- **Al aparecer Fenix (200)**: envía en **orden de prioridad** y **respetando topes de simultáneas y cooldown** — primero fallas iniciales de tierra (si aplica), luego el resto; lo sin cupo queda en preparación.
- Botón de pausa deshabilitado con aviso ("Fenix no disponible: las fallas no se pueden activar") + acción secundaria "Descartar fallas preparadas".
- Las fallas enviadas desde preparación son **idénticas en el historial** ("preparada sin Fenix" solo en log técnico).

## 16. Pendientes para la implementación (post-diseño)

- **Viabilidad técnica** de todo lo anterior contra la API real de Fenix y SimConnect (investigación).
- **Calibración numérica sobre papel**: cadencia por perfil, topes exactos, escalas de duración temporal, respiros, probabilidades por fase/tipo de falla.
- **Catálogo de fallas**: validar por falla `PuedeSerTemporal`, severidad, condiciones, fase (el `FlightPhase` ya existe en `failurePresets`).
- **Esquema de datos** del timeline / snapshot de falla / desgaste / sesión.
- **Confirmación de supuestos del dominio**: distribución histórica de críticas por fase (Airbus/FSF) para fijar los pesos.
