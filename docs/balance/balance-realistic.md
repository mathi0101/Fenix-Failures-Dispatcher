# Balance del modo Realistic — generado automáticamente

_Generado por `RealisticBalanceReportTests` con el código vigente del motor (FailureEngineSettings con sus perfiles; catálogo curado: 173 leves / 179 graves / 32 críticas). Regenerar: `docs/balance/balance.cmd` (herramienta manual, exige `RFF_BALANCE=1`). Versión formateada: `balance-realistic.xlsx`._

## Supuestos de la estimación

- Tasa en vuelo por severidad = `FailureRatePerHourBase` × multiplicador de severidad × multiplicador del perfil × factor de desgaste. Estimación global por severidad: no pondera por fase ni por la composición de presets.
- Terminología: **leves** = Minor, **graves** = Major, **críticas** = Critical.
- La semilla de tierra es un evento por vuelo (arranque de sesión); **no escala con las horas** y el motor solo arma fallas leves ahí.
- Perfil Bajo: el motor descarta fallas críticas, por lo que su cifra de críticas es siempre 0.
- La cadencia real la decide el motor tick a tick; esto es una estimación de presentación.

## Configuración del código con la que se generó este reporte

_Valores leídos del código al generar el archivo (`FailureEngineSettings` y perfiles del catálogo): si cambiaron parámetros, regenerar para reflejarlos._

| Parámetro | Valor |
|---|---:|
| Tasa base por falla leve elegible en vuelo, eventos/h (`FailureRatePerHourBase`) | 0,05 |
| Probabilidad base por falla leve en la semilla de tierra (`GroundSeedBaseProbability`) | 0,006 |
| Multiplicador de tasa leves / graves / críticas (`SeverityRateMultiplier`) | 1 / 0,4 / 0,14 |
| Peso de agravamiento leves / graves / críticas (`AggravationWeights`) | 1 / 2 / 4 |
| Agravación extra por misma ATA simultánea (`SameAtaAggravationExtra`) | 1 |
| Intervalo de evaluación del motor, s (`EvaluationIntervalSeconds`) | 60 |
| Semilla de tierra habilitada (`GroundFailureEnabled`) | sí |
| Umbral de aviso de sistema castigado, % (`Wear:ThresholdNoticePct`) | 80 |

| Perfil | Multiplicador | Desgaste máx | Tope agravación | Tope tierra | Tregua crítica |
|---|---:|---:|---:|---:|---:|
| **Bajo** | ×0,75 | ×2 | 3 | 1 | — |
| **Moderado** | ×1,1 | ×2,1 | 5 | 2 | 120 min |
| **Alto** | ×2,5 | ×2,2 | 8 | 3 | 60 min |
| **7700** | ×4,1 | ×2,3 | 10 | 5 | 30 min |

## Experiencia por vuelo (3 h; desgaste 0 / 25 / 100 %)

_Lo que siente el jugador por vuelo típico —la vista de 10 h de las tablas de abajo es para comparar perfiles—. Tres niveles de desgaste para comparar cómo empeora con el uso._

| Perfil | Desgaste | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |
|---|---|---:|---:|---:|---:|---:|
| **Bajo** | 0 % | 0,16 | 14,6 % | 4,4 % | — | — |
| **Bajo** | 25 % | 0,2 | 17,9 % | 5,5 % | — | — |
| **Bajo** | 100 % | 0,32 | 27,0 % | 8,6 % | — | — |
| **Moderado** | 0 % | 0,25 | 22,4 % | 6,4 % | 2,3 % | **43 vuelos** |
| **Moderado** | 25 % | 0,32 | 27,7 % | 8,1 % | 2,9 % | **34 vuelos** |
| **Moderado** | 100 % | 0,53 | 41,4 % | 12,9 % | 4,7 % | **21 vuelos** |
| **Alto** | 0 % | 0,58 | 43,9 % | 13,9 % | 5,1 % | **19 vuelos** |
| **Alto** | 25 % | 0,75 | 52,8 % | 17,7 % | 6,6 % | **15 vuelos** |
| **Alto** | 100 % | 1,27 | 71,9 % | 28,1 % | 10,9 % | **9 vuelos** |
| **7700** | 0 % | 0,95 | 61,2 % | 21,8 % | 8,2 % | **12 vuelos** |
| **7700** | 25 % | 1,25 | 71,5 % | 27,8 % | 10,8 % | **9 vuelos** |
| **7700** | 100 % | 2,18 | 88,7 % | 43,2 % | 18,0 % | **5 vuelos** |

### Ancla temporal del desgaste

Vuelos de 3 h que tarda un sistema en acumular cada nivel de desgaste, según su tasa (`WearPerFlightHour`; el seed va de 0,1 a 0,6 %/h):

| Tasa del sistema | Hasta 25 % | Hasta 50 % | Hasta 80 % (aviso) |
|---|---:|---:|---:|
| 0,1 %/h | 84 | 167 | 267 |
| 0,3 %/h | 28 | 56 | 89 |
| 0,6 %/h | 14 | 28 | 45 |

## Perfil Bajo (nivel 1)

