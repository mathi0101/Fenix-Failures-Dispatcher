# Tipos de falla del modo Realistic — taxonomía y diseño

**Estado: parcialmente implementado (2026-10-26).** Este doc es la fuente única para la nomenclatura y el comportamiento objetivo de cada tipo. Implementado: fallas de tierra (leves + recuperables) y fallas aleatorias (universo de catálogo completo). Pendiente: fallas temporales (§4.3) y escenarios armables (§4.4, depende de la migración de `realistic_presets.json`).

## 1. Por qué una taxonomía

Hoy el motor no distingue tipos: todo lo que arma en vuelo sale de las definiciones de los presets realistas, y la semilla de tierra comparte esa lógica. Eso es un error de modelado que esta taxonomía corrige: los presets son **escenarios** (una falla de motor *before V1* debe dispararla la lógica interna de la API de Fenix en el momento correcto del vuelo, no el azar del motor), mientras que las fallas "de simulación" (tierra, aleatorias, temporales) salen del catálogo completo y las decide el motor.

## 2. Las dos grandes familias: modo de activación

El eje que separa todo es **cómo se dispara la falla**:

- **Instantáneas**: el motor decide el momento y Fenix activa la falla al procesarla. Son de "sorteo y armado inmediato".
- **Armables**: la app arma la falla con su condición/fase y **Fenix la dispara con su lógica interna** en el momento correcto. La app no elige cuándo ocurre: solo arma.

| Familia | Quién decide el momento | Tipos |
|---|---|---|
| **Instantáneas** | El motor (sorteo) | Fallas de tierra, fallas aleatorias, fallas temporales |
| **Armables** | Fenix (lógica interna tras el armado) | Escenarios predefinidos (presets) |

## 3. Los cuatro tipos

| Tipo | Inglés (código) | Familia | Origen | Duración | Universo / definición |
|---|---|---|---|---|---|
| **Fallas de tierra** | Ground failures | Instantánea | Catálogo | Permanente | Solo **leves y recuperables** (`CanBeRepaired = true`) — **implementado** |
| **Fallas aleatorias** | Random failures | Instantánea | Catálogo | Permanente | Catálogo completo, mismas probabilidades de hoy — **implementado** |
| **Fallas temporales** | Timed failures | Instantánea | Catálogo | **Temporal** (auto-resolución) | Catálogo completo + tiempo aleatorio de vida |
| **Escenarios predefinidos** | Presets | Armable (mayormente) | Preset (escenario) | La que define el escenario | `realistic_presets.json`, armado tal cual está definido |

Dimensiones ortogonales que conviene no mezclar al hablar:

- **Origen**: catálogo (`FenixFailureDefinitions`, falla individual) vs preset (escenario compuesto).
- **Activación**: instantánea vs armable.
- **Duración**: permanente vs temporal (auto-resolución).

Un escenario (preset) está compuesto por fallas; cada falla del escenario puede ser instantánea o armable, aunque por lo general serán armables.

### Nombres que se descartan

- **«Fallas inteligentes»**: vago para el usuario y para el código. Se dice **escenarios predefinidos** (o «presets»).
- «Fallas de preset» como tipo: los presets no son un tipo de falla, son un origen/estructura (escenario). El tipo lo determina la activación.

## 4. Comportamiento objetivo por tipo (delta con hoy)

### 4.1 Fallas de tierra (hoy: semilla al iniciar la sesión) — **implementado 2026-10-26**

- **Igual que hoy**: evento one-shot al iniciar la sesión, esperanza truncada por el tope de tierra del perfil (`GroundFailuresCap`), escala con el desgaste, no suma en las métricas «en vuelo».
- **Cambio aplicado**: solo pueden ser **leves y recuperables** — filtro **`CanBeRepaired = true`** además del de severidad (Minor) en `BuildGroundCandidatesAsync` (`RealisticSessionEngine`). No arranca una sesión con una falla permanente que el piloto no puede resolver.

### 4.2 Fallas aleatorias (antes: «armado desde presets», error) — **implementado 2026-10-26**

- **Cambio aplicado**: las fallas en vuelo se sortean del **catálogo completo** (`FenixFailureDefinitions` vía `GetAllWithSystemsAsync`, cacheado por vida del engine) — `GetFlightUniverseAsync` arma candidatos sintéticos con `Preset = null` (pasan el filtro de fase en cualquier fase de vuelo) y `Probability = 1.0` (la λ por severidad la pone la política: base × severidad × perfil × desgaste, igual que la estimación del balance). Los presets dejan de alimentar el sorteo aleatorio (siguen cargados para el armado de escenarios, §4.4).
- **Igual que hoy**: caps de agravamiento, treguas y topes de simultáneas por perfil; regla de severidad del perfil (Bajo sin críticas) sigue en `FailureRuleEvaluator`.
- **Sinergia**: la estimación del balance ya era global por severidad, así que quedó alineada al universo nuevo sin cambios.

