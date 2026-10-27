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
| **Bajo** | ×0,6 | ×2 | 3 | 1 | — |
| **Moderado** | ×1,1 | ×2 | 5 | 2 | 120 min |
| **Alto** | ×1,6 | ×2,5 | 8 | 3 | 60 min |
| **7700** | ×2,2 | ×4 | 10 | 5 | 30 min |

## Experiencia por vuelo (3 h; desgaste 0 / 25 / 100 %)

_Lo que siente el jugador por vuelo típico —la vista de 10 h de las tablas de abajo es para comparar perfiles—. Tres niveles de desgaste para comparar cómo empeora con el uso._

| Perfil | Desgaste | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |
|---|---|---:|---:|---:|---:|---:|
| **Bajo** | 0 % | 0,13 | 11,8 % | 3,5 % | — | — |
| **Bajo** | 25 % | 0,16 | 14,6 % | 4,4 % | — | — |
| **Bajo** | 100 % | 0,25 | 22,3 % | 6,9 % | — | — |
| **Moderado** | 0 % | 0,25 | 22,4 % | 6,4 % | 2,3 % | **43 vuelos** |
| **Moderado** | 25 % | 0,32 | 27,2 % | 7,9 % | 2,8 % | **35 vuelos** |
| **Moderado** | 100 % | 0,51 | 39,8 % | 12,4 % | 4,5 % | **22 vuelos** |
| **Alto** | 0 % | 0,37 | 30,9 % | 9,2 % | 3,3 % | **30 vuelos** |
| **Alto** | 25 % | 0,51 | 39,8 % | 12,4 % | 4,5 % | **22 vuelos** |
| **Alto** | 100 % | 0,92 | 60,3 % | 21,3 % | 8,1 % | **12 vuelos** |
| **7700** | 0 % | 0,51 | 39,8 % | 12,4 % | 4,5 % | **22 vuelos** |
| **7700** | 25 % | 0,89 | 58,9 % | 20,6 % | 7,8 % | **12 vuelos** |
| **7700** | 100 % | 2,03 | 86,9 % | 41,0 % | 16,9 % | **5 vuelos** |

### Ancla temporal del desgaste

Vuelos de 3 h que tarda un sistema en acumular cada nivel de desgaste, según su tasa (`WearPerFlightHour`; el seed va de 0,1 a 0,6 %/h):

| Tasa del sistema | Hasta 25 % | Hasta 50 % | Hasta 80 % (aviso) |
|---|---:|---:|---:|
| 0,1 %/h | 84 | 167 | 267 |
| 0,3 %/h | 28 | 56 | 89 |
| 0,6 %/h | 14 | 28 | 45 |

## Perfil Bajo (nivel 1)

Multiplicador ×0,6 · desgaste máx ×2 · tope agravación 3 · tope tierra 1 · tregua crítica —

### Fallas en vuelo por severidad

| Desgaste | Escala | Leves | Graves | Críticas | Total |
|---|---|---:|---:|---:|---:|
| 0 % | 1 h | 0,03 | 0,01 | 0 | 0,04 |
| 0 % | 10 h | 0,3 | 0,12 | 0 | 0,42 |
| 0 % | 100 h | 3 | 1,2 | 0 | 4,2 |
| 25 % | 1 h | 0,04 | 0,02 | 0 | 0,05 |
| 25 % | 10 h | 0,38 | 0,15 | 0 | 0,53 |
| 25 % | 100 h | 3,75 | 1,5 | 0 | 5,25 |
| 50 % | 1 h | 0,05 | 0,02 | 0 | 0,06 |
| 50 % | 10 h | 0,45 | 0,18 | 0 | 0,63 |
| 50 % | 100 h | 4,5 | 1,8 | 0 | 6,3 |
| 75 % | 1 h | 0,05 | 0,02 | 0 | 0,07 |
| 75 % | 10 h | 0,53 | 0,21 | 0 | 0,74 |
| 75 % | 100 h | 5,25 | 2,1 | 0 | 7,35 |
| 100 % | 1 h | 0,06 | 0,02 | 0 | 0,08 |
| 100 % | 10 h | 0,6 | 0,24 | 0 | 0,84 |
| 100 % | 100 h | 6 | 2,4 | 0 | 8,4 |

### Semilla de tierra (fallas esperadas al iniciar cada vuelo)