Multiplicador ×0,75 · desgaste máx ×2 · tope agravación 3 · tope tierra 1 · tregua crítica —

### Fallas en vuelo por severidad

| Desgaste | Escala | Leves | Graves | Críticas | Total |
|---|---|---:|---:|---:|---:|
| 0 % | 1 h | 0,04 | 0,02 | 0 | 0,05 |
| 0 % | 10 h | 0,38 | 0,15 | 0 | 0,53 |
| 0 % | 100 h | 3,75 | 1,5 | 0 | 5,25 |
| 25 % | 1 h | 0,05 | 0,02 | 0 | 0,07 |
| 25 % | 10 h | 0,47 | 0,19 | 0 | 0,66 |
| 25 % | 100 h | 4,69 | 1,88 | 0 | 6,56 |
| 50 % | 1 h | 0,06 | 0,02 | 0 | 0,08 |
| 50 % | 10 h | 0,56 | 0,23 | 0 | 0,79 |
| 50 % | 100 h | 5,63 | 2,25 | 0 | 7,88 |
| 75 % | 1 h | 0,07 | 0,03 | 0 | 0,09 |
| 75 % | 10 h | 0,66 | 0,26 | 0 | 0,92 |
| 75 % | 100 h | 6,56 | 2,63 | 0 | 9,19 |
| 100 % | 1 h | 0,08 | 0,03 | 0 | 0,11 |
| 100 % | 10 h | 0,75 | 0,3 | 0 | 1,05 |
| 100 % | 100 h | 7,5 | 3 | 0 | 10,5 |

### Semilla de tierra (fallas esperadas al iniciar cada vuelo)

| Desgaste | Fallas esperadas al inicio |
|---|---:|
| 0 % | 0,67 |
| 25 % | 0,75 |
| 50 % | 0,81 |
| 75 % | 0,85 |
| 100 % | 0,89 |

### Experiencia por vuelo (3 h)

| Desgaste | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |
|---|---:|---:|---:|---:|---:|
| 0 % | 0,16 | 14,6 % | 4,4 % | — | — |
| 25 % | 0,2 | 17,9 % | 5,5 % | — | — |
| 50 % | 0,24 | 21,0 % | 6,5 % | — | — |
| 75 % | 0,28 | 24,1 % | 7,6 % | — | — |
| 100 % | 0,32 | 27,0 % | 8,6 % | — | — |

## Perfil Moderado (nivel 2)

Multiplicador ×1,1 · desgaste máx ×2,1 · tope agravación 5 · tope tierra 2 · tregua crítica 120 min

### Fallas en vuelo por severidad

| Desgaste | Escala | Leves | Graves | Críticas | Total |
|---|---|---:|---:|---:|---:|
| 0 % | 1 h | 0,06 | 0,02 | 0,01 | 0,08 |
| 0 % | 10 h | 0,55 | 0,22 | 0,08 | 0,85 |
| 0 % | 100 h | 5,5 | 2,2 | 0,77 | 8,47 |
| 25 % | 1 h | 0,07 | 0,03 | 0,01 | 0,11 |
| 25 % | 10 h | 0,7 | 0,28 | 0,1 | 1,08 |
| 25 % | 100 h | 7,01 | 2,81 | 0,98 | 10,8 |
| 50 % | 1 h | 0,09 | 0,03 | 0,01 | 0,13 |
| 50 % | 10 h | 0,85 | 0,34 | 0,12 | 1,31 |
| 50 % | 100 h | 8,53 | 3,41 | 1,19 | 13,13 |
| 75 % | 1 h | 0,1 | 0,04 | 0,01 | 0,15 |
| 75 % | 10 h | 1 | 0,4 | 0,14 | 1,55 |
| 75 % | 100 h | 10,04 | 4,02 | 1,41 | 15,46 |
| 100 % | 1 h | 0,12 | 0,05 | 0,02 | 0,18 |
| 100 % | 10 h | 1,16 | 0,46 | 0,16 | 1,78 |
| 100 % | 100 h | 11,55 | 4,62 | 1,62 | 17,79 |

### Semilla de tierra (fallas esperadas al iniciar cada vuelo)

| Desgaste | Fallas esperadas al inicio |
|---|---:|
| 0 % | 1,28 |
| 25 % | 1,48 |
| 50 % | 1,63 |
| 75 % | 1,74 |
| 100 % | 1,82 |

### Experiencia por vuelo (3 h)

| Desgaste | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |
|---|---:|---:|---:|---:|---:|
| 0 % | 0,25 | 22,4 % | 6,4 % | 2,3 % | **43 vuelos** |
| 25 % | 0,32 | 27,7 % | 8,1 % | 2,9 % | **34 vuelos** |
| 50 % | 0,39 | 32,6 % | 9,7 % | 3,5 % | **28 vuelos** |
| 75 % | 0,46 | 37,1 % | 11,3 % | 4,1 % | **24 vuelos** |
| 100 % | 0,53 | 41,4 % | 12,9 % | 4,7 % | **21 vuelos** |

## Perfil Alto (nivel 3)

Multiplicador ×2,5 · desgaste máx ×2,2 · tope agravación 8 · tope tierra 3 · tregua crítica 60 min

