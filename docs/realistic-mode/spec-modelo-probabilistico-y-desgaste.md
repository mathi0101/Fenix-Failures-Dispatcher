# Spec — Modelo probabilístico unificado, score de agravamiento y desgaste dinámico

> **Estado: BORRADOR para revisión — NO IMPLEMENTADO.** Nada de esto está en el árbol todavía.
> Fecha: 2026-10-23. Origen: incidente de la sesión 12 (doble falla hidráulica en perfil Moderate) y rondas de diseño del mismo día (ver Project Memory, "Modo Realistic - Agravamiento acumulado").
> Cuando se implemente, esta spec REEMPLAZA en `estado-y-diseno.md`: la tabla de constantes del motor viejo, las secciones «Tareas 1 y 2» (topes por severidad y tasas por hora) y «Seed de fallas de tierra probabilístico individual» (parámetros, no el mecanismo, que se conserva).

---

## 0. Motivación

Incidente (logs de `\\PC-MATHIAS\RealFenixFailures`, sesión 12, CX-SDI, Moderate, SUMU→SACO):

| Hora (-03) | Falla | Severidad | Sistema |
|---|---|---|---|
| 23:14 | F_ECP | Minor (1) | ATA 31 |
| 23:44 | F_BRAKE_NWS | Minor (1) | ATA 32 |
| 00:07 | F_HYD_LEAK_BLUE | Major (2) | ATA 29 |
| 00:32 | F_HYD_LEAK_GREEN | Major (2) | ATA 29 |

Doble falla hidráulica no repairable → piloto declara emergencia y desvía. Huecos del modelo vigente que lo permitieron:

1. **No existe medida de carga combinada**: `MinorCap`/`MajorCap`/`CriticalCap` son buckets independientes; 2 Majors pasan si `MajorCap ≥ 2`, aunque sean del mismo sistema.
2. **La exclusión de elegibles es por ID de falla** (`FailureRuleEvaluator.cs:22`), nunca por sistema: la green seguía elegible con la blue activa (mismo preset, ATA 29).
3. **El respiro solo se activa tras una Critical**: una 2ª Major no protege nada.
4. **Sin correlación entre sistemas** (backlog histórico de diseño).

Además, la configuración creció de más: 11 knobs por perfil donde el problema real tiene 2 ejes (frecuencia y carga).

## 1. Modelo probabilístico unificado

Una sola fórmula para tierra y vuelo:

```
p_falla = p_base × SeverityMult(severidad) × ProbabilityMultiplier(perfil)
          × WearFactor(sistema, desgaste, perfil) × Probability(del preset)
```

La interpretación de `p_falla` depende del contexto:

- **Seed de tierra** (gate, motores apagados): Bernoulli **one-shot** por candidata (una única decisión por sesión, igual que hoy). Clamp [0,1]. El universo sigue siendo solo Minors persistidas, excluidas `F_OH_NAV_IR_ALIGNMENT*`.
- **En vuelo**: `p_falla` se interpreta como **tasa λ en eventos/hora**. El roll del tick integra el tiempo transcurrido (mecanismo Poisson actual de `RealisticFailurePolicy.RollTick`):

```
p_tick = 1 − e^(−Σλ_elegibles·Δt),   Δt = tiempo desde el último roll (cap: EvaluationDeltaCapSeconds)
```

Esto preserva la **invariancia al `EvaluationIntervalSeconds`** (requisito duro: el intervalo solo define cada cuánto se corre el chequeo, nunca la cadencia). Válido mientras intervalo ≤ `EvaluationDeltaCapSeconds`.

**Simplificación del sorteo**: hoy son dos sorteos (severidad por tasas → candidato por peso). Con λ por falla es **un solo roll ponderado** sobre las elegibles, donde el peso de cada una es su λ efectiva.

### 1.1 Parámetros globales de física (nueva sección `RealisticMode:Physics`)

| Parámetro | Default borrador | Significado |
|---|---|---|
| `FailureRatePerHourBase` | 0.01 | λ base por falla Minor elegible, en eventos/hora |
| `GroundSeedBaseProbability` | 0.01 | p one-shot del seed de tierra (unidad distinta de la anterior; se mantiene separada para calibrar cada eje) |
| `SeverityRateMultiplier` | Minor 1.0 / Major 0.4 / Critical 0.15 | Cuánto más rara es cada severidad a igualdad de todo lo demás |
| `AggravationWeights` | Minor 1 / Major 2 / Critical 4 | Pesos del score de agravamiento (§2) |
| `SameAtaAggravationExtra` | 1 | Puntos extra por cada falla previa simultánea del mismo ATA (§2.3) |