| Desgaste | Fallas esperadas al inicio |
|---|---:|
| 0 % | 0,59 |
| 25 % | 0,67 |
| 50 % | 0,73 |
| 75 % | 0,79 |
| 100 % | 0,83 |

### Experiencia por vuelo (3 h)

| Desgaste | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |
|---|---:|---:|---:|---:|---:|
| 0 % | 0,13 | 11,8 % | 3,5 % | — | — |
| 25 % | 0,16 | 14,6 % | 4,4 % | — | — |
| 50 % | 0,19 | 17,2 % | 5,3 % | — | — |
| 75 % | 0,22 | 19,8 % | 6,1 % | — | — |
| 100 % | 0,25 | 22,3 % | 6,9 % | — | — |

## Perfil Moderado (nivel 2)

Multiplicador ×1,1 · desgaste máx ×2 · tope agravación 5 · tope tierra 2 · tregua crítica 120 min

### Fallas en vuelo por severidad

| Desgaste | Escala | Leves | Graves | Críticas | Total |
|---|---|---:|---:|---:|---:|
| 0 % | 1 h | 0,06 | 0,02 | 0,01 | 0,08 |
| 0 % | 10 h | 0,55 | 0,22 | 0,08 | 0,85 |
| 0 % | 100 h | 5,5 | 2,2 | 0,77 | 8,47 |
| 25 % | 1 h | 0,07 | 0,03 | 0,01 | 0,11 |
| 25 % | 10 h | 0,69 | 0,28 | 0,1 | 1,06 |
| 25 % | 100 h | 6,88 | 2,75 | 0,96 | 10,59 |
| 50 % | 1 h | 0,08 | 0,03 | 0,01 | 0,13 |
| 50 % | 10 h | 0,83 | 0,33 | 0,12 | 1,27 |
| 50 % | 100 h | 8,25 | 3,3 | 1,16 | 12,71 |
| 75 % | 1 h | 0,1 | 0,04 | 0,01 | 0,15 |
| 75 % | 10 h | 0,96 | 0,39 | 0,13 | 1,48 |
| 75 % | 100 h | 9,63 | 3,85 | 1,35 | 14,82 |
| 100 % | 1 h | 0,11 | 0,04 | 0,02 | 0,17 |
| 100 % | 10 h | 1,1 | 0,44 | 0,15 | 1,69 |
| 100 % | 100 h | 11 | 4,4 | 1,54 | 16,94 |

### Semilla de tierra (fallas esperadas al iniciar cada vuelo)

| Desgaste | Fallas esperadas al inicio |
|---|---:|
| 0 % | 1,28 |
| 25 % | 1,47 |
| 50 % | 1,61 |
| 75 % | 1,71 |
| 100 % | 1,79 |

### Experiencia por vuelo (3 h)

| Desgaste | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |
|---|---:|---:|---:|---:|---:|
| 0 % | 0,25 | 22,4 % | 6,4 % | 2,3 % | **43 vuelos** |
| 25 % | 0,32 | 27,2 % | 7,9 % | 2,8 % | **35 vuelos** |
| 50 % | 0,38 | 31,7 % | 9,4 % | 3,4 % | **29 vuelos** |
| 75 % | 0,44 | 35,9 % | 10,9 % | 4,0 % | **25 vuelos** |
| 100 % | 0,51 | 39,8 % | 12,4 % | 4,5 % | **22 vuelos** |

## Perfil Alto (nivel 3)

Multiplicador ×1,6 · desgaste máx ×2,5 · tope agravación 8 · tope tierra 3 · tregua crítica 60 min

### Fallas en vuelo por severidad

| Desgaste | Escala | Leves | Graves | Críticas | Total |
|---|---|---:|---:|---:|---:|
| 0 % | 1 h | 0,08 | 0,03 | 0,01 | 0,12 |
| 0 % | 10 h | 0,8 | 0,32 | 0,11 | 1,23 |
| 0 % | 100 h | 8 | 3,2 | 1,12 | 12,32 |
| 25 % | 1 h | 0,11 | 0,04 | 0,02 | 0,17 |
| 25 % | 10 h | 1,1 | 0,44 | 0,15 | 1,69 |
| 25 % | 100 h | 11 | 4,4 | 1,54 | 16,94 |
| 50 % | 1 h | 0,14 | 0,06 | 0,02 | 0,22 |
| 50 % | 10 h | 1,4 | 0,56 | 0,2 | 2,16 |
| 50 % | 100 h | 14 | 5,6 | 1,96 | 21,56 |
| 75 % | 1 h | 0,17 | 0,07 | 0,02 | 0,26 |
| 75 % | 10 h | 1,7 | 0,68 | 0,24 | 2,62 |
| 75 % | 100 h | 17 | 6,8 | 2,38 | 26,18 |
| 100 % | 1 h | 0,2 | 0,08 | 0,03 | 0,31 |
| 100 % | 10 h | 2 | 0,8 | 0,28 | 3,08 |
| 100 % | 100 h | 20 | 8 | 2,8 | 30,8 |