### Fallas en vuelo por severidad

| Desgaste | Escala | Leves | Graves | Críticas | Total |
|---|---|---:|---:|---:|---:|
| 0 % | 1 h | 0,13 | 0,05 | 0,02 | 0,19 |
| 0 % | 10 h | 1,25 | 0,5 | 0,18 | 1,93 |
| 0 % | 100 h | 12,5 | 5 | 1,75 | 19,25 |
| 25 % | 1 h | 0,16 | 0,07 | 0,02 | 0,25 |
| 25 % | 10 h | 1,63 | 0,65 | 0,23 | 2,5 |
| 25 % | 100 h | 16,25 | 6,5 | 2,28 | 25,03 |
| 50 % | 1 h | 0,2 | 0,08 | 0,03 | 0,31 |
| 50 % | 10 h | 2 | 0,8 | 0,28 | 3,08 |
| 50 % | 100 h | 20 | 8 | 2,8 | 30,8 |
| 75 % | 1 h | 0,24 | 0,1 | 0,03 | 0,37 |
| 75 % | 10 h | 2,38 | 0,95 | 0,33 | 3,66 |
| 75 % | 100 h | 23,75 | 9,5 | 3,33 | 36,58 |
| 100 % | 1 h | 0,28 | 0,11 | 0,04 | 0,42 |
| 100 % | 10 h | 2,75 | 1,1 | 0,39 | 4,24 |
| 100 % | 100 h | 27,5 | 11 | 3,85 | 42,35 |

### Semilla de tierra (fallas esperadas al iniciar cada vuelo)

| Desgaste | Fallas esperadas al inicio |
|---|---:|
| 0 % | 2,56 |
| 25 % | 2,80 |
| 50 % | 2,91 |
| 75 % | 2,96 |
| 100 % | 2,98 |

### Experiencia por vuelo (3 h)

| Desgaste | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |
|---|---:|---:|---:|---:|---:|
| 0 % | 0,58 | 43,9 % | 13,9 % | 5,1 % | **19 vuelos** |
| 25 % | 0,75 | 52,8 % | 17,7 % | 6,6 % | **15 vuelos** |
| 50 % | 0,92 | 60,3 % | 21,3 % | 8,1 % | **12 vuelos** |
| 75 % | 1,1 | 66,6 % | 24,8 % | 9,5 % | **10 vuelos** |
| 100 % | 1,27 | 71,9 % | 28,1 % | 10,9 % | **9 vuelos** |

## Perfil 7700 (nivel 4)

Multiplicador ×4,1 · desgaste máx ×2,3 · tope agravación 10 · tope tierra 5 · tregua crítica 30 min

### Fallas en vuelo por severidad

| Desgaste | Escala | Leves | Graves | Críticas | Total |
|---|---|---:|---:|---:|---:|
| 0 % | 1 h | 0,21 | 0,08 | 0,03 | 0,32 |
| 0 % | 10 h | 2,05 | 0,82 | 0,29 | 3,16 |
| 0 % | 100 h | 20,5 | 8,2 | 2,87 | 31,57 |
| 25 % | 1 h | 0,27 | 0,11 | 0,04 | 0,42 |
| 25 % | 10 h | 2,72 | 1,09 | 0,38 | 4,18 |
| 25 % | 100 h | 27,16 | 10,87 | 3,8 | 41,83 |
| 50 % | 1 h | 0,34 | 0,14 | 0,05 | 0,52 |
| 50 % | 10 h | 3,38 | 1,35 | 0,47 | 5,21 |
| 50 % | 100 h | 33,83 | 13,53 | 4,74 | 52,09 |
| 75 % | 1 h | 0,4 | 0,16 | 0,06 | 0,62 |
| 75 % | 10 h | 4,05 | 1,62 | 0,57 | 6,24 |
| 75 % | 100 h | 40,49 | 16,2 | 5,67 | 62,35 |
| 100 % | 1 h | 0,47 | 0,19 | 0,07 | 0,73 |
| 100 % | 10 h | 4,72 | 1,89 | 0,66 | 7,26 |
| 100 % | 100 h | 47,15 | 18,86 | 6,6 | 72,61 |

### Semilla de tierra (fallas esperadas al iniciar cada vuelo)

| Desgaste | Fallas esperadas al inicio |
|---|---:|
| 0 % | 4,49 |
| 25 % | 4,84 |
| 50 % | 4,95 |
| 75 % | 4,99 |
| 100 % | 5,00 |

### Experiencia por vuelo (3 h)

| Desgaste | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |
|---|---:|---:|---:|---:|---:|
| 0 % | 0,95 | 61,2 % | 21,8 % | 8,2 % | **12 vuelos** |
| 25 % | 1,25 | 71,5 % | 27,8 % | 10,8 % | **9 vuelos** |
| 50 % | 1,56 | 79,0 % | 33,4 % | 13,2 % | **7 vuelos** |
| 75 % | 1,87 | 84,6 % | 38,5 % | 15,6 % | **6 vuelos** |
| 100 % | 2,18 | 88,7 % | 43,2 % | 18,0 % | **5 vuelos** |