Estos defaults viven en `RealisticProfileCatalog` (código, única fuente); `appsettings` puede sobrescribirlos para calibrar en la PC de testing sin recompilar.

Con ~20 Minors y ~5 Majors elegibles por fase, las tasas esperadas quedan:

| Perfil | Mult | Minor/h | Major/h | Critical/h | (Referencia actual) |
|---|---|---|---|---|---|
| Low | 0.75 | 0.15 | 0.015 | 0 | hoy: 0.5 / 0.05 / 0 |
| Moderate | 1.0 | 0.20 | 0.02 | 0.003 | hoy: 0.2 / 0.1 / 0.01 |
| High | 1.5 | 0.30 | 0.03 | 0.010 | hoy: 0.3 / 0.25 / 0.1 |
| 7700 | 2.5 | 0.50 | 0.05 | 0.019 | hoy: 0.6 / 0.5 / 0.3 |

Consecuencias deliberadas: las Majors bajan ~5× en Moderate (corrección del "es demasiado" del incidente) y Low deja el 0.5/h de Minors que era mayor que Moderate (anomalía histórica). El bloqueo de Critical en Low sigue siendo política, no física (ver §2.3).

### 1.2 El `Probability` del preset se conserva

El peso curado de cada falla dentro de su preset (ej. 1/6 a cada hidráulica) multiplica su λ. `Probability = 0` ⇒ excluida.

## 2. Score de agravamiento y `AggravationCap`

### 2.1 Definición

```
score = Σ AggravationWeight(severidad) para cada falla ACTIVA propia de la sesión
        + Σ peso de cada RESERVA en PreparedFailureQueue
```

- Misma base de conteo que los topes actuales: activas (por `TriggeredFailure` persistido) + reservas de cola contadas una sola vez.
- Las fallas resueltas **no computan**: resolver enfría el score y reabre margen automáticamente.

### 2.2 Freno de sesión

- `AggravationCap` por perfil (nullable; `null` = sin tope, como hoy 7700).
- Al alcanzarlo el motor **no elige más fallas de ninguna severidad** hasta que el score baje. No desarma nada.

### 2.3 Filtrado ANTES del roll

La elegibilidad por severidad pasa a ser: `score + AggravationWeight(sev) ≤ AggravationCap`. Reemplaza a `HasSeverityRoom` en el mismo punto del pipeline (`TryChooseAsync`), para no gastar sorteos en severidades sin lugar.

- «Varias Minor cuentan como una Major» queda realizado por esta aritmética: 2 Minors (score 2) bloquean lo mismo que 1 Major.
- **Mismo sistema (ATA), sin bloqueo**: no hay exclusión por ATA. Las fallas del mismo sistema siguen elegibles, pero cada falla adicional simultánea de un ATA que ya tiene una activa/reservada suma un **extra de agravamiento** (`SameAtaAggravationExtra`, default 1 punto por cada falla previa del mismo ATA). Un 2º hidráulico cuesta 2+1=3 de score; un 3º, 2+2=4. Improbable que pase el filtro de score, pero posible — y cuando pasa, el score refleja que la situación es peor que una Major suelta.
- **7700 queda exento del extra de ATA en v1** (decisión a revisar en calibración). El bloqueo de Critical en Low se mantiene como política (`FailureRuleEvaluator`), sin importar el score.

### 2.4 El incidente bajo este modelo (Moderate, cap 5)

ECP (1) → NWS (2) → blue (score 4, entra; ATA 29 ahora cargado) → green: costaría 2 + extra 1 = **3 → 4+3 = 7 > 5 ⇒ bloqueada por score**. Resultado: una sola fuga hidráulica, sin Mayday. Con 7700 (sin cap) sí podría pasar: segundo hidráulico posible, con su extra pagado en score.

### 2.5 Cola de preparadas

`FlushPreparedAsync` revalida, antes de cada envío: elegibilidad vigente y `score + peso(reserva) + extras ATA vigentes ≤ cap`. La reserva ocupa su peso desde que entra a la cola.

### 2.6 Lo que se elimina

`MinorCap`, `MajorCap`, `CriticalCap`, `MinorPerHour`, `MajorPerHour`, `CriticalPerHour`, `GroundFailureBaseProbability`, `GroundWearProbabilityGain`. El rol del `CriticalCap=0` de Low ya lo cumple la política de severidades de `FailureRuleEvaluator`.