### 4.3 Fallas temporales (nuevo)

- Se sortean **igual que las aleatorias** (mismo universo y proceso), pero al armarse se les asigna un **tiempo aleatorio de vida** dentro de la sesión: transcurrido, la falla se desactiva sola (auto-resolución) sin acción del piloto.
- Parámetros nuevos de configuración: ventana de vida (mín/máx) y proporción de sorteos que salen temporales vs permanentes (o un multiplicador propio; decisión en la tarea de implementación).
- Mientras están activas cuentan para los topes de simultáneas y para el agravamiento igual que cualquier falla.

### 4.4 Escenarios predefinidos (hoy: presets)

- **Se arman tal cual están definidos** en `realistic_presets.json`: cada definición del escenario se arma según su propia naturaleza (instantánea o armable) y su probabilidad definida (el `Probability` de cada definición es parte de la definición).
- El timing del disparo lo resuelve **la lógica interna de la API de Fenix**: la app arma en la fase correcta (ej. falla de motor before V1 → se arma en taxi inicial) y Fenix la ejecuta en el momento preciso. Nosotros no simulamos ese momento.
- **Tarea aparte**: migrar `realistic_presets.json` al nuevo esquema (marcar por definición activación instantánea/armable). Mientras no ocurra, los presets siguen comportándose como hoy.

## 5. Nomenclatura para código, UI y docs

| Concepto | UI (piloto) | Código | Enum/valor propuesto |
|---|---|---|---|
| Familia por activación | «instantánea» / «armada» | `ActivationMode` | `Instant` / `Armed` |
| Origen de la definición | — | `FailureOrigin` | `Catalog` / `Scenario` |
| Duración | «temporal» / «permanente» | `FailureDuration` | `Permanent` / `Timed` |
| Fallas de tierra | «fallas en tierra» | Ground seed | `GroundFailureSource` |
| Fallas aleatorias | «fallas aleatorias» | Random | `RandomFailureSource` |
| Fallas temporales | «fallas temporales» | Timed | `TimedFailureSource` |
| Escenarios predefinidos | «escenarios» | Presets | `ScenarioSource` |

Nota de implementación: el campo de recuperabilidad en el catálogo/código es **`CanBeRepaired`** (campo `repairable` en `failures.json`); «recuperable» es la traducción al usuario.

Terminología de severidad vigente (no cambia): leves = Minor, graves = Major, críticas = Critical.

## 6. Qué no cambia

- Jerarquía y multiplicadores de severidad, desgaste, treguas tras crítica, topes de simultáneas y de agravamiento por perfil, ventana de perfiles y sus estimaciones.
- El reporte de balance: su fórmula global por severidad ya no depende del universo de presets, y con las fallas aleatorias sacadas del catálogo completo se vuelve **más** exacta.
- La doctrina de tierra: solo leves (ahora también solo recuperables), nunca graves ni críticas al iniciar sesión.

## 7. Plan de implementación sugerido (tareas separadas)

1. **Ground**: filtro `Repairable = true` en las candidatas de tierra — **hecho (2026-10-26)**.
2. **Universo aleatorio**: reemplazar el sorteo en vuelo por el catálogo completo — **hecho (2026-10-26)** (`GetFlightUniverseAsync`).
3. **Temporales**: duración (`FailureDuration`) + auto-resolución + parámetros de ventana y proporción — pendiente.
4. **Escenarios armables**: armado por fase con disparo delegado a Fenix; requiere el esquema nuevo de `realistic_presets.json` — pendiente (tarea aparte, ver §8).
5. **UI/docs**: etiquetas de la ventana de perfiles y textos del modal según §5 cuando el motor ya tenga los tipos — pendiente.

## 8. Dependencias y abiertas

- **Migración de `realistic_presets.json`**: necesaria para §4.4 (esquema con activación por definición). Tarea aparte; hasta entonces los escenarios siguen armándose como hoy.
- **Abiertas (decidir en implementación)**: ventana de vida de las temporales (mín/máx) y su proporción vs permanentes; si la auto-resolución de una temporal que estaba agravando un sistema recalcúla el score al bajar; interacción tregua crítica × temporales (¿una temporal que expira durante la tregua reactiva el reloj?); si el cap de tierra aplica también a fallas de tierra provenientes de escenarios.