### Semilla de tierra (fallas esperadas al iniciar cada vuelo)

| Desgaste | Fallas esperadas al inicio |
|---|---:|
| 0 % | 2,00 |
| 25 % | 2,42 |
| 50 % | 2,68 |
| 75 % | 2,83 |
| 100 % | 2,91 |

### Experiencia por vuelo (3 h)

| Desgaste | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |
|---|---:|---:|---:|---:|---:|
| 0 % | 0,37 | 30,9 % | 9,2 % | 3,3 % | **30 vuelos** |
| 25 % | 0,51 | 39,8 % | 12,4 % | 4,5 % | **22 vuelos** |
| 50 % | 0,65 | 47,6 % | 15,5 % | 5,7 % | **17 vuelos** |
| 75 % | 0,79 | 54,4 % | 18,5 % | 6,9 % | **14 vuelos** |
| 100 % | 0,92 | 60,3 % | 21,3 % | 8,1 % | **12 vuelos** |

## Perfil 7700 (nivel 4)

Multiplicador ×2,2 · desgaste máx ×4 · tope agravación 10 · tope tierra 5 · tregua crítica 30 min

### Fallas en vuelo por severidad

| Desgaste | Escala | Leves | Graves | Críticas | Total |
|---|---|---:|---:|---:|---:|
| 0 % | 1 h | 0,11 | 0,04 | 0,02 | 0,17 |
| 0 % | 10 h | 1,1 | 0,44 | 0,15 | 1,69 |
| 0 % | 100 h | 11 | 4,4 | 1,54 | 16,94 |
| 25 % | 1 h | 0,19 | 0,08 | 0,03 | 0,3 |
| 25 % | 10 h | 1,93 | 0,77 | 0,27 | 2,96 |
| 25 % | 100 h | 19,25 | 7,7 | 2,7 | 29,65 |
| 50 % | 1 h | 0,28 | 0,11 | 0,04 | 0,42 |
| 50 % | 10 h | 2,75 | 1,1 | 0,39 | 4,24 |
| 50 % | 100 h | 27,5 | 11 | 3,85 | 42,35 |
| 75 % | 1 h | 0,36 | 0,14 | 0,05 | 0,55 |
| 75 % | 10 h | 3,58 | 1,43 | 0,5 | 5,51 |
| 75 % | 100 h | 35,75 | 14,3 | 5,01 | 55,06 |
| 100 % | 1 h | 0,44 | 0,18 | 0,06 | 0,68 |
| 100 % | 10 h | 4,4 | 1,76 | 0,62 | 6,78 |
| 100 % | 100 h | 44 | 17,6 | 6,16 | 67,76 |

### Semilla de tierra (fallas esperadas al iniciar cada vuelo)

| Desgaste | Fallas esperadas al inicio |
|---|---:|
| 0 % | 3,05 |
| 25 % | 4,37 |
| 50 % | 4,85 |
| 75 % | 4,97 |
| 100 % | 4,99 |

### Experiencia por vuelo (3 h)

| Desgaste | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |
|---|---:|---:|---:|---:|---:|
| 0 % | 0,51 | 39,8 % | 12,4 % | 4,5 % | **22 vuelos** |
| 25 % | 0,89 | 58,9 % | 20,6 % | 7,8 % | **12 vuelos** |
| 50 % | 1,27 | 71,9 % | 28,1 % | 10,9 % | **9 vuelos** |
| 75 % | 1,65 | 80,8 % | 34,9 % | 13,9 % | **7 vuelos** |
| 100 % | 2,03 | 86,9 % | 41,0 % | 16,9 % | **5 vuelos** |