`GroundFailuresCap` **se conserva** (eje distinto: cuántas fallas recibe el avión en gate; el sorteo Bernoulli necesita tope propio). El respiro tras Critical se conserva (supresión temporal, distinta de la carga).

## 3. Modelo de desgaste

### 3.1 Factor por sistema (reemplaza a los gains)

```
WearFactor(sistema, perfil) = 1 + (WearMaxMultiplier(perfil) − 1) × wear_sistema/100
```

`WearMaxMultiplier` responde la pregunta "¿qué significa 100% de desgaste?": a 100%, la probabilidad de fallar se multiplica por ese valor. Lineal: 50% de desgaste → mitad del efecto; 0% → ×1.

| Perfil | `WearMaxMultiplier` | (equivale a los gains de hoy + 1) |
|---|---|---|
| Low | 2.5 | gain 1.5 |
| Moderate | 3.0 | gain 2 |
| High | 3.5 | gain 2.5 |
| 7700 | 4.0 | gain 3 |

Por perfil (decisión de diseño: el perfil define cuánto respeta el estado del avión; consistente con que el gain de tierra ya era por perfil). La **definición** del 100% es global (física), el **valor** es política.

El mapeo `FenixFailureSystem.AtaId ↔ AircraftWearableSystem.AtaId` existente se reutiliza para ambos contextos (tierra y vuelo). Sin registro de desgaste del sistema → ×1.

### 3.2 Acumulación por horas de vuelo (comportamiento NUEVO)

Hoy el desgaste es estático (lo pone el seeder debug y nunca cambia). Se agrega:

- **Columna nueva en el catálogo**: `AircraftWearableSystem.WearPerFlightHour` (double, % por hora de vuelo, global por sistema). Valores borrador del seed data de Infrastructure:

| Sistema (ATA) | %/h borrador | Racional |
|---|---|---|
| ENG (70-75), APU (49) | 1.0 | Desgaste térmico/mecánico alto |
| HYD (29), FUEL (28), NEUM (36) | 0.6 | Uso continuo en vuelo |
| ELEC (24), AVIONICS (31, 34) | 0.3 | Carga moderada |
| Estructura, tren en vuelo (32*), cabina | 0.1 | Fatiga lenta |

  (*el tren acumula sobre todo por ciclos — ver backlog §5)

- **Acumulación**: al cerrar sesión, `wear_sistema += WearPerFlightHour × horas_de_vuelo_de_la_sesión`, clamp [0, 100], actualizando `AircraftSystemWear.WearPercentage` y `LastUpdatedAt`. Sin horas de sesión (start sin fin, crash) no se acumula.
- **Horas de vuelo de la sesión**: duración real de la sesión (`FlightSession.StartedAt → FinishedAt`), medida al cerrarla. **No** se usa la duración del OFP como fuente primaria (la sesión es el uso efectivo del avión); el valor estimado del OFP es solo un fallback en la medida que el motor lo necesite antes del primer cierre (ej. preview pre-vuelo), nunca para acumular desgaste. Nunca wall-clock.

### 3.3 OFP: qué se usa y qué no

Verificado: `FlightPlan` ya persiste `EstimatedDurationMinutes` y `RouteDistanceNM` (esta última aún no expuesta en `FlightPlanDto`).

- **Se usa**: duración estimada del OFP como **fallback para el preview pre-vuelo (§5) únicamente** — no para acumular desgaste (§3.2, se usa la duración real de la sesión).
- **No se usa**: multiplicador de frecuencia por duración/distancia. El modelo de tasas horarias ya escala solo (vuelo de 4 h a λ=0.2/h espera 0.8 fallas; uno de 40 min, 0.13). Un factor adicional contaría dos veces.

## 4. Configuración final

Perfil: de 11 campos a **6**. Espejo completo validado en `%TEMP%\RF-espejo\appsettings.ESPEJO.propuesta.json`; la sección `RealisticMode` resultante:

```json
"RealisticMode": {
  "EvaluationIntervalSeconds": 5,
  "PreparedQueueLimit": 5,
  "GroundFailureEnabled": true,
  "ThinkingLinesCap": 50,
  "EvaluationDeltaCapSeconds": 300,
  "RespiteClampSeconds": 900,
  "Physics": {
    "FailureRatePerHourBase": 0.01,
    "GroundSeedBaseProbability": 0.01,
    "SeverityRateMultiplier": { "Minor": 1.0, "Major": 0.4, "Critical": 0.15 },
    "AggravationWeights": { "Minor": 1, "Major": 2, "Critical": 4 },
    "SameAtaAggravationExtra": 1
  },
  "Profiles": [
    { "RiskLevel": 1, "ProbabilityMultiplier": 0.75, "WearMaxMultiplier": 2.5, "AggravationCap": 3,   "GroundFailuresCap": 1, "CriticalRespiteSeconds": 0 },
    { "RiskLevel": 2, "ProbabilityMultiplier": 1.0,  "WearMaxMultiplier": 3.0, "AggravationCap": 5,   "GroundFailuresCap": 2, "CriticalRespiteSeconds": 0 },
    { "RiskLevel": 3, "ProbabilityMultiplier": 1.5,  "WearMaxMultiplier": 3.5, "AggravationCap": 7,   "GroundFailuresCap": 3, "CriticalRespiteSeconds": 600 },
    { "RiskLevel": 4, "ProbabilityMultiplier": 2.5,  "WearMaxMultiplier": 4.0, "AggravationCap": null, "GroundFailuresCap": 5, "CriticalRespiteSeconds": 300 }
  ],
  "Ofp": { "PreStartWindowHours": 2, "PostArrivalWindowHours": 0.5 },
  "Wear": { "AmplificationFactor": 0.5, "ThresholdNoticePct": 80 }
}
```

Notas de migración de config: los `appsettings` existentes (PC de testing incluida) con la sección vieja deben reemplazarse; los campos nuevos que falten caen a los defaults del código (binding tolerante), pero los viejos (`*PerHour`, `*Cap`, `GroundWearProbabilityGain`) dejan de leerse. El appsettings vigente de la PC de testing tiene caps apretados a mano (Minor 3/4/6, Major 1/2/3, Critical 0/1/2/3): ese ajuste fino pasa a vivir en `AggravationCap`.

## 5. Backlog (explícitamente fuera de esta implementación)

1. **Ciclos de vuelo** (v2): `wear += cycle_bump` por vuelo para sistemas de fatiga (tren, frenos, motores). Un hop corto hace el mismo ciclo que uno largo en menos horas; `RouteDistanceNM` + duración lo identifican (ratio distancia/duración). Requiere una constante más por sistema en la DB.
2. **`CapWearFactor`** (v2): cap dinámico por desgaste medio de la aeronave (`cap_efectivo = cap × (1 − k × wear_medio/100)`, default 0 = apagado). El desgaste ya amplifica probabilidad; acoplar también el cap duplica el efecto y complica la calibración.
3. **Preview pre-vuelo en UI**: con la duración del OFP, mostrar expectativa antes de activar («2h20 Moderate → ~0.5 fallas esperadas», `Σλ × horas`). Herramienta de calibración esperado-vs-observado.
4. **Correlaciones/incompatibilidades** más finas que la regla ATA (backlog histórico).

## 6. Cambios por proyecto

