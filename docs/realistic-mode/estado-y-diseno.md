# Modo Realistic — estado y diseño

Documento **vigente** del modo Realistic (2026-10). Reemplaza el diseño inicial que vivía en `Agent Prompts/prompt1.md`: ese archivo quedó **reemplazado por este documento** y no debe usarse como fuente (está sin versionar/ignorado; conservar solo como antecedente histórico, no como especificación).

Estado conjunto: el WIP original quedó committeado como `031a9e7` (branch `dev`); la estabilización de 2026-10-14 está aplicada en el árbol de trabajo y committeada, y las correcciones de telemetría y estabilización de fase de **2026-10-15** (detalladas abajo) quedaron committeadas en `dev` con el resto del working tree.

## Trazabilidad Debug de decisiones (2026-10-24)

Los logs diarios de `%AppData%\RealFenixFailures\logs\realfenix-<fecha>.log` incluyen la traza detallada del modo al habilitar `Serilog:MinimumLevel:Default = Debug` en appsettings. Para limitar el volumen al modo, configurar overrides `Debug` para `RealFenixFailures.Application.Session.RealisticSessionEngine`, `RealFenixFailures.Application.Services.RealisticSessionManager` y `RealFenixFailures.Application.Services.FlightSessionService`. Debug de compilación y nivel de logging son independientes; en la compilación Debug también se superpone appsettings.Development.json.

- Solo decisiones positivas: inicio aceptado y cambios efectivos. No hay trazas por tick ni listados de candidatas, descartes por reglas/score, treguas en curso, esperas de conexión/cola, sorteos negativos o respuestas de armado rechazadas. También se retiran esos mensajes rutinarios anteriores de Information/Warning. Los errores reales y sus reportes se conservan.
- Cuando el sorteo de vuelo elige una falla: fase, delta de escenario, score/cap, cantidad de candidatas, suma lambda, `p_tick = 1 − exp(−Σλ × Δt/3600)`, azar real y selección ponderada. Solo para la elegida se detallan preset, severidad/ATA, desgaste y factor, base por hora, multiplicadores de severidad/perfil, peso del preset y lambda efectiva.
- Tierra: se registra únicamente la falla seleccionada tras cap y encolada con éxito, con probabilidad, azar Bernoulli real y desgaste/factores. No se registran sorteos negativos ni positivos que quedaron fuera del cap o de la cola; el resumen cuenta los encolados reales.
- Reservas exitosas, armado aceptado, persistencia, confirmación Active y resolución por piloto. Se trazan eventos persistidos, inicio de tregua crítica, envío de preparadas, pausa/desarme, restauración, descarte manual efectivo, cierre y compensaciones exitosas. Aceptar el armado no se presenta como confirmación de falla activa. El manager solo resume evaluaciones con envíos o cambios de estado.
- Desgaste normal y recuperación por ETA: cada incremento efectivo muestra horas, tasa por hora, previo, incremento calculado y efectivo tras clamp 0–100 y total; se confirma el guardado cuando hay deltas. Se omiten sistemas sin tasa/horas y cálculos sin incremento efectivo. El cálculo se identifica como pendiente hasta guardar; no cambia el cálculo ni el delta histórico existente.

Las trazas usan propiedades estructuradas y correlación por sesión. No consumen azar adicional: la política expone los sorteos ya realizados mediante diagnóstico; conserva su API previa y la misma secuencia de selección. Los logs Debug no pasan al modal de crash. Se conservan los registros operativos de acciones y errores. No se agregan eventos a la base ni nuevos motores de fallas, temporales o desgaste.

## Cierre de sesión consolidado, recuperación de huérfanas, modal de resultados y hora Zulu (2026-10-23)

**Errores inesperados (2026-10-24)**: los errores de persistencia/recuperación/desgaste y sus warnings con excepción pasan por el [reporte común de la aplicación](../ui/manejo-de-errores-y-arranque.md). El fatal de evaluación, la terminación fatal del manager y las compensaciones imposibles conservan la excepción y usan Critical: la app se cierra al confirmar el diagnóstico. Un fallo de persistencia compensado con desarme exitoso se reporta como recuperable. Cancelaciones, desconexiones y rechazos operativos no generan crash dialogs.

**Modal completado respecto del mock (2026-10-24)**: el resultado incluye su aeronave (id/matrícula/tipo), OFP (número/callsign/fuente), descarte y cronología propia. No depende de la selección de UI. La cabecera expone desgaste **medio por sistema** (promedio aritmético de deltas registrados); las fallas muestran ATA/sistema/grupo, fase traducida, horas UTC, fuente observada y duración. Rechazadas se presentan aparte y no suman en lanzadas/severidad. La cronología diferencia categorías por color; se registra la asociación `OFP_LOADED` al iniciar y un `FAILURE_NOT_RESOLVED` por falla al consolidar, sin duplicaciones; el cierre normal termina con `SESSION_STOPPED`. No se atribuyen reparaciones vía FMGC/ECAM sin evidencia. La tabla de desgaste tiene encabezados, barras 0–100 proporcionales y resalte ámbar desde 80 %. Borde rojo efectivo para no resueltas, textos ajustables y estados vacíos explícitos. «Ver historial completo» abre la aeronave de la sesión y la expande en el paso 2. Sin cambio de esquema; no se reconstruyen eventos históricos ausentes. El desgaste por fase, las temporales y las candidatas rechazadas del mock no añaden lógica ficticia al motor. Build Debug/Release sin errores/advertencias; 198/198 tests Release excluyendo logging por permisos, y render WPF aislado de tamaños normal/mínimo y estado vacío, sin errores de binding. Navegación manual completa e integración MSFS/Fenix: no verificadas.

**Corrección de acumulación (2026-10-23)**: el manager entrega al motor el `IAircraftSystemWearRepository` obligatorio del scope; antes omitía este argumento y el cierre salteaba el desgaste tanto en Debug como en Release. El repositorio completa con cero los sistemas del catálogo sin registro para la aeronave (nueva o existente), sin resetear los registros previos, y persiste las filas nuevas con la acumulación en `SaveAsync`. También cubre recuperación por ETA. El dashboard consulta sin tracking para mostrar lo guardado por el scope de sesión en lugar de valores antiguos del contexto de UI. Las horas aceleradas de los mocks sí generan desgaste; las fechas del historial siguen en UTC real. Sin cambio de esquema ni reparación retroactiva de sesiones cerradas. Regresiones verificadas por manager y SQLite; 194/194 tests excluyendo los 5 tests de logging limitados por permisos de AppData. No verificado manualmente en UI/MSFS/Fenix.

Implementación del mapa wayfinder `.scratch/cierre-sesion-realista/` (destino confirmado por el usuario; mock HTML aprobado para el modal).

