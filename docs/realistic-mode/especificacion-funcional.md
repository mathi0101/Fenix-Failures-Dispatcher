# Modo Realistic — especificación funcional deseada

Documento de **especificación funcional** (visión de producto, 2026-10). Describe el comportamiento **deseado** del modo Realistic según lo definido por el usuario/product owner. No describe el estado de implementación: para eso está [estado-y-diseno.md](estado-y-diseno.md), que es la fuente única de qué está construido, en desarrollo o pendiente.

Ante cualquier diferencia entre este documento y `estado-y-diseno.md`: este documento es el objetivo a alcanzar; `estado-y-diseno.md` es la realidad del código. La sección [Brechas con la implementación actual](#brechas-con-la-implementación-actual) conecta ambos.

## 1. Concepto base: ejecución en segundo plano

La app corre en segundo plano durante todo el vuelo en modo Realistic: el usuario vuela en MSFS2024 con el Fenix A320 y la app observa y actúa sin requerir interacción continua, más allá de arrancar la sesión y, eventualmente, reaccionar a una falla.

## 2. Selección y gestión de aeronaves

- Al iniciar, el usuario selecciona una aeronave existente o crea una nueva, identificada por **matrícula** (tail number).
- Cada aeronave acumula historial a través de los vuelos que se generan en modo Realistic con ella:
  - Fallas ocurridas.
  - Horas de vuelo.
  - Millas/distancia navegada.
  - (Implícito) cualquier otro dato de sesión relevante para el desgaste y el seguimiento de la aeronave.
- Esta información persiste y se asocia a la aeronave entre vuelos, no solo a la sesión individual: es lo que permite que el desgaste y el historial tengan continuidad vuelo tras vuelo.

## 3. Sistema de desgaste

- Mientras la aeronave vuela, sus sistemas acumulan **desgaste**, expresado como porcentaje por sistema (ej. motor 1, motor 2, sistema hidráulico, etc.), acumulable hasta el 100%.
- El desgaste se mide por **uso, distancia y horas de vuelo** — no por eventos aleatorios.
- A mayor desgaste de un sistema, **mayor probabilidad de que falle un componente de ese sistema específico**. Ejemplo: si el motor 2 tiene más desgaste que el motor 1, hay más probabilidad de que falle el motor 2 o cualquier componente asociado a él, no el sistema en general.
- El desgaste es **independiente del nivel de riesgo**: se acumula igual sin importar si el vuelo se vuela en Bajo, Moderado o Alto. El nivel de riesgo solo afecta qué tan probable/severa es la falla resultante, no la acumulación de desgaste en sí.

## 4. Niveles de riesgo del vuelo

Seleccionados por el usuario después de elegir la aeronave, antes de activar la sesión:

| Nivel | Nombre | Comportamiento esperado |
|---|---|---|
| 1 | **Bajo** | Fallas menores en sistemas secundarios. Pensado para vuelos tranquilos, bajo desafío. |
| 2 | **Moderado** | Fallas en sistemas principales, con **baja probabilidad** de escalar a emergencia. Un fallo de motor no debería ser lo típico, pero puede ocurrir con probabilidad baja. Es el modo de vuelo "normal". |
| 3 | **Alto** | Pensado para entrenamiento. Posibilidad de fallas críticas con alta probabilidad en cada vuelo; requiere manejo activo de emergencias y conocimiento real de la aeronave. |

## 5. Lógica de fallas en tiempo real

- Con la sesión activa, la app monitorea en tiempo real el estado del avión combinando SimConnect (contexto del vuelo) y la API de Fenix (sistemas reales), y evalúa continuamente si corresponde disparar una falla.
- Principio rector: **las fallas no son aleatorias, son racionales** — deben tener sentido según el contexto (fase de vuelo, desgaste de sistemas, condiciones operativas), no un sorteo desconectado de la situación real.
- Objetivo de diseño: mantener al piloto con el nivel de atención de un vuelo real — por ejemplo, que antes del despegue tenga presente el procedimiento de falla de motor (V1/decisión), o que durante el vuelo sepa reaccionar ante un problema de sistemas.
- Las fallas deben poder ocurrir en **cualquier fase del vuelo** (no limitadas a crucero), siempre que el contexto lo justifique.

## Brechas con la implementación actual

Comparación con [estado-y-diseno.md](estado-y-diseno.md) (2026-10-14):

| Punto de la especificación | Estado actual |
|---|---|
| Selección/alta de aeronave por matrícula, historial por aeronave | Modelo de datos existe (`UserAircraft`); uso funcional completo del historial (horas, millas) no confirmado como terminado — ver "Pendiente" en `estado-y-diseno.md`. |
| Desgaste por sistema, acumulado por uso/distancia/horas, afecta probabilidad de falla del componente | **No implementado.** `AircraftSystemWear` existe como entidad pero no está conectado a la probabilidad de disparo; la fórmula prevista (`probabilidadFinal = base × (1 + factor × wear/100)`) es una decisión abierta sin valores finales. |
| Desgaste independiente del nivel de riesgo | No aplica todavía: al no estar implementado el desgaste, tampoco está verificado que su acumulación sea independiente del riesgo. |
| 3 niveles de riesgo con efecto en severidad/probabilidad | Implementado de forma distinta a la narrativa: hoy el riesgo filtra **severidad permitida** (Low→Minor, Moderate→Minor/Major, High→todas) y aplica un **multiplicador de probabilidad** (0.5 / 1.0 / 1.75) sobre una probabilidad base fija por tick (0.10), no una lógica de "motor no debería fallar en Moderado pero puede". Revisar si el comportamiento resultante coincide con la intención narrativa de cada nivel. |
| Fallas racionales según contexto (fase, desgaste, condiciones) | Parcial: hoy el filtro es por fase de vuelo y severidad permitida (`FailureRuleEvaluator`), sin componente de desgaste ni de "historia" de la aeronave. |
| Fallas en cualquier fase del vuelo | Implementado en el motor de evaluación (`FlightEventDetector` + `RealisticSessionEngine` evalúan por tick durante todo el vuelo), pero el catálogo de fallas candidatas por fase depende de los presets configurados. |
| App en segundo plano, inicio solo con avión en gate y motores apagados | Implementado (`RealisticSessionEngine.TryStartSession`). |

## Decisiones abiertas derivadas de esta especificación

- Fórmula y constantes exactas de desgaste → probabilidad por componente/sistema (factor de amplificación, tasa de acumulación por hora/milla/ciclo).
- Mapeo entre sistemas desgastables (`AircraftWearableSystem`) y fallas concretas de Fenix (FK directa vs. tabla de mapeo) — ya señalado como decisión abierta en `estado-y-diseno.md`.
- Qué datos exactos de la sesión se muestran/exponen al usuario como "historial de la aeronave" (horas, millas, fallas, desgaste acumulado por sistema).
- Si el nivel de riesgo debe seguir modelándose como (severidad permitida × multiplicador de probabilidad) o si la narrativa de cada nivel requiere una regla distinta (por ejemplo, exclusión explícita de falla de motor salvo baja probabilidad en Moderado).

## Notas

- Este documento describe intención de producto; no reemplaza la verificación de implementación, que vive en [estado-y-diseno.md](estado-y-diseno.md).
- Pendiente: especificación equivalente para los modos Training y Custom (fuera del alcance de este documento).