| Proyecto | Cambios |
|---|---|
| **Domain** | `RealisticProfileSettings`: nuevos campos (`ProbabilityMultiplier`, `WearMaxMultiplier`, `AggravationCap`), baja de los eliminados. `RealisticProfileCatalog`: nuevos defaults por perfil + defaults de `Physics`. `FailureEngineSettings`: sección `Physics` nueva. `FailureRuleEvaluator`: elegibilidad por score/cap/ATA (entra el score como input). `AircraftWearableSystem`: columna `WearPerFlightHour`. |
| **Application** | `RealisticFailurePolicy`: `RollTick` unificado (un roll ponderado por λ), score, `WearFactor`; seed de tierra con la fórmula unificada. `RealisticSessionEngine`: `TryChooseAsync` (filtrado score/ATA pre-roll, Δt medido igual que hoy), `SeedGroundFailuresAsync` (misma fórmula), acumulación de desgaste al cerrar sesión. `PreparedFailureQueue`: reservas con peso; `FlushPreparedAsync` revalida score/ATA. |
| **Infrastructure** | Seed data `WearPerFlightHour` (§3.2). Sin migraciones (dev usa `EnsureCreatedAsync`): **borrar `realfenixfailures.db` de `%AppData%\RealFenixFailures\`** tras el cambio de esquema. |
| **UI** | `appsettings.json` con la sección nueva (espejo §4). Modal de perfiles: fichas con los 6 knobs reales. Panel Modo Realista: overhaul según §8 (bloque OFP ampliado, checks, navegación, expectativa de fallas). |
| **TestUnits** | Tests nuevos deterministas (§7) y actualización de los que usan knobs viejos (`RealisticProfileCatalogTests`, engine, policy). |

## 7. Criterios de aceptación

1. **Build y tests**: build Release 0 errores/0 advertencias; suite completa en verde con los tests nuevos.
2. **Invariancia al intervalo** (regresión clave): con reloj/random deterministas, la tasa de disparos con `EvaluationIntervalSeconds` 5 vs 30 es estadísticamente indistinguible en N sesiones simuladas.
3. **Reproducción del incidente**: test con seed determinista que reconstruye la secuencia de la sesión 12 — la green **nunca** es elegida (el score con extra ATA excede el cap de Moderate) y con cap `null` el 2º hidráulico SÍ puede salir, pagando el extra.
4. **Score**: pesos suman activas + reservas una sola vez; extras ATA aplican por falla previa simultánea del mismo ATA; resueltas descuentan; cap alcanzado ⇒ cero elecciones; `null` ⇒ sin freno.
5. **Seed de tierra**: p efectiva con/sin desgaste (clamp [0,1]), cap prioriza mayor desgaste → mayor p → azar (se conserva el comportamiento actual).
6. **Desgaste**: acumulación por horas con clamp [0,100] y fallback a duración OFP; `WearFactor` a 0/50/100% da ×1 / mitad / máximo.
7. **Docs**: `estado-y-diseno.md` actualizado (marcando las secciones reemplazadas) + entrada en `CHANGELOG.md` en el mismo trabajo.
8. **Verificación manual en PC de testing** (integración real MSFS/Fenix): pendiente siempre; los builds y tests no la reemplazan. Calibración fina de defaults tras observar sesiones reales.
9. **UI (§8)**: build + verificación manual de la vista — la fila OFP refleja el estado real de validación (fix del texto estático), el check de aeronave del OFP muestra ✔/✘ con el mensaje del servicio, las pastillas navegan hacia atrás y quedan bloqueadas con sesión activa, el botón de OFP queda deshabilitado durante la sesión, y la expectativa de fallas aparece junto al desgaste medio (con y sin perfil elegido).

## 8. UI — Panel Modo Realista (`RealisticModePanel`)

> Revisión previa a implementación (pedido del usuario: dejar front y motor ajustados juntos). Grounding: `RealisticModePanel.xaml` (952 líneas) y `RealisticModeViewModel.cs` (843 líneas), leídos íntegros el 2026-10-23.

### 8.0 Hallazgos de la revisión

- **Bug**: la fila "OFP" del panel de requisitos es texto estático `✘ Pendiente` (RealisticModePanel.xaml ~línea 725) — nunca refleja `OfpValidated`. Se corrige en §8.4.
- `CanDownloadOfp` **ya** bloquea la descarga durante sesión (`!IsEngineActive && !IsOperationPending`, RealisticModeViewModel.cs:348) — falta solo el efecto visual/tooltip.
- La validación de ICAO del OFP contra la aeronave **ya existe** en `SimbriefOfpService` (compara `AircraftIcaoType`, SimbriefOfpService.cs:63-65) pero no se muestra como check propio (§8.2).
- `FlightPlan.RouteDistanceNM` existe en la entidad pero **no** está en `FlightPlanDto` ni se puebla desde SimBrief — requerido para mostrar distancia (§8.1).

### 8.1 Bloque OFP ampliado

Al cargar un OFP, el bloque "OFP Y RUTA" pasa a mostrar (etiqueta + valor, vacíos ocultos):

| Dato | Fuente |
|---|---|
| Ruta (chips `SUMU → SACO` + route text) | `OriginIcao`/`DestinationIcao`/`RouteText` |
| Nº de vuelo y Callsign | `FlightNumber`/`Callsign` |
| Salida programada (local, con UTC en tooltip) | `OffBlockUtc` |
| Llegada estimada (local) | `EstimatedArrivalUtc` |
| Duración total estimada («2 h 15 m») | `EstimatedDurationMinutes` |
| Distancia («1 240 NM») | `RouteDistanceNM` — **requiere** exponerla en `FlightPlanDto` y poblarla parseando SimBrief |
| Aeronave del OFP («A320») | `AircraftIcaoType` |
| Fuente y hora de descarga («SimBrief · 22:31») | `Source`/`DownloadedAtUtc` |

Estado vacío: «Sin OFP · Descargalo para ver los detalles del vuelo». El estado de validación sigue visible (`OfpStatusDisplay`).

### 8.2 Check de aeronave del OFP en requisitos

Nueva fila en "REQUISITOS PARA ACTIVAR": **«Aeronave del OFP»** → `✔ Cumplido` (verde) cuando el OFP válido corresponde a la aeronave seleccionada; `✘` (ámbar) con el mensaje del servicio («El OFP corresponde a A321, no a A320») si no. La comparación ya la hace el servicio; la UI expone el resultado del último `ApplyOfpResult` (propiedad calculada `OfpAircraftMatch`).

### 8.3 Botón «Descargar OFP»

- Rediseño: botón primario suave (accent outline con icono de descarga + texto), altura 32, anclado **abajo a la izquierda** del bloque OFP fijo (hoy queda flotando bajo el estado).
- Durante sesión: `IsEnabled=false` (lógica ya existente) + tooltip «Sesión activa: el OFP no puede modificarse. Detené la sesión para cargar otro plan.». Mientras descarga: deshabilitado con la barra indeterminada existente.
- La nota del userid queda como pie del bloque (sin cambios).

### 8.4 Panel de requisitos — ajustes

- **Fix**: fila «OFP» bindeada a `OfpValidated` (hoy estática): `✔ Cumplido · Listo para activar` / `✘ Pendiente` / `✘ Inválido · mensaje`.
- Checks consistentes: todos verde `✔` cumplido / ámbar `✘` pendiente; nuevo orden: MSFS · Datos aeronave · Aeronave del OFP (§8.2) · OFP · Perfil. Fenix queda al final como nota opcional.
- **Botón de perfil rediseñado**: reemplaza el ghost pequeño por un card-button ancho completo con el nombre del perfil («Moderado»), subtítulo de estado («Seleccionado» / «Sin perfil») e icono de edición a la derecha; deshabilitado con sesión activa (lógica `CanChangeProfile` ya existente).

### 8.5 Navegación por pastillas (sin «◀ Volver»)

- Se eliminan los botones «◀ Volver» de los encabezados de pasos 2 y 3.
- Las pastillas «1 · Aeronave / 2 · Resumen / 3 · Preparación» pasan a ser botones de navegación: **hacia atrás** se puede saltar a cualquiera (habilitadas si `!IsEngineActive && !IsOperationPending`); **hacia adelante** sigue solo el botón «Continuar a preparación ▶» (pastillas futuras deshabilitadas).
- Con el motor activo, las pastillas quedan deshabilitadas (equivalente a como hoy se ocultan los Volver).

### 8.6 Encabezado del paso 3

Sin «Volver», el par «Estado: X · Perfil: Y» sube a la banda del encabezado, **a la misma altura que las 3 pastillas** (pills a la izquierda, Estado/Perfil a la derecha; la aeronave sigue visible en el card colapsable de desgaste).

### 8.7 Expectativa de fallas junto al desgaste medio

Junto a «Desgaste medio: X%» del header colapsable (paso 3), agregar la expectativa para la próxima sesión, calculada **antes de elegir perfil** (referencia: Moderate, `ProbabilityMultiplier=1.0`):

- Con OFP cargado: «≈ N fallas esperadas · P(≥1 falla) = Y% para 2 h 20 m» — N = Σλ (modelo §1) × horas del OFP; P = `1 − e^(−Σλ·T)`.
- Sin OFP: ritmo por hora: «≈ 0.2 fallas/h de vuelo (referencia Moderado)».
- El desgaste medio entra vía `WearFactor` por sistema (§3.1); tooltip detalla supuestos (perfil de referencia, duración, desgaste vigente).
- Si hay perfil elegido, se muestra además el valor con ese perfil.
- Requiere un servicio de Application (ej. `IRealisticExpectationService`: wear + duración → Σλ, esperadas, P≥1) que reutilice la λ del motor. Es estimación de presentación, no cambia el motor (mismo criterio que §5.3).

## 9. Riesgos abiertos

- **Mix de severidades uniforme entre perfiles**: con multiplicadores globales, todos los perfiles comparten proporciones (hoy Low tiene Majors 10× más raras que Minors y High casi parejas). Mitigado: bloqueo político de Critical en Low + calibración posterior.
- **Dos bases de datos con esquema nuevo**: la PC de testing comparte su DB en el share; regenerarla borra historial de sesiones — coordinar con el usuario antes.
- **Trabajo multi-agente**: el árbol tiene WIP de otras tareas (ver `git status`); la implementación se hará sobre archivos releídos al momento y commits selectivos de solo los archivos de esta spec.