- **Cierre único (§9)**: `FlightSessionService.FinalizeSessionAsync(sessionId, finishedAt, wearDeltas, ct)` es el único camino de cierre — pone `FinishedAt` (UTC), consolida las fallas no resueltas (`Preparing/Queued/Sent/Active` → `NotResolved` + `ResolutionSource.SessionEnded` + `ResolvedAtUtc` = cierre; `Rejected` quedan como están), persiste los deltas de desgaste y calcula el resultado después de consolidar (`ActiveAtEnd` = 0). El motor, en `StopSession`, desarma falla por falla en Fenix (best-effort, `DisarmActiveFailuresAsync`) antes de consolidar; el reset global del orquestador queda como red de seguridad. `RealisticSessionManager.StopAsync` captura `LastSessionResult` para el modal.
- **Recuperación de sesiones sin finalizar (§6/§13)**: al arrancar, si hay sesiones abiertas (`GetUnfinishedSessionsAsync`), `UnfinishedSessionsWindow` ofrece **Finalizar y ver resultados** (`FinalizeUnfinishedSessionAsync`: desgaste diferido con la **ETA del OFP** como fin del vuelo, sin clampear; sin OFP no aplica desgaste), **Descartar** (`DiscardSessionAsync`: `DiscardedAt` nuevo en `FlightSession`, queda en historial con badge «Descartada», **sin desgaste**) o **Dejar para después**. Limpieza Fenix best-effort. Tras finalizar se abre el modal.
- **Modal de resultados (§13)**: `SessionResultWindow` (mock aprobado) — cabecera (ruta/registro/perfil/inicio/fin/duración UTC), resumen por estado y severidad, cronología completa (`SessionEventLog`), fallas con su consolidación y **delta de desgaste por sistema** (nueva entidad `SessionSystemWearDelta` + `SystemWearDeltaDto`, persistida al cerrar para que el modal sea re-abrible). Se abre: tras «DETENER», al finalizar una huérfana y con el botón «Resultados» de cada sesión del historial (`ShowSessionResultFromHistoryCommand`).
- **Hora Zulu en todo (ticket UTC)**: reloj UTC de pared en la status bar de la MainWindow (`HH:mm:ss UTC`, junto a FASE); feed de fallas, historial, item de fallas, log en vivo y bloque OFP en UTC sin reconversión (el OFP ya viene en UTC de SimBrief; se eliminó la dualidad local/tooltip); logs en disco vía `UtcTimestampEnricher` + plantillas `{UtcTimestamp:...}` (aplicación y diagnóstico SimConnect).
- **Feed de fallas integrado al ojito (§14)**: el bloque independiente «Fallas de la sesión actual» desaparece; el feed (fallas enviadas y estados, tal cual hoy) y el lugar del razonamiento futuro viven dentro de «Pensamiento del motor», oculto por defecto y visible al expandir. El aviso «sin Fenix» y el botón de descartar preparadas quedan siempre visibles.
- **Esquema**: cambió el modelo (nuevos `FlightSession.DiscardedAt` y tabla `SessionSystemWearDeltas`) — borrar `realfenixfailures.db` local (`%AppData%\RealFenixFailures\`) para regenerar con `EnsureCreatedAsync`. Verificado: build Debug/Release sin warnings y 193/193 tests (incluye 3 nuevos: consolidación al cierre, descarte sin desgaste, desgaste por ETA). **No verificado en sesión real MSFS/Fenix.**

## Modelo probabilístico unificado, score de agravamiento y desgaste dinámico (2026-10-23)

**Preparación de pre-release (2026-10-24)**: el esquema actual queda consolidado en InitialRelease y el arranque usa MigrateAsync; ya no se regeneran bases al cambiar el modelo. Las referencias históricas a EnsureCreated/borrado de la base describen el flujo anterior. Ver transición y configuración Debug/Release en [desarrollo](../desarrollo.md). Los defaults del motor y de RealisticProfileCatalog reflejan el appsettings anterior, incluidos cadencia 5 s, ventana posarribo 0,5 h y perfil 7700 con tope 10 y tregua 600 s; las listas explícitas de perfiles mantienen prioridad.

**Ajuste vigente (2026-10-24)**: `WearMaxMultiplier` en la configuración de UI pasa a 1,5 / 2 / 2,5 para Bajo / Moderado / Alto; 7700 conserva su valor. Esto reduce la amplificación probabilística por desgaste, sin alterar las tasas de acumulación. El DTO/mapeo del catálogo Fenix conserva su identificador ATA y la persistencia lo asigna a `FenixFailureSystem.AtaId`, vínculo usado en el desgaste y el detalle de resultados. Sin cambio de esquema ni actualización retroactiva automática del catálogo existente. Calibración manual e integración real no verificadas.

**Implementado y committeado en `dev` (2026-10-23)**. Especificación fuente: [spec-modelo-probabilistico-y-desgaste.md](spec-modelo-probabilistico-y-desgaste.md) — con motivación (incidente de la sesión 12 del 2026-10-22: doble falla hidráulica en Moderate sin desgaste), fórmulas, criterios de aceptación y backlog. Esta sección reemplaza, para el disparo en vuelo y el seed de tierra, a «Tareas 1 y 2 (2026-10-19)» y a «Seed de fallas de tierra probabilístico individual (2026-10-22)» (sus knobs por perfil dejaron de existir); el resto de aquellas secciones (mecanismo de cola, gate, ciclo de vida) sigue vigente.

- **Fórmula unificada** (tierra y vuelo): `p = p_base × mult_severidad × mult_perfil × factor_desgaste × peso_preset`. Global en `FailureEngine:RealisticMode:Physics` (`FailureRatePerHourBase=0.01`, `GroundSeedBaseProbability=0.01`, `SeverityRateMultiplier` 1.0/0.4/0.15, `AggravationWeights` 1/2/4, `SameAtaAggravationExtra=1`). En tierra la p se interpreta one-shot (Bernoulli por candidata en gate); en vuelo como **λ en eventos/hora** integrada por `RollTick` con Δt real (`1−e^(−Σλ·Δt)`) — la cadencia queda **invariante al `EvaluationIntervalSeconds`** por construcción. Sorteo único ponderado por λ (antes: severidad por tasas → candidato por peso).
- **Score de agravamiento** (reemplaza a `MinorCap`/`MajorCap`/`CriticalCap` y a las tasas `*PerHour`): activas propias + reservas de cola pesan por severidad; `AggravationCap` por perfil (Low 3, Moderate 5, High 7, 7700 null) frena la sesión completa; filtrado **antes** del roll. **Sin bloqueo por sistema**: un 2º/3º fallo del mismo ATA sigue posible pero paga un extra (`SameAtaAggravationExtra` × fallas previas simultáneas de ese ATA) — improbable con cap, posible en 7700. Fallas sin ATA resuelto quedan fuera de la agrupación (sin extras).
- **Desgaste dinámico**: `WearFactor = 1 + (WearMaxMultiplier−1)×wear%/100` con `WearMaxMultiplier` por perfil (2.5/3/3.5/4 — reemplaza a los `*WearProbabilityGain`). **Acumulación nueva** (el desgaste era estático): columna `AircraftWearableSystem.WearPerFlightHour` (%/h, seed data: ENG/APU 1.0, HYD/FUEL/NEUM 0.6, ELEC/AVIONICS 0.3, resto 0.1); al cerrar sesión cada sistema suma `rate × horas de sesión` (StartedAt→fin, clamp 0–100) vía `IAircraftSystemWearRepository` (nuevo). Sin duración no acumula; el OFP no participa (solo usará la duración de sesión; ver spec §3.3/§5).
- **Config**: perfil de 11 a 6 knobs (`ProbabilityMultiplier`, `WearMaxMultiplier`, `AggravationCap`, `GroundFailuresCap`, `CriticalRespiteSeconds`, `RiskLevel`); `GroundFailuresCap` y el respiro tras Critical se conservan. El `appsettings.json` ya migado a la estructura nueva.
- **Verificación**: build Debug y Release **0 errores / 0 advertencias**; suite **177/177** (nuevos: fórmula unificada y seed con física global, WearFactor lineal, λ por severidad/perfil, score y extras ATA, invariancia al intervalo 5 s vs 30 s, reproducción del incidente de la sesión 12 a nivel engine — el 3º hidráulico jamás elegible en Moderate y posible pagando extra en 7700 —, flush de preparadas por score, acumulación de desgaste sobre SQLite real). **No verificado en sesión real MSFS/Fenix** (calibración de defaults pendiente de observación).
- **Pendiente**: recalibrar la PC de testing (su `appsettings` local sigue en el formato viejo; además hay que **borrar `realfenixfailures.db`** por el esquema nuevo). El overhaul de UI §8 quedó implementado el mismo día (ver sección siguiente).

## Interfaz §8 — panel Realistic alineado a la spec (2026-10-23)

Implementación de la sección §8 de la spec sobre `RealisticModePanel`/`RealisticModeViewModel` (detalle completo en el CHANGELOG de la fecha). Resumen del estado:

- **Bloque OFP ampliado (§8.1)**: con OFP cargado se muestran chips origen → destino, texto de ruta y filas Nº de vuelo, Callsign, Salida programada (local, UTC en tooltip), Llegada estimada, Duración estimada, Distancia, Aeronave del OFP y Fuente. Sin OFP: estado vacío; el estado de validación permanece visible. La distancia requiere `RouteDistanceNM` expuesto en `FlightPlanDto`.
- **Descarga de OFP (§8.3)**: botón outline accent de 32 px + botón chico rojo de descarte (icono de borrar) junto a él; ambos deshabilitados por el comando durante sesión activa, descarga u operación pendiente, con tooltips que lo explican. El descarte borra solo el OFP de la UI (sin OFP → hay que descargar otro antes de activar); no existe borrado en BD y no se agregó.
- **Requisitos (§8.4)**: la fila «OFP» ya no es texto estático (bug corregido: se bindea a `OfpRowDisplay`); check nuevo «Aeronave del OFP» con `OfpAircraftMatchDisplay`; orden MSFS · Datos aeronave · Aeronave del OFP · OFP · Perfil con Fenix como nota opcional al final; card-button de perfil (nombre + «Seleccionado»/«Sin perfil» + icono de edición) en lugar del ghost, sin la fila «Perfil seleccionado» redundante. Tras feedback visual: las filas OFP muestran estados cortos («✔ Cumplido»/«✘ Sin OFP»/«✘ Pendiente»/«✘ Vencido»/«✘ Inválido») y el mensaje completo de validación vive en el tooltip (`OfpValidationToolTip`) y en el bloque OFP de la izquierda.
- **Navegación (§8.5/§8.6)**: pastillas 1/2/3 convertidas en botones (`NavigateToStepCommand`, `CanNavigateToStep1/2/3`); hacia atrás libre, hacia adelante solo «Continuar»; deshabilitadas con motor activo. Sin botones «◀ Volver»; «Estado · Perfil» vive en la banda del encabezado junto a las pastillas.
- **Expectativa de fallas (§8.7)**: texto junto al desgaste medio del header colapsable, alimentado por `IRealisticExpectationService`/`RealisticExpectationService` (referencia 0.22 Σλ/h a multiplier 1.0 y desgaste 0; usa `RealisticProfileCatalog.Resolve` + `RealisticFailurePolicy.WearFactor`). Solo presentación.
- **Verificación**: build 0/0 y suite **185/185** (8 tests nuevos de la expectativa). **No verificado visualmente en la app ni en sesión real MSFS/Fenix.**

## Reloj de simulación escalable: mocks v2 para probar el motor sin volar (2026-10-23)

**Implementado en rama `feature/tiempo-escalado-mocks`** (base `dev`): permite reproducir y auditar el cronograma de fallas del modo Realistic sobre un vuelo completo sin volar horas ni debuggear en vivo.

- **Reloj compartido de escenario**: `ScaledTimeProvider` (`RealFenixFailures.Domain/Services/ScaledTimeProvider.cs`) es un `TimeProvider` cuyo `GetUtcNow` avanza a ×escala respecto del reloj interno. Registrado como singleton en `AddApplication` (junto a `TimeProvider` → el mismo singleton), lo comparten `MockSimConnectClient` (que ya inyectaba `TimeProvider`), el timer automático de `EngineOrchestrator` y el loop de evaluación de `RealisticSessionManager`. Con escala 1 el comportamiento es idéntico al tiempo real (Release sin mock incluido). Las fechas del historial usan UTC de pared.
- **Vuelo sintético realista**: el mock de SimConnect pasó de un keyframe de 5 minutos a un vuelo puerta-a-puerta de **~2h35** (9300 s) con ~28 keyframes deterministas: gate cold&dark → pushback (T+12) → arranque motores (T+14/15) → taxi → despegue (T+24:30) → ascenso → crucero FL350 con deriva (T+58–118) → descenso (T+118) → approach → aterrizaje (T+140:30) → taxi-in → shutdown (T+148) → cold&dark que se sostiene. El estado sigue siendo función pura del tiempo de escenario, por lo que cambiar la escala no altera el perfil, solo la velocidad.
- **Motor en tiempo de escenario**: `RealisticFailurePolicy` recibe `() => relojEscalado.GetUtcNow().UtcDateTime`, así que cadencia (λ), desgaste y tregua tras Critical se rigen por el tiempo de simulación. Los loops del motor ya no usan `PeriodicTimer` de pared: esperan tramos cortos (≤250 ms de pared) contra objetivos en tiempo de escenario, de modo que los cambios de escala en vivo y los saltos se reflejan en el próximo tick.
- **Knob**: `IEngineOrchestrator` expone `SimulatedTimeScale`, `SetSimulatedTimeScale(double)` y `AdvanceSimulatedTime(TimeSpan)`; la UI agrega el bloque «RELOJ DE SIMULACIÓN (DEBUG)» en el paso 3 del panel Realistic (1x/5x/15x/60x y saltos +10min/+30min/+1h, visible con motor activo). Con 60x, un vuelo de 2h30 corre en 2min30; con «Saltar» se va directo al descenso.
- **Trazabilidad**: los logs de disparo del modo Realistic agregan el marcador `a T+hh:mm:ss` (tiempo de escenario desde el inicio de sesión) y la escala vigente; el arranque/salto/parada de modo también quedan logueados con escenario.
- **Corrección UTC (2026-10-23)**: inicio, cierre, fallas enviadas (incluidas las preparadas), confirmaciones, resoluciones y sus DTO/eventos se registran en UTC de pared, igual que los eventos del detector. Antes se mezclaban ambos relojes y ordenar por fecha desplazaba las fallas respecto de las fases/motores. `SESSION_STARTED` usa el mismo instante que `FlightSession.StartedAt`. El desgaste conserva las horas de escenario desde el arranque del motor; `LastUpdatedAt` usa UTC de pared. No cambia el esquema ni se corrigen registros históricos ya guardados con fechas simuladas.
- **Mock de Fenix v2** (ver [docs/integraciones/fenix-api.md](../integraciones/fenix-api.md)): sirve el catálogo real `failures.json` con estado `failed` mutable y es seleccionable con `FenixApi:UseMock` (solo DEBUG; el repo lo deja en `true` para desarrollo sin Fenix).
- **Verificación**: build de solución y UI 0 errores / 0 advertencias; suite **190/190** (10 nuevos: cronograma del vuelo de 2h35, cold&dark final, reinicio de timeline, aceleración ×60 con cambio de escala y salto, y 4 unit tests de `ScaledTimeProvider`). **No verificado en la app corriendo ni en sesión real MSFS/Fenix**: falta probar el knob en la UI y una corrida completa a 60x en la PC de testing.

## Interfaz de preparación y sesión (2026-10-17)

El front de Realistic usa ahora tres pasos: (1) selección/registro de aeronave, (2) resumen y sesiones, (3) pantalla única de preparación y monitoreo. Esta sección describe **solo lo implementado en UI y validación de registro**; el motor y los datos vigentes se describen más abajo. El diseño futuro de la experiencia está en `diseno-objetivo.md` §14.

- **Aeronave**: tarjetas con matrícula, tipo, horas, vuelos, promedio simple del desgaste registrado por sistema o «Sin datos», y marca de sesión sin finalizar. `UserAircraftService.GetAllAsync` carga relaciones de desgaste y sistema. El formulario limita matrícula a 10 caracteres y ofrece un selector A319/A320/A321; `CreateAsync` valida el tipo y normaliza matrícula (`Trim` + mayúsculas) también en el servicio. No se cambió el esquema ni se crean/acumulan registros de desgaste nuevos.
- **Resumen**: permanecen las estadísticas, barras de desgaste e historial expandible. Se muestra «OFP: No registrado» para todas las sesiones actuales porque `FlightSessionDto` todavía no guarda OFP; no se presentan rutas inventadas ni se ofrece todavía finalizar una sesión anterior desde su tarjeta.
- **Preparación/monitoreo**: pantalla compartida con aeronave clicable para consultar desgaste y cabecera de perfil/estado. La fase y las conexiones se consultan en la barra de estado global, no se duplican en otra tarjeta; los requisitos sí indican SimConnect/Fenix/datos de aeronave/OFP/perfil. Dos tarjetas en paralelo muestran OFP/ruta y requisitos de forma compacta. El contenido central se desplaza sin barra nativa visible. Las fallas del feed muestran el estado confirmado contra Fenix (enviada → activa → resuelta, con color por estado; ya no «Enviada (sin confirmación)»); el ojo del pensamiento, plegado por defecto y sin datos ficticios, solo aparece durante sesión activa.
- **Selección de perfil**: el botón dentro de requisitos abre `RealisticRiskProfileWindow`, ventana modal oscura con cuatro perfiles expandibles y público «Ideal para pilotos que…». Cada uno separa diseño objetivo de comportamiento del motor actual; 7700 solo se consulta y no se puede seleccionar. Bajo/Moderado/Alto se preseleccionan según la elección previa; Cancelar la conserva y Confirmar aplica el perfil elegido. El cambio se bloquea mientras haya sesión activa.
- **Acciones disponibles y bloqueadas**: «Descargar OFP» queda habilitado y ejecuta el flujo real de descarga/validación/persistencia usando el **userid de SimBrief configurado en Ajustes** (`UserPreferencesService`; ya no se edita desde el modo Realistic); `OfpValidated` se alimenta del `FlightPlanDto` devuelto. «Pausar fallas» permanece deshabilitado con explicación. La confirmación VATSIM está cableada después de validar requisitos y antes de iniciar, pero **no se puede alcanzar todavía** (bloqueada por `CanActivate` que además exige Fenix conectado); explica que prevalece el controlador, que pausar fallas sería la acción apropiada cuando se implemente y que hoy debe detenerse y comprobarse el desarme en Fenix. Si el modo ya estaba activo, «Detener» sigue disponible; pide confirmación inline en vuelo y protege contra doble invocación mientras procesa. Se recomienda terminar en tierra, pero puede detenerse en vuelo. «Reanudar evaluación» conserva la semántica antigua tras pausa por desconexión; **no** desarma/rearma fallas. El botón «Detener» invoca el reset existente sin garantía de limpieza completa en Fenix real. La UI no agrega sonidos ni notificaciones de fallas al feed izquierdo.
- **Pendiente de integración**: persistencia de severidades (campo agregado al catálogo por @mathi, en curso); arranque sin Fenix y sin restricción de fase; los cuatro perfiles completos con topes/cadencias/temporales; pensamiento real del motor; pausa/desarme/restauración; resultados y recuperación de sesiones abiertas. Los tres perfiles seleccionables describen **solo el motor vigente** (tope global 3 y cooldown 60 s), no los topes objetivo de `diseno-objetivo.md` §5. **Estados de falla ya implementados** (ver sección "Ciclo de vida de fallas con confirmación por Fenix" más abajo): Sent→Active→ResolvedByPilot por lectura.
- **Verificación**: la compilación Debug y las pruebas de unidad automatizadas se consignan en `docs/CHANGELOG.md`. La app DEBUG arrancó desde la carpeta del proyecto y se navegó por automatización a Realista/paso 1; no se comprobó el paso 3 ni el modal porque no había aeronaves registradas. No se ha verificado visualmente la ventana a tamaño mínimo ni se ha ensayado esta UI en una sesión real MSFS/Fenix.

## Fase 1 — Fundamentos del esquema objetivo (2026-10-17)

Primera fase del backend del modo Realistic: configuración, esquema (migración `RealisticModeFlowSchema`) y catálogo de sistemas. La severidad por falla se completó el **2026-10-17** con los campos `severity` y `repairable` en las 384 fallas de `failures.json` (ver sección «Severidad de fallas del catálogo»); la **persistencia** en `FenixFailureDefinition` (Severity + CanBeRepaired) se implementó el **2026-10-19**.

- **Configuración** — nueva sección `FailureEngine:RealisticMode` en `FailureEngineSettings` y `appsettings.json`: `EvaluationIntervalSeconds=30`, `PreparedQueueLimit=5`, `GroundFailureEnabled`, `ThinkingLinesCap=50`, `Ofp{PreStartWindowHours=2, PostArrivalWindowHours=2}`, `Wear{AmplificationFactor=0.5, ThresholdNoticePct=80}`. Sin cadencias de disparo nuevas (calibración pendiente; los topes actuales del motor son los vigentes).
- **Esquema** — creado con `EnsureCreatedAsync()` desde el modelo (desarrollo; no hay migraciones; política en `docs/desarrollo.md`):
  - `FlightPlans` (tabla nueva): origen/destino/aerotipo, número/vuelo y callsign, offblock y aterrizaje estimado en UTC, duración, ruta, XML crudo, fuente (`Userid`/`Url`), fechas de descarga/validez, `ValidationState` y mensaje. `FlightSession` gana `FlightPlanId` (FK Restrict), `OriginIcao`, `DestinationIcao` y `ResultSnapshotJson`.
  - `TriggeredFailure` gana el ciclo de vida: `State` (default `Sent`), `FenixConfirmedAtUtc`, `ResolvedAtUtc`, `ResolvedPhase`, `ResolutionSource`, `CanBeTemporary`, `PreparedAtUtc`.
  - `FenixFailureSystem.AtaId` y `AircraftWearableSystem.AtaId` (join 1:1 por ATA) y `FenixFailureDefinition.CanBeTemporary`.
  - **Seed de sistemas ampliado de 8 a 19**, alineado a los 19 bloques ATA de `failures.json` (21..80): los Ids 1-8 se re-semantizan (antes Engine 1/2, HYD, GEAR, NAV, PNEU, APU, DOOR) y ya no existe "Engine 1/Engine 2" como sistemas independientes; se fusionan en ATA 70 (Power plant) y ATA 80 (Start faults). **Impacto**: datos de `AircraftSystemWear` de aeronaves existentes que apunten a Ids 1-8 quedan asociados a un sistema de distinto nombre; en la base real no había aeronaves con desgaste persistido al momento del cambio.
- **Enums aditivos** — `FailureLifecycleState` (Preparing/Queued/Sent/Active/ResolvedByPilot/AutoResolved/NotResolved/Rejected), `ResolutionSource` (EngineAuto/Pilot/SessionEnded), `ValidationState` (Pending/Valid/Invalid/Expired) y `SessionEventCategory` ampliado (Pause, ResumeOrReconnect, FailureStateChanged, WearUpdated, OfpLoaded, SessionRecovered = 9..14). Sin esquema.
- **Verificación** — esquema validado con `EnsureCreated()` sobre SQLite en memoria y en DB temporal de archivo (19 sistemas con AtaId 21..80, tabla `FlightPlans`, defaults del ciclo de vida); se borraron las bases locales y se eliminaron las migraciones previas (consolidación dev, 2026-10-17). Build Release sin advertencias y **88/88 tests** (3 nuevos: seed 19/AtaId, valores aditivos de `SessionEventCategory`, defaults de `RealisticMode`). El roundtrip a DB del ciclo de vida de `TriggeredFailure` quedó validado por el esquema; su prueba unitaria de entidad se deja para la fase de estados de falla.

## Implementado (verificado en código)

- **`RealisticSessionManager`** (`RealFenixFailures.Application/Services/RealisticSessionManager.cs`) — singleton; **responsable del ciclo de vida y el timer**: `StartNewSessionAsync`, `StopAsync`, `PauseAsync`, `ResumeAsync(ct, restoreFailures)`; `PeriodicTimer` de evaluación de **30 s** (`EvaluationIntervalSeconds`); despacha `FailureTriggered` y `SessionError` hacia la UI.
  - **Pausa que desarma (diseño objetivo §6)**: `PauseAsync` detiene el loop y **desarma en Fenix** cada falla activa/armada de la sesión (`DisarmFailureAsync` por falla); el motor sigue pensando en memoria; el piloto puede volar sin fallas. Fallas que no se desarman se registran en log (no aborta la pausa).
  - **Reanudar con restauración opcional**: `ResumeAsync(ct, restoreFailures=false)` re-arma las fallas en curso cuando `restoreFailures=true`, reconstruyendo la condición de armado desde la BD (`BuildArmRequestFrom`) vía `IFenixFailureApiDispatcher.ArmFailureAsync`. La reconstrucción es **best-effort**: si la condición no se puede reconstruir (campos string), la falla NO se re-arma y se registra en log. `RealisticModeViewModel.RestoreFailuresOnResume` (default true) alimenta este parámetro.
- **`RealisticSessionEngine`** (`RealFenixFailures.Application/Session/RealisticSessionEngine.cs`) — nueva instancia por sesión; **responsable de la evaluación**: `TryStartSession` (valida conexión Fenix+SimConnect y avión en gate con motores apagados), `EvaluateAsync` por tick, `TryTriggerFailureAsync`.
- **Reanudación manual desde la UI**: `RealisticModeViewModel` expone `IsSessionPaused` y `ResumeSessionCommand` (invoca `IRealisticSessionManager.ResumeAsync(CancellationToken.None)`, suscrito a su `PropertyChanged`); `RealisticModePanel.xaml` muestra el botón **"REANUDAR SESIÓN"** junto a la acción de detener, visible solo cuando la sesión está pausada. No hay auto-resume.
- **`RealisticSession`** (`RealFenixFailures.Application/Session/RealisticSession.cs`) — estado mutado por el engine: fase actual, datos del sim, fallas Fenix armadas/falladas, `LastTriggeredAtUtc`, presets disponibles, `ActiveFailureCount`.

#### Ciclo de vida de fallas con confirmación por Fenix

Implementado en `RealisticSessionEngine.ReconcileFailureLifecycleAsync` (por lectura, diseño objetivo §10). Cada tick (`EvaluateAsync`), después de `ProcessSimData` y antes de intentar disparar, compara las fallas persistidas en curso (repo `GetActiveForSessionAsync`: Preparing/Queued/Sent/Active) contra lo que Fenix reporta real:

| Transición | Condición leída de Fenix | Persistencia |
|---|---|---|
| `Sent` → `Active` | `failed=true` (confirmada por Fenix) | `FenixConfirmedAtUtc` + `State=Active` |
| `Active` → `ResolvedByPilot` | `failed=false` y no armada (el piloto la despejó) | `ResolvedAtUtc`, `ResolvedPhase`, `ResolutionSource=Pilot`, `State=ResolvedByPilot` |

`FlightSessionService.UpdateTriggeredFailureStateAsync` persiste la transición (firma: sesión, fenixFailureId, nuevo estado, fase, fuente, utcNow, ct) usando `TriggeredFailureRepository.GetActiveForUpdateAsync` (rastreo EF). No dispara sobre fallas ya resueltas (`State==newState` es no-op). `ResolutionSource` admite `Pilot`, `EngineAuto` y `SessionEnded`.

**Reacción del engine ante la confirmación (2026-10-19)**: por cada transición observada, la reconciliación (además de persistirla) registra el evento de sesión `FailureStateChanged` (códigos `FAILURE_CONFIRMED_ACTIVE` / `FAILURE_RESOLVED_BY_PILOT`; antes el enum 11 quedaba sin uso), lo devuelve en `EvaluationResult.StateChanges` y el manager lo despacha como `FailureStateChanged` hacia la UI. El feed «FALLAS DE LA SESIÓN ACTUAL» ya no queda en «Enviada (sin confirmación)»: cada entrada muestra su estado confirmado con color (diseño objetivo §14: enviada amarillo, activa Minor/Major naranja, activa Critical rojo, resuelta verde) y se actualiza cuando Fenix confirma la transición.

#### Resultados de sesión y recuperación (diseño objetivo §6/§13)

- **DTOs**: `ResultSummaryDto` (conteos por estado — `ActiveAtEnd`, `ResolvedByPilot`, `AutoResolved`, `Rejected` — y por severidad — `Minor/Major/Critical` — + `ResolvedCount`), `FailureResultDto` (detalle por falla: estado, fuente de resolución, fase, `ResolvedAtUtc`) y `SessionResultDto` (cabecera + resumen + fallas).
- **Servicio**: `IFlightSessionService.GetResultAsync(sessionId)` (resultado agregado; null si la sesión no existe), `GetUnfinishedSessionsAsync()` (sesiones abiertas, con OFP) y `FinalizeSessionAsync(sessionId, finishedAt)` (marca `FinishedAt` si estaba abierta y devuelve el resultado).
- **Historial**: `FlightSessionDto` gana `OriginIcao`, `DestinationIcao`, `OffBlockUtc`, `EstimatedArrivalUtc` (desde `FlightPlan`), `Result`, `DiscardedAt`. `FlightSessionItemViewModel` muestra la ruta del OFP en `OfpStatusDisplay` y el badge «Descartada» cuando corresponde.
- **Front (2026-10-23)**: modal de resultados (`SessionResultWindow`, mock aprobado) consumiendo `GetResultAsync`, apertura tras «DETENER» (`RealisticSessionManager.LastSessionResult`), re-apertura desde el historial y apertura al finalizar una huérfana; botón «Finalizar sesión» integrado en el diálogo de recuperación (`UnfinishedSessionsWindow`). Ver sección de 2026-10-23 arriba.

- **Consultas** — `ITriggeredFailureRepository` gana `GetActiveForSessionAsync` (no-tracking, por sesión) y `GetActiveForUpdateAsync` (rastreo, para transicionar). <span style="color:gray">(No hay UI nueva aún; el feed y el historial se alimentan de la sesión persistida.)</span>
- **`FlightEventDetector`** (singleton, transversal) — clasifica fase y eventos; fuente única de fase.
- **Persistencia del WIP**: `IFlightSessionService` crea/cierra sesión (`FlightSession` con `RiskLevel` y `UserAircraftId`), registra `TriggeredFailure` por falla disparada y persiste además **`SessionEventLog` append-only** durante las sesiones Realistic: `SESSION_STARTED`, `SESSION_STOPPED`, eventos de motores, cambios de fase, cruces de altitud y fallas disparadas (detalle en la sección "Historial de sesiones y totales derivados (2026-10-15)").
- **Provisión de presets realistas (seed)** — `PresetService.InitializeAsync` siembra por tipo de forma **condicional e independiente**: si no existe ningún preset `TrainingMode`, carga `training_presets.json` (embedded resource) vía `IPresetsLoader.GetTrainingPresetsAsync` y los inserta; después hace el mismo chequeo para `RealisticMode` con `realistic_presets.json` (`GetRealisticPresetsAsync`). La secuencia es siempre primero Training y luego Realistic; por tipo es idempotente (si ya existe alguno, no recarga ni reinserta). El catálogo realista (44 presets) queda en la base como candidatos y la sesión Realistic los consume desde el repositorio (`GetRealisticPresetsAsync` lee de la DB).
- **Arranque desde UI**: `EngineOrchestrator.StartRealisticModeAsync` → manager; requiere conexión previa.
- **Mock de SimConnect** en DEBUG (vuelo sintético de 5 min) y **mock de Fenix** en DEBUG.

#### Seed de fallas de tierra sin contaminar el tracking de EF (2026-10-19)

`RealisticSessionEngine.BuildGroundCandidatesAsync` completaba severidad y grupo de las definiciones de tierra leyendo el catálogo con un repo que usaba **AsNoTracking** (`FenixFailureDefinitionRepository.GetAllWithSystemsAsync`) y asignaba la navegación `def.FenixFailure.Group = entity.Group` sobre definiciones **ya trackeadas** del contexto de sesión (los presets se cargan con tracking). Eso envenenaba el tracking: el siguiente `SaveChanges` intentaba adjuntar el grafo desanclado y chocaba con las mismas claves ya trackeadas (`InvalidOperationException`, "cannot be tracked because another instance with the same key value"). Con el bug, las fallas de tierra se armaban en Fenix y se desarmaban por compensación pero **nunca se persistían**, y el stop de sesión (SESSION_STOPPED + `StopAsync`) fallaba igual dejando la sesión abierta en BD.

Fix de raíz aplicado: `GetAllWithSystemsAsync` ya **no usa AsNoTracking** — la consulta comparte identidad con el contexto de la sesión, así que asignar la navegación `Group` es seguro (misma instancia trackeada) y el seed conserva el bucket por sistema (`Group.SystemId`). Una alternativa previa (asignar solo `GroupId` + `Severity` sin navegación) se descartó porque dejaba `Group` null y colapsaba el seed de tierra a 1 sola falla ("Menos candidatos de tierra que el rango deseado: 1 de 5", verificado en corrida real 03:50). Cubierto por el test de integración `RealisticSessionEnginePersistenceTests` (engine + repositorios reales sobre SQLite en memoria; seed de tierra → flush → `TriggeredFailure` persistido con `State=Sent`).

Evolución del mismo día: el universo de candidatas de tierra **ya no sale de una whitelist configurable** — se elimina `GroundFailureIds` de `RealisticModeSettings`/`appsettings.json` (era una lista duplicada y desactualizable) —. `BuildGroundCandidatesAsync` ahora toma **todas las `FenixFailureDefinition` persistidas con severidad 1/2** (`Minor`/`Major`) de `GetAllWithSystemsAsync` y construye un `PresetFailureDefinition` sintético por cada una (`FenixFailure` = entidad persistida trackeada, `Probability = 1.0`, sin condiciones de armado); la sesión escoge desde esa lista por rango de riesgo y sistema distinto como antes. Al no mutar navegaciones ajenas, el seed deja de depender de la reconciliación `Group`/`Severity` y el tracking de `GetAllWithSystemsAsync` (misma identidad) basta para que `SaveChanges` no truene. El nombre del preset en la cola/historial pasa a ser el del **sistema** (`Group.System.Name`, p. ej. `ELEC`).

#### Seed unificado: DatabaseSeeder (2026-10-20)

El arranque siembra la base desde un **punto único de orquestación**, `DatabaseSeeder` (`Application/Services`): primero carga el catálogo de fallas (`IFailuresPersistenceService.LoadInitialFailuresAsync`, desde `failures.json` embebido) y después los presets (`IPresetService.InitializeAsync`, Training y Realistic desde sus JSON embebidos), reutilizando los servicios existentes. Implementa `IInitializerService` —el contrato que `App.OnStartup` resuelve desde DI—, así que el arranque no cambia. Los seeds estáticos del esquema (`PresetType` y los 19 `AircraftWearableSystem`) siguen en `SeedData.Apply` (`OnModelCreating`), fuera del seeder runtime. El WIP de aeronave de ejemplo `LV-SEED` con sesiones/fallas/desgaste se retiró: `SeedData` volvió a su alcance original y `FailuresPersistenceService` a su constructor de 2 parámetros. Todo dato semilla futuro se orquesta desde `DatabaseSeeder`, con la restricción de que las fallas Fenix (FK `TriggeredFailure.FenixFailureId`) solo existen después del paso del catálogo.

#### Seed debug temporal: aeronave de ejemplo con sesiones, fallas y desgaste (2026-10-20)

Para **desarrollo** (no cargar datos a mano cada vez que se regenera la base: borrar la `.db` local y `EnsureCreated` reconstruye el esquema vacío) se agrega un **seed temporal solo DEBUG**, `DebugSampleDataSeeder` (`Infrastructure/Services`, implementa `IDebugSampleDataSeeder` en `Application/Interfaces`). En una base recién creada siembra la aeronave de ejemplo **`LV-DBG`** (A320) con su historial, todo en **una única transacción**:

- 3 `FlightPlan` manuales (FK obligatoria de `FlightSession.FlightPlanId`).
- 4 `FlightSession`: 3 finalizadas (riesgo High/Moderate/Low) y una **abierta** (sin `FinishedAt`, no cuenta para totales de horas/vuelos).
- 4 `TriggeredFailure` con `FenixFailureId` del catálogo real (`F_FUEL_PUMP_LEFT_1`, `F_ENGINE_1`, `F_HYD_LEAK_GREEN`, `F_BRAKE_ABCU`) en estados distintos del ciclo de vida (ResolvedByPilot, NotResolved, AutoResolved, Sent).
- 6 `AircraftSystemWear` acumulado (Fuel 22 %, Hydraulic 31,5 %, Landing gear 18 %, APU 9 %, Power plant 15 %, Start faults 6 %).

**Integración con el pipeline de arranque**: en `Infrastructure/DependencyInjection.cs` se registra, bajo `#if DEBUG`, `IDebugSampleDataSeeder` y además `IInitializerService -> DebugSampleDataSeeder`, de modo que corre **después** de `DatabaseSeeder` (catálogo+presets). Para sostener varios initializers, `App.OnStartup` resuelve `IEnumerable<IInitializerService>` y los ejecuta en orden de registro (Application antes que Infrastructure ⇒ el catálogo garantiza la FK antes de las fallas de ejemplo). En **Release** el seed no se registra ni se resuelve: no siembra nada. Es idempotente (solo siembra si aún no existe `LV-DBG`) y temporal: cuando ya no se necesite, se quita el registro `#if DEBUG` y las dos clases, sin tocar `DatabaseSeeder`.

**Verificado** con repositorios y catálogo reales sobre SQLite de archivo recién creada (`EnsureCreated`): 384 definiciones de fallas, 1 aeronave, 3 planes, 4 sesiones, 4 `TriggeredFailure` (ninguna huérfana respecto del catálogo), 6 desgastes; re-ejecutar el seeder no duplica (idempotencia). Compila Debug y Release (Release queda sin seed). La suite de tests completa sigue bloqueada por el WIP ajeno de otra tarea (ver CHANGELOG).

### Constantes verificadas

| Constante | Valor | Lugar |
|---|---|---|
| Probabilidad base por tick | `BaseProbability = 0.10` | `RealisticSessionEngine` |
| Cooldown entre fallas | `MinCooldownSeconds = 60` | `RealisticSessionEngine` |
| Máx. fallas simultáneas | `MaxSimultaneousActiveFailures = 3` | `RealisticSessionEngine` |
| Multiplicador Low | 0.5 | `RiskProbabilityMultiplier` |
| Multiplicador Moderate | 1.0 | `RiskProbabilityMultiplier` |
| Multiplicador High | 1.75 | `RiskProbabilityMultiplier` |
| Intervalo de evaluación | 30 s | `RealisticSessionManager` |

Significado de `Probability` por definición (realistic): **peso relativo de selección** entre las fallas elegibles del preset; `FailureTrigger.TryTriggerFailure` elige por acumulación ponderada sobre la suma de pesos (`totalWeight`). No es la probabilidad de disparo de la sesión: esa es `BaseProbability` (0.10) × multiplicador de riesgo, aplicada por preset candidato en cada tick; el multiplicador de riesgo no modifica los `Probability` individuales.

`Difficulty` es metadato de clasificación (Easy=1, Medium=2, Hard=3, Insane=4); no está conectado al filtro de riesgo del modo Realistic (el filtro opera por severidad de la falla Fenix en `FailureRuleEvaluator`, no por `Difficulty` del preset).

Regla de severidad (`FailureRuleEvaluator`): Low → solo `Minor`; Moderate → `Minor`/`Major`; High → todas.

## Estabilización verificada (2026-10-14)

Correcciones aplicadas sobre el WIP (`031a9e7`) y **verificadas por build y tests** (aun **no committeadas**). Referencias con ruta/símbolo.

- **Validación antes de persistir sesión** — `RealisticSessionEngine.TryStartSession` (`Application/Session/RealisticSessionEngine.cs`) valida conexión y condiciones (gate con motores apagados) y solo después crea la `FlightSession`; si el inicio falla, `RealisticSessionManager.StartNewSessionAsync` descarta el scope y deja `SessionState == null` (sin estado residual).
- **Presets antes de persistir la sesión (sesión huérfana resuelta)** — `TryStartSession` carga `GetRealisticPresetsAsync` ANTES de `StartNewAsync` y asigna esa lista ya cargada a `Session.AvailablePresets`; si la carga de presets lanza, `StartNewAsync` no se llama y `Session` queda `null` (no queda sesión persistida huérfana).
- **Fallo fatal de evaluación sin estado activo residual** — `EvaluateAndExecuteAsync` devuelve una señal para que `RunEvaluationLoopAsync` termine; al salir por fallo fatal, el manager limpia engine/scope/timer/cts, deja `IsPaused=false` y notifica `SessionError`. El cierre se ejecuta dentro de la task del loop sin auto-esperarse (no invoca `StopAsync`/`StopLoopAsync`, que esperarían `_evaluationLoopTask`); un `OperationCanceledException` o la desconexión no se tratan como fallo fatal (la desconexión sigue siendo pausa reanudable).
- **Compensación distingue cancelación** — en `TryTriggerFailureAsync`, si `AddTriggeredFailureAsync` arroja `OperationCanceledException` se loguea distinto y se intenta **igual el desarme** (con `CancellationToken.None`) antes de propagar la cancelación; el resto de errores de persistencia mantienen la compensación existente.
- **Documentación de desarme sin campo `armed`** — [integraciones/fenix-api.md](../integraciones/fenix-api.md) ya no menciona un campo inexistente; la semántica real es `(Failed=false, Condition=null)` (una falla está armada cuando `FailureCondition != null` o `Failed == true`).
- **Null safety** — `TryStartSession` y la obtención de datos de sim comprueban `IsSuccess` y null antes de usar el valor (no hay accesos `Value!` sin verificar).
- **Scope por sesión** — `RealisticSessionManager` (singleton) crea un `IServiceScope` por sesión (`IServiceScopeFactory.CreateScope()`) y resuelve de ahí `IFlightSessionService`/`IPresetService`/`IFailureTrigger`; el scope se descarta en `StopAsync`. Ya no captura el servicio scoped en el ctor.
- **Pausa/reanudación idempotentes** — `PauseAsync`/`ResumeAsync` no actúan si el estado no cambia; `StopLoopAsync` cancela el `PeriodicTimer` y **espera** la tarea del loop antes de cerrar la sesión.
- **Detector thread-safe** — `FlightEventDetector` (`Application/Services/FlightEventDetector.cs`): estado mutable protegido con `lock (_sync)` en `ProcessSnapshot`, `Reset` y propiedades.
- **Landing alcanzable** — `DeterminePhase` devuelve `ComplexFlightPhaseEnum.Landing` al aterrizar desde una fase aérea y el evento de aterrizaje se emite.
- **StopFlightSimulation garantizado** — `EngineOrchestrator.StopCurrentModeAsync` invoca `IFlightSimulationController.StopFlightSimulation` en el bloque `finally` para cualquier modo (antes solo se llamaba al iniciar).
- **finishedAt respetado** — `FlightSessionService.StopAsync(sessionId, finishedAt, ct)` persiste ese `FinishedAt`; `RealisticSessionEngine.StopSession` cierra la sesión con `DateTime.UtcNow`.
- **Compensación de persistencia** — `RealisticSessionEngine.TryTriggerFailureAsync`: si `AddTriggeredFailureAsync` falla tras armar la falla, se intenta el **desarme individual** (`ISimulatorConnectionService.DisarmFailureAsync`); si ambos fallan, loguea `Critical` (la falla queda armada sin registrar). Detalle en [integraciones/fenix-api.md](../integraciones/fenix-api.md).
- **Feed de logs acotado** — `MainWindowViewModel.FlightEventDetector_FlightEventDetected` inserta en `LogEntries` vía `Dispatcher.InvokeAsync` y recorta el excedente sobre `MaxLogEntries = 200` (FIFO).
- **Warnings, arquitectura y paquetes** — nulabilidad, `ExecutionEngineException` obsoleta y variables sin uso corregidas; `PlatformTarget=x64` en UI, TestUnits e Integrations.SimConnect (resuelve MSB3270); dependencias actualizadas (resuelve NU1903).

**Verificación (2026-10-14)**

- `dotnet build RealFenixFailures.slnx -c Debug --no-restore`: 0 errores, 0 advertencias.
- `dotnet build RealFenixFailures.slnx -c Release --no-restore`: 0 errores, 0 advertencias.
- `dotnet test RealFenixFailures.TestUnits/RealFenixFailures.TestUnits.csproj --no-restore`: **34/34** (se suman los focalizados de esta revisión: presets-huérfanos, cierre fatal del manager, pausa por desconexión reanudable y compensación por cancelación).
- `dotnet list RealFenixFailures.slnx package --vulnerable --include-transitive`: ningún paquete vulnerable.
- Integración real MSFS/Fenix: **no verificada**.

## Estabilización de telemetría y fase (2026-10-15)

Correcciones aplicadas y **verificadas por build y tests** (49/49) en `dev`. Referencias con ruta/símbolo.

- **SimVars correctos para altitud y velocidad indicada** — el cliente real (`RealFenixFailures.Integrations.SimConnect/Services/SimConnectClient.cs`) solicita ahora `PLANE ALTITUDE` (altitud de la aeronave en pies, mapeada a `AltitudeMSL`) en lugar de `PRESSURE ALTITUDE`, y agrega `AIRSPEED INDICATED` (knots, FLOAT64) como telemetría adicional; el campo del struct se renombra a `PlaneAltitude` y `AirspeedIndicated` se propaga a `SimConnectAircraftState`, `SimulatorAircraftStateSnapshot`, clones, provider y logs de diagnóstico. El orden del struct y de `AddToDataDefinition` se mantiene sincronizado. **No** se usan `L:Vars` ni se captura combustible (sin base).
- **Redondeo solo en la representación de diagnóstico** — `SimulatorFlightDataProvider.GetAircraftRawData` redondea lat/lon a 5 decimales y N1/throttle a 1 decimal en el log de diagnóstico; los valores internos conservan su precisión (no se redondean/alteran arbitrariamente).
- **Flag `HasSimulatorData`** — `SimulatorAircraftStateSnapshot.HasSimulatorData` (default `false`, `init`, copiado en `Clone`) lo marca `SimulatorFlightDataProvider` desde `SimConnectAircraftState.IsConnected`. `SimConnectClient` limpia el último estado a `new()` y `_lastDataReceivedUtc = DateTime.MinValue` bajo el lock al iniciar una nueva conexión, al desconectar y al recibir Quit, para que no sobreviva una muestra obsoleta a la reconexión.
- **Detector sin datos** — `FlightEventDetector.ProcessSnapshot` con `HasSimulatorData=false` no procesa transiciones/eventos/altitud, limpia todo el tracking anterior (fase confirmada, debounce, altitud y motor/latch) y devuelve `Unknown`, sin emitir eventos; así una lectura corrupta o la ausencia de avión nunca dejan `Landing`/`Cruise` estancadas. (Antes conservaba la fase confirmada; ver "Detección de datos de aeronave coherentes y DuplicateID 29" más abajo, que corrige ese comportamiento.)
- **Bandas VS simétricas** — `DeterminePhase`: debajo de 5000 ft AGL, `VS>200` Climb, `VS<-200` Approach, resto Cruise; a/por encima de 5000 ft AGL, `VS>200` Climb, `VS<-200` Descent, resto Cruise (antes la banda `|VS| 100..200` por encima de 5000 clasificaba ascenso suave como Descent).
- **Debounce temporal por `ObservedAt`** — transiciones aéreas (Climb/Cruise/Descent/Approach) requieren lecturas consistentes: Cruise 60 s, Descent 45 s, Climb/Approach 10 s. La primera fase conocida se acepta al instante; las fases de tierra y Landing se aplican inmediatamente; dos candidatos distintos limpian/reinician el tiempo; al perder datos se borra el candidato. La cadencia de evaluación (5 s general / 30 s Realistic) no cambia: en modo 30 s la confirmación cae en el tick que supera el límite.
- **Carrera de despegue** — estando en tierra: `throttle>80` y `GS>50` ⇒ Takeoff; `GS>5` ⇒ Taxi (antes Takeoff requiría `GS<=5` con throttle alto y era inalcanzable en carrera).
- **Latch de aterrizaje** — tras Landing, mientras `_hasLandedThisFlight` y no se observa Takeoff, una lectura aérea espuria de nivel bajo no vuelve a Cruise: se mantiene la fase terrestre confirmada. El despegue real pasa por la fase Takeoff, que limpia el latch (se conserva el texto "Taxi final hacia el gate").
- **Engine sin datos** — `RealisticSessionEngine.EvaluateAsync` con `HasSimulatorData=false` no actualiza `RealisticSession` ni intenta disparar una falla en ese tick: loguea debug y devuelve `EvaluationResult.NoAction()`.

**Verificación (2026-10-15)**

- `dotnet build RealFenixFailures.slnx -c Debug`: 0 errores, 0 advertencias.
- `dotnet test RealFenixFailures.TestUnits/RealFenixFailures.TestUnits.csproj`: **49/49** (se suman: snapshot inválido inicial y entre muestras, VS +150 >5000 no es Descent, debounce Cruise 60 s y Descent 45 s, Landing inmediata, lectura aérea tras aterrizaje sin retorno a Cruise, Takeoff alcanzable, y engine con snapshot sin datos).
- Integración real MSFS/Fenix: el usuario verificó la publicación en producción y reportó que la telemetría corregida funciona; los logs de diagnóstico muestran datos coherentes. Esta verificación es manual y no la cubren los tests automatizados.

## Detección de datos de aeronave coherentes y DuplicateID 29 (2026-10-15)

Cambio implementado, verificado por build y tests y **comprobado por el usuario en producción**; corrección del problema indicado por los logs de diagnóstico compartidos. El usuario reporta que la telemetría corregida funciona correctamente. Referencias con ruta/símbolo.

- **DuplicateID 29 en `AddToDataDefinition`** — los logs mostraban `SimConnect Exception: 29` en cada conexión junto con telemetría corrupta (`Lon=6.8e38` o `0`, `AltMSL=0`, `IndAlt≈1e-313`, `AltAGL=-2147483648`, `Flaps=2147483647`, combustión con enteros enormes, N1/throttle absurdos) y el detector marcando `Landing` con `OnGround=false`. Contrastado con la doc oficial del SDK, el enum 29 es `SIMCONNECT_EXCEPTION_DUPLICATE_ID`, que se produce por IDs repetidos usados en `AddToDataDefinition`: el cliente pasaba `0` explícito como DatumID en las 18 definiciones. `SimConnectClient.RegisterDataDefinitions` ahora asigna IDs de datum únicos (`uint datumId` con `datumId++`, 0..17) sin alterar el orden del struct; el handler `OnRecvException` enriquece el mensaje con `dwException`, `dwSendID` y `dwIndex`.
- **Validador puro de paquetes** — `AircraftStateDataValidator.IsValid` (`RealFenixFailures.Integrations.SimConnect/Models/AircraftStateDataValidator.cs`): rangos físicos razonables (posición, altitudes MSL/AGL, heading, velocidades, VS, flaps, N1, throttle), booleanos int estrictos 0/1 (`SIM ON GROUND`, combustión), rechazo de NaN/Infinity/denormales y del frame completamente en cero; permite aeronave estacionada a altitud 0 cuando posición y otros campos contienen datos coherentes. Un paquete inválido conserva sus valores crudos en `SimConnectAircraftState` para diagnóstico pero con `IsConnected=false` (y por lo tanto `HasSimulatorData=false`). El callback actualiza `_lastDataReceivedUtc` por cualquier paquete recibido (sin bucle de reconexión cuando SimConnect responde sin avión) y sobrescribe siempre el último estado, de modo que una muestra inválida nunca deja la telemetría previa válida como actual. Sin warnings por paquete (100 ms).
- **`HasAircraftData` en el estado de conexión** — `ConnectionStatusDto` (Application) gana la propiedad init-only `HasAircraftData` (default `false`; se preserva el constructor posicional). `SimulatorConnectionService.GetConnectionStatusAsync` marca `HasAircraftData` y reporta fase `Disconnected` si el handle está abierto pero no hay datos coherentes (antes `Flying`/`OnGround` con telemetría basura). Se representa por separado: handle SimConnect conectado vs telemetría coherente disponible.
- **UI** — `MainWindowViewModel` sincroniza `HasAircraftData`; `SimConnectStatus` distingue `Disconnected`, `Connected · No aircraft data` y `Connected · Aircraft data` con punto gris/ámbar/verde (los bindings XAML existentes no cambian); `CanStartEngine` exige SimConnect + datos de aeronave + Fenix; `CurrentFlightPhaseDisplay` muestra `Disconnected`/`No aircraft data` sin presentar fases viejas. `RealisticModeViewModel` sincroniza la propiedad, exige `HasAircraftData` en `CanActivate`, agrega la razón "MSFS conectado pero sin datos de aeronave disponibles." cuando el handle está abierto sin telemetría y muestra `Disconnected`/`No aircraft data` en su indicador de fase.
- **Detector limpia tracking ante falta de datos** — `FlightEventDetector.ProcessSnapshot` con `HasSimulatorData=false` limpia fase, debounce, altitud y motor/latch y devuelve `Unknown` sin emitir eventos: una lectura corrupta o ausencia de avión nunca deja `Landing`/`Cruise` estancada. El motor realista ya salta ese tick (`EvaluateAsync` devuelve `NoAction` con `HasSimulatorData=false`); ese comportamiento se conserva.

**Verificación (2026-10-15 — detección de datos de aeronave y DuplicateID 29)**

- `dotnet build RealFenixFailures.slnx -c Debug`: 0 errores, 0 advertencias.
- `dotnet test RealFenixFailures.TestUnits/RealFenixFailures.TestUnits.csproj`: **69/69** (se suman: validador acepta aeronave estacionada y rechaza el frame vacío y el frame tipo log, fase `Disconnected` con handle conectado sin datos y `OnGround`/`Flying` solo con datos, detector a `Unknown` sin eventos ante snapshot inválido y sin eventos fantasma de fase al reanudar, y estado de conexión del simulador en el servicio).
- La detección de datos se describe como **datos coherentes disponibles** (validador físico), no como confirmación de un estado de menú específico del simulador; no se implementó ningún API de menú ni `L:Vars`. La telemetría de los logs posterior al cambio sigue pendiente de una nueva sesión real; builds y tests no reemplazan la prueba manual.

## Historial de sesiones y totales derivados (2026-10-15)

Cambio aplicado y verificado por builds focalizados y tests (55/55); verificación al final de la sección. Referencias con ruta/símbolo.

- **`SessionEventLog` append-only por sesión** — `IFlightSessionService` persiste la nueva entidad `SessionEventLog` (`Domain/Entities/SessionEventLog.cs`) durante las sesiones Realistic: `SESSION_STARTED` al iniciar, `SESSION_STOPPED` al cerrar (antes de `StopAsync`), eventos de motores, cambios de fase y cruces de altitud, y `FAILURE_TRIGGERED` por cada falla disparada. Cada evento guarda `OccurredAt`, categoría (`SessionEventCategory`), descripción, fase de vuelo y el `TriggeredFailureId` enlazado cuando corresponde. `EventCode` es un identificador semántico string (p. ej. `TAXI`, `LANDING`) que puede repetirse entre eventos/sesiones; la identidad de fila es `Id`.
- **Suscripción al detector solo durante la sesión activa** — `RealisticSessionEngine` (`Application/Session/RealisticSessionEngine.cs`) se suscribe a `FlightEventDetector.FlightEventDetected` al iniciar la sesión y se desuscribe al cerrarla; los eventos llegan **síncronos** a una cola `_pendingFlightEvents` (protegida con lock) y se **persisten async en cada tick** (`DrainFlightEventsAsync` dentro de `EvaluateAsync`). El `SESSION_STARTED` se registra antes de suscribirse; si su registro falla, la sesión se cierra y no queda activa.
- **Falla + evento en una unidad de trabajo** — `FlightSessionService.AddTriggeredFailureAsync` (`Application/Services/FlightSessionService.cs`) guarda el `TriggeredFailure` y su evento `FAILURE_TRIGGERED` enlazado y persiste ambos con un único `SaveChangesAsync` (atómico).
- **Snapshot histórico en `TriggeredFailure`** — la entidad (`Domain/Entities/TriggeredFailure.cs`) ahora copia al disparar el detalle completo de la condición del preset: nombre y severidad de la falla, `PresetId`/`PresetName`, `Probability`/`ProbabilityGroup`, `Ias`, altitudes (`AboveAltitude`/`BelowAltitude`), `Time` y `AfterEvent`/`AfterEventSeconds`. El historial no depende del catálogo mutable.
- **Totales de aeronave derivados** — `UserAircraft.TotalFlights` y `UserAircraft.TotalFlightHours` ya **no se almacenan** (columnas eliminadas por la migración): los DTOs (`UserAircraftService.ToDto`) los calculan desde las `FlightSessions` **finalizadas** de la aeronave (`FinishedAt != null`); las sesiones abiertas no cuentan.

**Verificación (2026-10-15 — historial y totales derivados)**

- `dotnet test RealFenixFailures.TestUnits/RealFenixFailures.TestUnits.csproj -c Debug --no-restore`: **55/55**.
- `dotnet build RealFenixFailures.slnx -c Debug --no-restore`: 0 errores, 0 advertencias.
- `dotnet build RealFenixFailures.slnx -c Release --no-restore`: 0 errores, 0 advertencias.
- La migración se aplicó sobre una base temporal y se verificaron `Up`, `Down` y una nueva aplicación de `Up`; la base real no se modificó. Sin verificación manual; las integraciones reales MSFS/Fenix siguen sin verificar (ver "Limitaciones restantes").

## En desarrollo

- Verificación manual de la integración real MSFS/Fenix (los mocks de DEBUG y los 29 tests no reemplazan la prueba real).

## Pendiente

- **`RemoveTemporaryFailuresAsync`** — eliminación de fallas temporales de emergencia (prevista en el diseño, no implementada).
- **Provisión de presets realistas — limitación del seed** — el sembrado es de una sola vía: si en la base ya existe **cualquier** preset `RealisticMode` (propio o de otra fuente), el catálogo no se mezcla ni se actualiza (no hay merge ni refresh del catálogo contra la DB). El catálogo tampoco define relación componente-desgaste (sigue pendiente).
- **Cálculo y persistencia de desgaste** — `AircraftSystemWear`/delta por vuelo, amplificación de probabilidad por desgaste (`probabilidadFinal = base × (1 + factor × wear/100)`); no implementado.
- **Incompatibilidades de fallas** — evitar fallas mutuamente excluyentes simultáneas; no implementado.
- **Configuración de constantes** — probabilidad base, cooldown, máx. simultáneas y multiplicadores están como `const` en código; se prevé moverlas a configuración.
- Completar el uso funcional del modelado de aeronaves y desgaste ya existente (`UserAircraft`, `AircraftWearableSystem`, `AircraftSystemWear`), especialmente la acumulación de **desgaste** por vuelo y su efecto sobre las probabilidades (los totales de horas/vuelos ya no son pendiente: se calculan desde las sesiones finalizadas, ver sección de 2026-10-15).
- **Auto-resume** — reanudar automáticamente la sesión tras una interrupción; no implementado.

## Decisiones abiertas

- Valores numéricos finales del factor de desgaste por nivel de riesgo.
- Mecanismo de notificación manager → UI (hoy eventos `EventHandler` + `INotifyPropertyChanged`).
- Vincular fallas Fenix ↔ sistema desgastable: FK directa vs tabla de mapeo.

## Investigación y diseño: la meta del modo Realistic y los bloques de pensamiento del engine (2026-10-16)

> **Sección de investigación/diseño, no implementada en código.** Explora cómo debería pensar el motor realista cuando el modo evolucione del sorteo ponderado actual a un proceso con narrativa propia. Todo lo de esta sección es propuesta: el comportamiento vigente es el de "Implementado" arriba y las restricciones de "Limitaciones restantes".

### Objetivo del modo realista

El modo Realistic debe producir fallas que se sientan como **consecuencia natural de la operación**, no como sorteo: el engine razona sobre contexto, estado de la aeronave, historial de la sesión, plausibilidad y objetivo para decidir *si* y *qué* falla; un piloto que mira hacia atrás debe poder decir "tenía sentido que pasara eso".

### Capas de pensamiento del engine

| Capa | Qué piensa | Qué necesita |
|---|---|---|
| Contexto de vuelo | Dónde está la aeronave y qué está haciendo: fase, altitud MSL/AGL, IAS, VS, motores, tierra/aire | Telemetría coherente del simulador (SimConnect) y fase confirmada del `FlightEventDetector` |
| Estado del avión | Qué tan sano está cada sistema y cómo se operó: desgaste, configuración, uso | Modelo de aeronave y desgaste de sistemas (hoy pendiente, ver "Pendiente") |
| Historial de sesión | Qué pasó ya en esta sesión/vuelo: fallas disparadas, cooldown, eventos | `SessionEventLog`, `TriggeredFailure` y reloj de cooldown |
| Plausibilidad | Si es creíble que falle *esto* en *este* momento: condiciones físicas, incompatibilidades, narrativa | Reglas de severidad y condiciones de los presets; incompatibilidades (pendiente) |
| Objetivo | Qué experiencia se está construyendo: tensión, aprendizaje, realismo | Perfil de riesgo del modo: **dirección, no veredicto** |

### Proceso mental del engine (flujo propuesto)

```mermaid
flowchart TD
    A["Origen de inputs"] --> B["Validación"]
    B -->|"Datos coherentes"| C["Construcción de contexto"]
    B -->|"Faltan datos coherentes"| NA["NoAction"]
    C --> D["Filtro de elegibilidad"]
    D --> E["Ponderación"]
    E --> F["Decisión"]
    F --> G["Ejecución"]
    G --> H["Persistencia / feedback"]
    H -->|"Siguiente tick"| I["Replanificación"]
    NA -->|"Siguiente tick"| A
    I -->|"Siguiente tick"| A
```

`Origen de inputs → Validación → Construcción de contexto → Filtro de elegibilidad → Ponderación → Decisión → Ejecución → Persistencia/feedback → Replanificación`

Flujo principal por fases: cada fase consume lo que produce la anterior. En la tabla, "consume" incluye tanto entradas externas como lo dejado por la fase previa, y "produce" es lo que queda para la siguiente.

| Paso | Qué hace | Consume | Produce |
|---|---|---|---|
| **1. Origen de inputs** | Reunir todo lo que llega para el tick | Telemetría, conexión Fenix + SimConnect, presets, historial de la sesión, OFP, desgaste de aeronave, preferencias del usuario | Paquete bruto de inputs del tick |
| **2. Validación** | Comprobar que hay datos mínimos coherentes y permisos para evaluar | Telemetría coherente (`HasSimulatorData`), conexión Fenix + SimConnect, presets cargados | Tick válido, o `NoAction` como salida directa (no se entra al resto del flujo) |
| **3. Construcción de contexto** | Interpretar dónde está la aeronave, qué está haciendo y si la realidad sigue el plan | Tick validado, fase confirmada del detector, historial de la sesión, OFP como planificación previa | Snapshot de contexto interpretado: fase, ventanas esperadas vs reales y estado del avión |
| **4. Filtro de elegibilidad** | Descartar lo imposible o increíble para la situación actual | Contexto, catálogo/presets, reglas de severidad y condiciones, plan de vuelo (ventanas) | Lista de candidatos elegibles para el tick |
| **5. Ponderación** | Calcular el peso final de cada candidato: plausibilidad (contexto + historial + objetivo) más `Probability` del preset | Candidatos, historial, desgaste (pendiente), objetivo/preferencias del modo | Pesos finales de selección por candidato |
| **6. Decisión** | Aplicar la probabilidad de disparo (hoy `BaseProbability` × multiplicador de riesgo) con cooldown y máx. simultáneas, y elegir por acumulación ponderada | Pesos, constantes del modo, estado de cooldown y fallas activas | Decisión del tick: **armar**, **posponer** o **no armar** |
| **7. Ejecución** | Materializar la decisión en el simulador | Decisión, API Fenix | Falla armada o desarmada en Fenix |
| **8. Persistencia/feedback** | Registrar la falla y el evento de sesión como unidad de trabajo y actualizar el estado de la sesión | Falla ejecutada, sesión activa, historial | `TriggeredFailure` + `SessionEventLog` (atómico) y estado de sesión actualizado |
| **9. Replanificación** | Cerrar el tick: qué queda descartado para la sesión y qué se vuelve plausible después | Historial actualizado, contexto del tick | Narrativa de la sesión actualizada, que vuelve a entrar al siguiente tick vía historial |

La cadena conceptual anterior (`Observar → Enmarcar → Seleccionar → Ponderar → Decidir → Actuar y registrar → Re-planificar`) se conserva como resumen: **Observar** cubre el origen de inputs y la validación; **Enmarcar** es la construcción de contexto; **Seleccionar candidatos** es el filtro de elegibilidad; **Ponderar** y **Decidir** mapean 1:1 a ponderación y decisión; **Actuar y registrar** une ejecución + persistencia; **Re-planificar** es la replanificación.

### Entradas y salidas del motor (diagrama de diseño)

**Tabla de inputs**, agrupados por categoría. La columna "Oblig." marca si el motor necesita el input para funcionar (**obligatorio**) o si solo lo enriquece cuando está disponible (opcional).

| Categoría | Input | Oblig. | Rol |
|---|---|---|---|
| Simulador | Telemetría (fase, altitud MSL/AGL, IAS, GS, VS, heading, motores, tierra/aire, N1/throttle, flaps) | **Obligatorio** | Contexto real del tick; sin datos coherentes el tick se salta (`NoAction`) |
| Simulador | Disponibilidad de datos coherentes (`HasSimulatorData`) | **Obligatorio** | Puerta de entrada de la evaluación: solo se procesa telemetría validada por rangos físicos |
| Simulador | Conexión Fenix + SimConnect | **Obligatorio** | Habilita/inhabilita evaluación y armado |
| Aeronave | Modelo de la aeronave (`UserAircraft`, sistemas asociados) | Opcional | Contexto de qué sistemas existen y su historial de uso (pendiente de conexión al motor) |
| Aeronave | Desgaste de sistemas (`AircraftSystemWear`, delta por vuelo) | Opcional | Factor de plausibilidad por sistema y amplificación de probabilidad (`base × (1 + factor × wear/100)`); pendiente de implementación |
| Aeronave | Configuración y uso en vuelo (flaps, tiempos de operación) | Opcional | Operación por sistema, hoy solo parcial vía telemetría |
| Sesión/historial | Historial de la sesión (`SessionEventLog`, `TriggeredFailure`) | **Obligatorio** | Memoria operativa: qué pasó, qué no se repite, narrativa |
| Sesión/historial | Estado de la sesión (fase actual, `ActiveFailureCount`, `LastTriggeredAtUtc`, presets disponibles) | **Obligatorio** | Estado mutado por el engine entre ticks, reentrante a cada evaluación |
| Sesión/historial | Reloj de cooldown y máx. fallas simultáneas | **Obligatorio** | Restricciones de la decisión (hoy constantes; pendiente configuración) |
| Plan de vuelo | OFP / flightplan (SimBrief: flight log, fuel, pesajes, TLR, routing, weather) | Opcional | Planificación previa: ventanas esperadas de fase/altitud/tiempo que orientan la selección de candidatos; **no es oráculo** |
| Plan de vuelo | Plan vigente y desvíos en vuelo (re-consulta) | Opcional | Ciclo de vida del plan en cambios de ruta (abierta, ver preguntas de diseño 2 y 4) |
| Entorno | Clima (METAR/TAF de origen, destino y alterno) | Opcional | Plausibilidad climática; el motor hoy no tiene telemetría de clima |
| Entorno | Condiciones de aeropuerto (NOTAMs, infraestructura) | Opcional | Fuente potencial de escenarios; texto libre difícil de correlacionar, no prioritario ahora |
| Reglas/configuración | Catálogo / presets realistas | **Obligatorio** | Define candidatos por fase y pesos de selección |
| Reglas/configuración | Reglas de severidad y condiciones de los presets (altitud, IAS, tiempo, evento previo) | **Obligatorio** | Filtro físico/narrativo de candidatos |
| Reglas/configuración | Constantes del modo (probabilidad base, cooldown, máx. simultáneas, multiplicadores de riesgo) | **Obligatorio** | Probabilidad y límites de disparo del tick (hoy `const`; pendiente configuración) |
| Reglas/configuración | Incompatibilidades entre fallas | Opcional | Evita fallas mutuamente excluyentes simultáneas (pendiente) |
| Manual user input | Preferencias de riesgo del usuario | Opcional | Intensidad buscada (hoy los multiplicadores de riesgo) |
| Manual user input | Selección de aeronave/presets al iniciar la sesión | Opcional | Define el alcance de candidatos de la sesión |
| Manual user input | Pausa/reanudación manual de la sesión | Opcional | Control de la ventana de evaluación (hoy no hay auto-resume) |

**Tabla de outputs**, separados por tipo de salida.

| Tipo de salida | Elemento | Qué contiene / hacia dónde va |
|---|---|---|
| **Operativa** | Decisión del tick (armar / posponer / no armar) | Resultado de la fase de decisión; alimenta la ejecución y la UI en tiempo real |
| **Operativa** | Comando de armado/desarme | Decisión materializada en la API Fenix (una falla armada o desarmada en el simulador) |
| **Persistencia** | `TriggeredFailure` (snapshot de la condición) | Registro durable de la falla disparada con el detalle del preset/condición al momento del disparo |
| **Persistencia** | `SessionEventLog` (append-only) | Trazabilidad de la sesión: inicio/fin, motores, fases, altitud y fallas; enlazado a la `TriggeredFailure` cuando corresponde |
| **Persistencia** | Estado de sesión actualizado | Fase, `ActiveFailureCount`, `LastTriggeredAtUtc`, cooldown — entrada del siguiente tick |
| **Explicación/diagnóstico** | Explicación de la decisión (piloto) | Por qué falló esto y por qué ahora: candidatos, plausibilidad y reglas que llevaron a la decisión (pendiente de diseño, ver preguntas 1 y 5) |
| **Explicación/diagnóstico** | Log / trazabilidad de desarrollo | Qué se vio, qué se descartó y por qué en cada fase; soporte a calibración y depuración (hoy logs de diagnóstico) |

### Relación entre inputs y outputs

- **Elegibilidad** — qué candidatos entran a la selección: reglas de severidad y condiciones de los presets, telemetría (fase, altitud, IAS, tiempo), historial (cooldown, no repetir, máx. simultáneas), plan de vuelo (ventanas esperadas) e incompatibilidades (pendiente). Si el tick no pasa la validación, aquí no llega nada.
- **Ponderación** — cuánto pesa cada candidato elegible: `Probability` del preset (peso de selección relativo), plausibilidad calculada (contexto + historial + objetivo), desgaste de aeronave (pendiente) y objetivo/preferencias del modo.
- **Decisión/ejecución** — las constantes del modo (probabilidad de disparo, cooldown, máx. simultáneas) y la conexión Fenix determinan si la salida operativa es armar, posponer o no armar.

- **Explicación** — qué se le cuenta al piloto y al desarrollador: lo descartado en el filtro y la ponderación (por qué *no*), y la decisión con su trazabilidad (por qué *sí* y por qué ahora). La explicación es una salida derivada de las fases 4, 5 y 6, no un input.

### Cómo debe pensar el motor: estados internos y política de decisión

Capa de planeamiento conceptual complementaria al flujo de "Proceso mental del engine": el flujo dice **qué se hace** en cada fase; esta capa dice **cómo piensa** el motor — qué pregunta se plantea en cada estado, qué recuerda de la sesión, con qué criterio decide y qué debe poder explicar. Sigue siendo diseño conceptual, no implementación.

**Estados internos**

| Estado | Pregunta que resuelve | Qué entra | Qué sale |
|---|---|---|---|
| **Observar / recolectar** | ¿Qué llega en este tick? | Telemetría, conexión Fenix + SimConnect, presets, historial de la sesión, OFP, preferencias | Paquete bruto de inputs del tick |
| **Validar** | ¿Hay datos mínimos coherentes y permiso para evaluar? | Paquete bruto, `HasSimulatorData`, conexiones | Tick válido, o `NoAction` como salida directa |
| **Contextualizar** | ¿Dónde está la aeronave y qué significa este momento? | Tick validado, fase confirmada del detector, historial, OFP | Snapshot de contexto interpretado: fase, ventanas esperadas vs reales, estado del avión |
| **Evaluar elegibilidad** | ¿Qué candidatos son posibles y creíbles aquí? | Contexto, presets, reglas de severidad y condiciones, ventanas del OFP | Lista de candidatos elegibles |
| **Ponderar** | ¿Cuánto pesa cada candidato? | Candidatos, historial, desgaste (pendiente), objetivo del modo | Pesos finales de selección por candidato |
| **Decidir** | ¿Qué hago este tick: armar, posponer o no armar? | Pesos, constantes del modo, cooldown, máx. simultáneas | Decisión del tick |
| **Ejecutar** | ¿Cómo materializo la decisión? | Decisión, API Fenix | Falla armada o desarmada |
| **Registrar** | ¿Qué dejo asentado de este tick? | Falla ejecutada, sesión activa, historial | `TriggeredFailure` + `SessionEventLog` (atómico), estado de sesión actualizado |
| **Aprender / replanificar** | ¿Qué cambió para los próximos ticks? | Historial actualizado, memoria del tick, contexto | Narrativa de sesión y ventanas revisadas, que vuelven a entrar por memoria/historial |

Los estados mapean 1:1 con las fases del flujo existente (Observar = fases 1–2, Contextualizar = 3, Evaluar elegibilidad = 4, Ponderar = 5, Decidir = 6, Ejecutar = 7, Registrar = 8, Aprender = 9): **no reemplazan el flujo, lo leen desde el ángulo de la pregunta interna**. A diferencia del flujo, no todos son pasos secuenciales de un tick: la **memoria** y el **aprendizaje** son transversales — se leen en varios estados y se actualizan al cerrar el tick.

**Memoria del motor**

Lo que el motor recuerda **durante la sesión** (memoria operativa, se reconstruye al iniciar):

- **Fallas disparadas** — cuáles, cuándo y en qué contexto (fase, altitud, condición), para la narrativa y el "no repetir".
- **Candidatos descartados y por qué** — lo que el filtro y la ponderación dejaron fuera y el motivo, para no reevaluar absurdos tick a tick y para poder explicar el "por qué *no*".
- **Cooldowns y fallas activas** — reloj de espera entre disparos y cuenta de simultáneas.
- **Desvíos del OFP** — en qué se apartó la realidad del plan (retrasos, esperas, cambios de ruta) y qué ventanas quedaron vencidas.
- **Contexto reciente** — fase confirmada, transiciones y eventos cercanos que dan continuidad a la lectura del momento.

Lo que **no** debería arrastrar entre sesiones: toda la memoria operativa expira al cerrar la sesión. Solo sobreviven los **datos persistentes** (`TriggeredFailure` y `SessionEventLog` como historial durable, y a futuro el desgaste de aeronave), y de ellos la sesión nueva reconstruye su punto de partida: qué falló antes, cuánto desgaste acumula cada sistema, qué perfil de riesgo opera. Los candidatos descartados, cooldowns y contexto de una sesión vieja no dicen nada de la nueva y no deben contaminarla.

**Política de decisión**

Criterio conceptual de cuándo corresponde cada salida:

- **Armar** — el tick valida, hay al menos un candidato elegible, la probabilidad del tick cae a favor, se respetan cooldown y máx. simultáneas, y la falla es plausible para el momento (contexto + historial + objetivo). Es la salida **excepcional**: la operación normal del motor es no armar.
- **Posponer** — la falla es plausible **pero no ahora**: sus condiciones aún no maduran (fase, altitud, IAS, tiempo del preset; una ventana del OFP que llegará más adelante) o el cooldown está por vencer. Posponer **no descarta**: el candidato permanece vivo para los próximos ticks, con su memoria intacta.
- **No armar** — el tick no valida (`NoAction`), no quedan candidatos elegibles, la probabilidad del tick no cae, o la falla contradice la narrativa/objetivo. Es la salida por defecto y la menos costosa: no consume narrativa ni memoria.

Influencia del objetivo y la plausibilidad: el objetivo del modo (tensión, realismo, aprendizaje) es **dirección, no veredicto** — inclina la selección entre candidatos igualmente plausibles y fija qué tan agresiva es la ventana de probabilidad, pero nunca ordena una falla increíble. La plausibilidad decide dentro de esa dirección: una falla posible pero increíble para el momento pesa menos que una cuyas condiciones físicas y narrativas están a favor.

Cómo evitar repeticiones absurdas: cooldown y máx. simultáneas son la restricción mínima; sobre esa base, la memoria de sesión debería **no repetir el mismo sistema/falla sin justificación**, **escalar severidad o cambiar de tipo** cuando un sistema ya falló (dándole tiempo de recuperación narrativa) y **descartar lo contradictorio** (p. ej., fallar presurización en tierra con motores apagados) registrando el motivo para no reevaluarlo cada tick. Heredando la regla de "Planificación previa vs reevaluación en tiempo real": *ante conflicto entre lo posible y lo creíble, manda lo creíble* (ver pregunta de diseño 3).

**Explicabilidad**

El motor debe poder contestar en lenguaje natural sobre **cada decisión**:

- **¿Qué decidió?** — armar, posponer o no armar (y, en su caso, cuál falla).
- **¿Por qué ahora?** — qué condiciones del contexto/telemetría y qué ventana del OFP hicieron válido el momento.
- **¿Por qué esta y no otra?** — qué pesó en la ponderación: plausibilidad, historial, objetivo, `Probability`.
- **¿Por qué no?** — qué se descartó en elegibilidad y ponderación, y con qué motivo.
- **¿Qué viene después?** — cuándo puede volver a ser plausible (cooldown, fase siguiente, condiciones por madurar).

Estos son los niveles de la salida "Explicación/diagnóstico" de la tabla de outputs: al **piloto** le basta el qué y el por qué ahora en una línea narrativa; al **desarrollador**, la trazabilidad por fase (qué se vio, qué se descartó y por qué) para calibrar sin cajas cerradas (ver preguntas de diseño 1 y 5).

**Conexión con el flujo existente**

```
[tick 30 s] → Observar → Validar → Contextualizar → Elegibilidad → Ponderar → Decidir → Ejecutar → Registrar
               (1)        (2)        (3)              (4)           (5)        (6)        (7)         (8)
                               │
                               └── faltan datos coherentes → NoAction → siguiente tick

Registrar → Aprender / replanificar (9) → siguiente tick (que vuelve a Observar con memoria + historial actualizados)
```

Los estados internos siguen el mismo orden y las mismas salidas que el flujo de "Proceso mental del engine" (`NoAction`, decisión del tick, persistencia); el ciclo se cierra cuando **Registrar → Aprender / replanificar** devuelve la memoria y el historial del tick al siguiente ciclo, que vuelve a **Observar**. El diagrama Mermaid y la tabla de fases existentes no se reemplazan: esta es la lectura de "cómo piensa" sobre ese mismo flujo.

### Planificación previa vs reevaluación en tiempo real

El OFP/flightplan describe el vuelo **esperado** (fases, altitudes, tiempos, puntos) y sirve como planificación previa: permite anticipar ventanas plausibles ("despegue entre T+5 y T+10", "crucero a FL350") y descartar temprano escenarios que el plan no contempla. Es una **entrada de orientación para la selección de candidatos**, no una fuente de verdad sobre lo que está pasando.

Pero el OFP **orienta, no manda**: cada decisión se re-evalúa contra el contexto real de cada tick (fase del detector, telemetría, estado del avión). Si la realidad se desvía del plan (retraso, espera en tierra, desvío, motores apagados), manda el hecho observado y el plan queda degradado a referencia. Regla propuesta: *el OFP declara la intención; la telemetría declara el hecho; el historial declara la memoria; el objetivo declara la dirección; ante conflicto, manda el hecho*.

### SimBrief / OFP como fuente de planificación (ejemplo de respuesta real)

Análisis de una **respuesta real de SimBrief** (OFP/flightplan) tomada de la URL pública `https://www.simbrief.com/api/xml.fetcher.php?username=R3M1X0101`, como ejemplo concreto de los datos que devuelve ese servicio y de su valor potencial como entrada de planificación para el modo Realistic. Complementa la subsección anterior ("el OFP orienta, no manda") documentando **qué contiene una respuesta real**, no cómo se integraría.

**Bloques que devuelve la respuesta**

- **Encabezado OFP** — identidad del vuelo: callsign, fecha, ruta (p. ej. SBGL-SUMU), tipo/aeronave, registro, hora de release, ATC callsign, viento medio, etc.
- **Pesajes** — MTOW, LAW y ZFW máximos y estimados (en el ejemplo: TOW 60309 / LAW 54513 / ZFW 51912).
- **Fuel** — TRIP, CONT 5%, ALTN, FINRES, MINIMUM T/OFF FUEL, TAXI y BLOCK FUEL (8627 en el ejemplo).
- **Routing** — camino de la ruta, p. ej. `SBGL/10 TIVRO1A BITAK UN857 NELOX UM424 TOLEP UM661 TOSIB DCT SUMU/19`.
- **Flight log** — tabla por waypoint/tramo con columnas de performance/posición (FL, MN, WIND, OAT, EFOB, TAS, GS, DIS, RDIS, EET, ETO, LAT/LONG, etc.) y combustible estimado a lo largo de la ruta.
- **Weather** — METAR/TAF de origen, destino y alterno.
- **TLR (takeoff/landing report)** — pista, temperatura, viento, QNH, pesos, flaps, limitaciones y remarks de despegue/aterrizaje.
- **ATC flight plan** — FPL con equipment, DEP/ARR, ruta, PBN, DOF, REG, EET, SEL, CODE, OPR y RMK.
- **NOTAMs** — avisos breves para los aeropuertos de la operación.

**Qué aporta cada bloque**

| Bloque | Qué aporta | Utilidad potencial para el motor |
|---|---|---|
| Encabezado OFP | Identidad del vuelo (callsign, fecha, ruta, registro, release) | Contexto operacional de la sesión; incidencia directa baja en la selección de fallas |
| Pesajes | MTOW/LAW/ZFW y pesos estimados | Umbrales físicos y márgenes de performance como contexto de plausibilidad |
| Fuel | Combustible por tramo (trip, contingencias, alterno, final, mínimo, taxi, bloque) | Ventanas esperadas de autonomía/reserva; contexto para fallas de combustible |
| Routing | Camino de la ruta (SID/STAR, airways, DCT, pistas) | Geometría del plan: relación entre fases, puntos y el flight log |
| Flight log | Perfil esperado por waypoint: FL, viento, OAT, EFOB, TAS/GS, tiempos y distancias | **La más rica para el motor**: ventanas esperadas de fase/altitud/tiempo y evolución del combustible |
| Weather | METAR/TAF de origen, destino y alterno | Plausibilidad climática (limitada: el motor hoy no tiene telemetría de clima) |
| TLR | Perfil esperado de despegue/aterrizaje: pista, viento, QNH, pesos, flaps, límites | Contexto esperado de las fases terrestres (Takeoff/Landing) |
| ATC flight plan | Plan de vuelo ATC: equipment, PBN, DOF, REG, EET, SEL, CODE, OPR, RMK | Cross-check operacional; secundario para la selección de fallas |
| NOTAMs | Avisos breves por aeropuerto (infraestructura, servicios) | Fuente potencial de escenarios; texto libre difícil de correlacionar automáticamente |

**Valor por prioridad para el motor**

| Categoría | Bloques | Razón |
|---|---|---|
| **Muy útil** | Flight log, Fuel, Pesajes, TLR | Definen perfiles esperados (fases, altitudes, tiempos, autonomía) y umbrales físicos que orientan directamente la plausibilidad de candidatos |
| **Útil pero secundario** | Routing, Weather, Encabezado OFP, ATC flight plan | Dan contexto operacional y estructural, pero con incidencia indirecta o dependiente de datos que el motor hoy no tiene (p. ej. clima) |
| **No prioritario ahora** | NOTAMs | Texto libre difícil de parsear/correlacionar con valor inmediato para la selección de fallas |

**Nota de alcance** — El análisis del uso de estos datos **como input al comenzar el modo realista** (integración concreta, parsing, carga al inicio de la sesión) queda **fuera de alcance** de esta investigación: aquí solo se documenta qué devuelve la respuesta y su valor potencial.

### Preguntas de diseño abiertas

1. ¿Cómo se representa la **plausibilidad** como valor combinable (contexto × historial × objetivo) sin que la decisión pierda explicabilidad ni se vuelva una caja cerrada?
2. ¿El OFP se integra como modelo **estático al iniciar la sesión** o puede re-consultarse/cambiarse en vuelo (modificación de plan, desvío)?
3. ¿El historial debe influir solo como **frecuencia** (cooldown, no repetir) o también como **narrativa** (no repetir sistema en la misma sesión, escalar severidad)?
4. Cuando la realidad contradice el OFP (retraso, desvío, avión parado), ¿el engine **re-planifica sobre la marcha**, descarta el plan restante o pausa los candidatos condicionados al plan?
5. ¿Qué nivel de **explicación** de la decisión se le da al piloto (por qué falló esto y por qué ahora) en la UI/log, y qué nivel al desarrollador (depuración y calibración)?

## Fallas ajenas al engine (externas) — hallazgo 2026-10-17, pendiente de diseño

Fenix simula fallas **propias de su flujo operativo** que el engine no provoca y no debe tocar. Verificado contra la API real (`GET /fenix/failures/manual`, avión A320 en gate, cold & dark): el listado devuelve **3 fallas activas por diseño** — `F_OH_NAV_IR_ALIGNMENT_1/2/3` (IR1/2/3 alignment, `failed=true`, sin `failureCondition`) — que se resuelven cuando el piloto completa la alineación de los ADIRs, flujo normal de cockpit preparation del Captain.

**Impacto sobre el engine actual (sin manejo de externas):**

1. **Reset total al detener** — `EngineOrchestrator.StopCurrentModeAsync` → `ResetAllFailuresAsync` (FenixApiFailureService.cs:80) desactiva **todas** las fallas activas/armadas de Fenix, externas incluidas: al detener una sesión borraría el estado operativo propio de Fenix (p. ej. las IR que el piloto esté alineando).
2. **Tope de simultáneas contaminado** — `RealisticSession.ActiveFailureCount` y `ActiveFenixFailureIds` (RealisticSession.cs:29-34) miran el estado Fenix crudo: con las 3 IR ALIGNMENT activas en gate, `ActiveFailureCount >= MaxSimultaneousActiveFailures (3)` y el engine **no puede disparar ninguna falla** hasta que el piloto alinee los ADIRs.
3. **Reconciliación futura** — la re-consulta tras reconexión / detección de resuelta no podría distinguir una externa resuelta (el piloto alineó) de una propia resuelta por el piloto.

**Decisión (fuera de alcance de la fase actual):** el engine debe operar sobre una **"cartera de fallas propias"** (engine-owned failures): IDs que él armó en la sesión (persistidos en `TriggeredFailure`), usándolos para topes de simultáneas y desarme/reset por ID; las externas consumen 0 y jamás se tocan. Las fallas de procedimiento (tipo IR ALIGNMENT) tampoco deberían ser candidatas armables por el engine. En esta fase solo queda documentado; no hay cambios de código.

## Severidad de fallas del catálogo (2026-10-17)

Las 384 fallas de `failures.json` llevan ahora `"severity": 1|2|3`, aplicada con la definición operativa siguiente (clasificación para **vuelo en condiciones normales, en crucero**):

| Nivel | Criterio | Marcos |
|---|---|---|
| **1 Minor** | El avión cumple el vuelo sin cambios de plan ni limitaciones; el sistema tiene respaldo/redundancia que absorbe la falla, o es información de apoyo | Falla de **un** canal de un sistema dual (CPC, MCDU, DU, fuel pumps, radios, ILS), equipamiento informativo (TCAS, GPWC, NAV, ATC, com), door/tyre/start y fallas de tierra |
| **2 Major** | Degrada capacidades: ECAM actions/checklist, respaldos mermados o vuelo con limitaciones; el destino sigue factible | Falla de un motor, un eje hidráulico, generador/IDG, ADIRU/ADR, buses principales, laps de detección de fuego, fuga de combustible/bleed, gear lock |
| **3 Critical** | Emergencia o sistema esencial sin respaldo: desvío/aterrizaje inmediato o compromete el control | Fuego/humo (motor, cargo, lavatory, avionics), descompresión rápida, buses esenciales (AC/DC ESS, ESS TR), direct/alternate law sin protecciones, gear locked up, sidestick reversal, elevador L+R, reverser presurizado/desbloqueado, falla de motor con daño, fuel valve de motor atascada |

Reglas de desempate usadas: la **redundancia del A320** es la guía principal (un canal de un par redundante → 1; sistema completo inoperante o par roto → 2; doble falla o pérdida sin respaldo de algo esencial → 3); **control de vuelo manda** (falla de control primario o ley sin respaldo → 3); fuego/humo/descompresión/falla de ambos motores → 3 salvo evidencia en contrario; equipamiento informativo → 1; ante duda → el nivel menor.

Distribución resultante: **173 Minor, 179 Major, 32 Critical**. Detalle: Fire protection 0/10/12, Power plant 4/18/6, Flight controls 10/20/5, Electrical power 11/15/3, AutoFlight 4/13/2, Fuel 20/9/2, Ice and rain 9/17/0, Hydraulic 7/14/0, Pneumatic 7/13/0, Air Conditioning 15/8/1, Landing gear 13/11/1, Navigation 24/27/0, Indicating 16/2/0, Doors 15/0/0, Start faults 8/0/0, APU 3/0/0, Communications 2/0/0, Oxygen 4/2/0, Information systems 1/0/0. La revisión completa (id | ATA | grupo | título | severidad) se hizo contra el catálogo: quedan a validar por el usuario los ítems dudosos anotados en el CHANGELOG.

Nota: `IRS Alignment` (IR1/2/3 alineación) quedaron con severidad 1 pero son fallas de **procedimiento** externas al engine (ver sección «Fallas ajenas al engine») y no deberían ser armables por el motor.

### Campo `repairable` (2026-10-17, mismo día)

Cada falla lleva además `"repairable": true|false` = **la función normal del sistema puede restaurarse durante el vuelo** (reset de computadora, re-arranque del motor, realineación de ADIRs, reintento de arranque). Es un atributo de catálogo **independiente de la severidad**: no se ajustó ningún valor de severidad al marcar esta propiedad (el motor decidirá más adelante cómo combinar ambos). Criterio aplicado: `true` solo donde existe un procedimiento en vuelo que el piloto ejecuta y Fenix refleja; por defecto `false` (mantenimiento en rampa / la falla persiste). Ante duda → `false`.

Distribución: **20 reparables en vuelo / 364 no**. Las `true` son: `F_ENGINE_1/2` (engine failure, relight), `F_OH_FLT_CTL_*_RECOVERABLE_*` (FAC1/2, ELAC1/2, SEC1/2/3 — resettable), `F_MCDU_1/2_RECOVERABLE`, `F_START_FAULT_*_HOT_START/HUNG_START/NO_IGNITION` (reintento de arranque en tierra) y las `F_OH_NAV_IR_ALIGNMENT_*` (se resuelven alinear; externas al engine de todas formas). Excluidas explícitamente a pesar del nombre o del evento: `Engine surge`, `Bird strike`, `Engine failure with damage`, `Fire … extinguished with N bottles` (estado posterior al evento, no acción del piloto), `Start valve` (válvula atascada: hardware) y las `resettable` de otros sistemas que no se pueden resetear en vuelo.

### Persistencia en el cargado inicial (2026-10-19)

`FailuresPersistenceService.LoadInitialFailuresAsync` ya **no fuerza `FailureSeverity.Minor`**: lee `severity` y `repairable` del catálogo y persiste `Severity` + `CanBeRepaired` (columna nueva) en cada `FenixFailureDefinition`. Cadena: `FenixManualFailure` (`Severity` int default 1, `Repairable` default false) → `FenixFailureDto` (`Severity` enum default `Minor`, `Repairable`) → `FenixMappers` → `FailuresPersistenceService` → repo. Nota: la **API viva** de Fenix no envía estos campos (quedan en defaults Minor/false, comportamiento previo preservado); solo el catálogo curado `failures.json` los aporta. Esquema: ya no hay «pendiente» de persistencia; la decisión de las externas sigue en la sección «Fallas ajenas al engine».

## Fase 2 — Módulo OFP/SimBrief (backend completo) (2026-10-17)

Terminado el backend del OFP y su enganche en el front (Paso 3): descargar, validar y persistir; la UI ya ejecuta el flujo real.

- **Integración nueva `RealFenixFailures.Integrations.SimBrief`**: `SimBriefApiClient` (HTTP real por `userid`; base `https://www.simbrief.com/api/xml.fetcher.php`) + `MockSimBriefApiClient` (DEBUG, OFP sintético) + `SimBriefApiOptions` (`SimBriefApi:BaseUrl`, `DownloadTimeoutSeconds`). `SimBriefOfpFetcher` adapta el cliente al contrato `IOfpFetcher` de Application.
- **Parser `SimBriefOfpReader`** (implementa `ISimBriefOfpReader` de Domain): convierte el XML del OFP a `FlightPlan` (aerolínea, número, ICAO ac/dest, horarios UTC, duración, ruta, raw). Sin validar. Desde 2026-10-18 interpreta los instantes de `times` como segundos Unix UTC: off-block de `sched_out`, aterrizaje estimado de `est_on` (alternativo `sched_on`) y duración de `est_time_enroute` en segundos convertidos a minutos. Conserva compatibilidad con el mock textual anterior y rechaza timestamps fuera de rango sin excepción. Mapeo, limitaciones y captura OFP completa de referencia en [integraciones/simbrief.md](../integraciones/simbrief.md).
- **Servicio `SimbriefOfpService`** (Application) orquesta: valida ICAO == aeronave registrada, campos mínimos (origen/destino/duración) y ventana UTC (`RealisticMode.Ofp` PreStart −2 h / PostArrival +2 h), persiste `FlightPlan` y devuelve `FlightPlanDto` con `ValidationState` y mensaje.
- **Persistencia** `IFlightPlanRepository`→`FlightPlanRepository` (Infrastructure) con `GetById`, `GetLatestForAircraftType`, `Add`.
- **Config/DI**: sección `SimBriefApi` en appsettings; `AddSimbriefIntegration` registrado en el host; solución con el nuevo csproj; referencias en UI y TestUnits.

**Decisión de capas**: el ID de SimBrief del usuario vive en userprefs (privacidad), no en BD; `FlightPlan` no guarda el userid.

**Enganche en el front (Paso 3)**: `RealisticModeViewModel` inyecta `ISimbriefOfpService`; el botón "Descargar OFP" queda habilitado (antes deshabilitado sin backend) y ejecuta `DownloadOfpAsync` con el `userid` (campo editable, referencia `SimbriefUserId`) y el `IcaoTypeCode` de la aeronave seleccionada. `OfpValidated`, `RouteDisplay` y `OfpStatusDisplay` se alimentan del `FlightPlanDto` devuelto; la activación se habilita sólo con OFP válido en ventana. En DEBUG se usa el mock de SimBrief (OFP sintético).

**Verificación**: build Release 0 errores/advertencias y **91/91 tests** (3 nuevos: parser del OFP, OFP válido en ventana persiste, ICAO distinto inválido). Sin verificación con la API real de SimBrief (pendiente de network/test manual).

## Tareas 1 y 2 — perfiles con topes/cadencias y arranque sin Fenix con cola (2026-10-19)

Implementadas las tareas 1 y 2 del diseño objetivo (perfiles de riesgo y arranque en tierra sin Fenix):

- **Perfiles con topes por severidad y cadencias** — `RealisticProfileCatalog` (Domain) define por perfil (Bajo/Moderado/Alto/7700) máximos simultáneos `Minor/Major/Critical`, tasas por hora (Poisson en `RealisticFailurePolicy`) y rangos de fallas de tierra; `FailureRuleEvaluator` permite a Low Minor+Major y a 7700 todas (antes Low solo Minor). `RiskLevel.Squawk7700` nuevo (valor 4) incluido en la selección de la UI. Los topes cuentan solo fallas propias activas + reservas de cola (una vez): `GetActiveForSessionAsync` + snapshot de `PreparedFailureQueue`.
- **Arranque sin gate de Fenix** — `TryStartSession` ya no exige Fenix conectado ni fase en gate con motores apagados; solo SimConnect + datos de simulación. En gate con motores apagados siembra fallas de tierra (todas las persistidas de severidad 1/2 —Minor/Major—, excluye IR_ALIGNMENT, sistemas distintos; ver «Seed de fallas de tierra…»), que se descartan al dejar OnGate (no se resiembra). Sin Fenix, las fallas elegidas se preparan en `PreparedFailureQueue` (acotada por `PreparedQueueLimit`, tierra con prioridad) y se envían al restablecerse; el motor espera sin resolver si Fenix estuvo disponible y cae. `EngineOrchestrator.StartRealisticModeAsync` y `CanActivate` ya no exigen Fenix.
- **UI** — `CanActivate` sin Fenix ni `IsOnGround`; rango de riesgo 1..4 con «7700»; botón `DiscardPreparedCommand` visible solo en sesión activa sin Fenix con cola no vacía (sin revelar fallas); textos de topes reales en las tarjetas del modal de perfiles; fila Fenix marcada «opcional» en la sesión.
- **Config** — sección `RealisticMode:Profiles` (topes/cadencias/rangos) y `GroundFailureRanges`/`EvaluationDeltaCapSeconds`/`RespiteClampSeconds` en `FailureEngineSettings`/appsettings; `SnapshotSettings` copia la configuración por sesión sin mutar la global.

**Verificación**: build Release de `Application`, `UI` y `TestUnits` con 0 errores/advertencias; **145/145 tests** (engine reescrito: 35 casos con reloj/random deterministas; catálogo y manager nuevos). **No verificado**: build de la solución completa en una sola ejecución, ni integración MSFS/Fenix real (mocks de DEBUG no reemplazan la prueba manual).

## Seed de fallas de tierra probabilístico individual (2026-10-22)

Reemplaza el modelo de seed descrito en «Tareas 1 y 2» (sorteo uniforme `Next(0, GroundFailuresCap)` + relleno con máx. 1 falla por sistema) y el historial de «Seed de fallas de tierra sin contaminar el tracking de EF»: la cantidad de fallas de tierra **deja de elegirse primero** y emerge de sorteos individuales por falla. `GroundFailuresCap` pasa a ser un **tope**, no un objetivo.

- **Modelo**: cada candidata Minor del catálogo (persistidas en `FenixFailureDefinition`, excluidas `F_OH_NAV_IR_ALIGNMENT*`) hace un sorteo Bernoulli independiente con `p_efectiva = clamp(p_base × (1 + k × wear_sistema_padre / 100), 0, 1)`. Sin registro de desgaste para el sistema → wear 0 → probabilidad base sin amplificar.
- **Parámetros por perfil** (`RealisticProfileSettings`, defaults en `RealisticProfileCatalog`, configurables vía `RealisticModeSettings.Profiles` —expuestos en `FailureEngine:RealisticMode:Profiles` de `appsettings.json`, fuente principal de calibración— y copiados ahora también por `SnapshotSettings`): `GroundFailureBaseProbability` (p_base) y `GroundWearProbabilityGain` (k). Defaults — Low: 0.004 / 1.5, Moderate: 0.01 / 2, High: 0.015 / 2.5, Squawk7700: 0.025 / 3. Con ~170 Minor elegibles, la esperanza queda debajo del cap de cada perfil; con desgaste alto el cap trunca.
- **Truncado por cap** (`RealisticFailurePolicy.SelectGroundFailures`): si los elegidos superan `GroundFailuresCap`, se prioriza **mayor desgaste → mayor p_base → azar**. Se elimina la regla "1 falla por sistema": puede haber 2+ fallas del mismo sistema (tope = cap). Con cap ≤ 0 no se siembra nada (antes `Next(0, 0)` lanzaba excepción).
- **Desgaste del sistema padre**: mapeo `FenixFailureSystem.AtaId ↔ AircraftWearableSystem.AtaId` con el desgaste de la aeronave de la sesión; para resolverlo desde memoria, `SystemWearDto` expone ahora `AtaId` (mapeado en `UserAircraftService`). `Probability` del `PresetFailureDefinition` sintético lleva la p_efectiva (antes un random placeholder), visible en logs.

**Verificación**: build Debug de la solución 0 errores/0 advertencias; `RealFenixFailures.TestUnits` **164/164 pasan** (nuevos: `RealisticFailurePolicyGroundSeedTests` para probabilidad y selección con cap; `RealisticProfileCatalogTests` valida defaults por perfil; el test de persistencia del seed usa p_base=1.0 determinista). **No verificado**: integración real MSFS/Fenix (requiere sesión real); la distribución resultante en vivo queda pendiente de observación en la PC de testing.

## Limitaciones restantes (no resueltas)

1. **Fallas de Training/Custom fuera del feed de eventos** — el log en pantalla (`MainWindowViewModel.LogEntries`) solo recibe eventos del detector; las fallas disparadas por los modos Training/Custom no llegan a ese feed. Referencia: `MainWindowViewModel.FlightEventDetector_FlightEventDetected`.
2. **Sin test de UI/WPF del límite del feed** — el tope de 200 entradas (`MaxLogEntries`) no tiene cobertura a nivel de viewmodel/WPF.
3. **Helpers sin uso con reflexión parcial** — `FenixFailures.GetAllFailureIds()` y `FenixFailures.Exists(id)` (`Domain/DTOs/FenixFailures.cs`) no se usan en la app y su reflexión no recorre clases anidadas (puede omitir fallas agrupadas).
4. **Backlog de diseño** — `RemoveTemporaryFailuresAsync`, desgaste, incompatibilidades de fallas y configuración de constantes siguen pendientes (ver "Pendiente").
5. **Auto-resume** — no implementado (ver "Pendiente").
6. **Integración real MSFS/Fenix sin verificar** — builds y tests no reemplazan la prueba manual.
7. **Historial persistido solo en el modo Realistic** — `FlightSession`, `SessionEventLog` y `TriggeredFailure` solo se crean desde `RealisticSessionEngine` durante sesiones Realistic; los modos Training/Custom no persisten sesiones ni historial (igual que antes de este cambio).
8. **Fallas ajenas al engine sin manejo** — Fenix levanta fallas propias de su flujo (comprobado: IR ALIGNMENT en gate); el engine actual las cuenta para topes y el reset total las desarma. Diseño pendiente de una "cartera de fallas propias"; ver sección "Fallas ajenas al engine (externas)".
