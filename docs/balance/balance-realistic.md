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
| Tasa base por falla leve elegible en vuelo, eventos/h (`FailureRatePerHourBase`) | 0,15 |
| Probabilidad base por falla leve en la semilla de tierra (`GroundSeedBaseProbability`) | 0,001 |
| Multiplicador de tasa leves / graves / críticas (`SeverityRateMultiplier`) | 1 / 0,4 / 0,12 |
| Peso de agravamiento leves / graves / críticas (`AggravationWeights`) | 1 / 2 / 4 |
| Agravación extra por misma ATA simultánea (`SameAtaAggravationExtra`) | 1 |
| Intervalo de evaluación del motor, s (`EvaluationIntervalSeconds`) | 60 |
| Semilla de tierra habilitada (`GroundFailureEnabled`) | sí |
| Umbral de aviso de sistema castigado, % (`Wear:ThresholdNoticePct`) | 80 |

| Perfil | Multiplicador | Desgaste máx | Tope agravación | Tope tierra | Tregua crítica |
|---|---:|---:|---:|---:|---:|
| **Bajo** | ×0,6 | ×2 | 3 | 1 | — |
| **Moderado** | ×1 | ×2 | 5 | 2 | 120 min |
| **Alto** | ×1,4 | ×2,5 | 8 | 3 | 60 min |
| **7700** | ×2,2 | ×4 | 10 | 5 | 30 min |

## Experiencia por vuelo (3 h; desgaste 0 / 25 / 100 %)

_Lo que siente el jugador por vuelo típico —la vista de 10 h de las tablas de abajo es para comparar perfiles—. Tres niveles de desgaste para comparar cómo empeora con el uso._

| Perfil | Desgaste | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |
|---|---|---:|---:|---:|---:|---:|
| **Bajo** | 0 % | 0,38 | 31,5 % | 10,2 % | — | — |
| **Bajo** | 25 % | 0,47 | 37,7 % | 12,6 % | — | — |
| **Bajo** | 100 % | 0,76 | 53,0 % | 19,4 % | — | — |
| **Moderado** | 0 % | 0,68 | 49,5 % | 16,5 % | 5,3 % | **19 vuelos** |
| **Moderado** | 25 % | 0,86 | 57,5 % | 20,1 % | 6,5 % | **15 vuelos** |
| **Moderado** | 100 % | 1,37 | 74,5 % | 30,2 % | 10,2 % | **9 vuelos** |
| **Alto** | 0 % | 0,96 | 61,6 % | 22,3 % | 7,3 % | **13 vuelos** |
| **Alto** | 25 % | 1,32 | 73,2 % | 29,3 % | 9,9 % | **10 vuelos** |
| **Alto** | 100 % | 2,39 | 90,9 % | 46,7 % | 17,2 % | **5 vuelos** |
| **7700** | 0 % | 1,5 | 77,8 % | 32,7 % | 11,2 % | **8 vuelos** |
| **7700** | 25 % | 2,63 | 92,8 % | 50,0 % | 18,8 % | **5 vuelos** |
| **7700** | 100 % | 6,02 | 99,8 % | 79,5 % | 37,8 % | **2 vuelos** |

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
| 0 % | 1 h | 0,09 | 0,04 | 0 | 0,13 |
| 0 % | 10 h | 0,9 | 0,36 | 0 | 1,26 |
| 0 % | 100 h | 9 | 3,6 | 0 | 12,6 |
| 25 % | 1 h | 0,11 | 0,05 | 0 | 0,16 |
| 25 % | 10 h | 1,13 | 0,45 | 0 | 1,58 |
| 25 % | 100 h | 11,25 | 4,5 | 0 | 15,75 |
| 50 % | 1 h | 0,14 | 0,05 | 0 | 0,19 |
| 50 % | 10 h | 1,35 | 0,54 | 0 | 1,89 |
| 50 % | 100 h | 13,5 | 5,4 | 0 | 18,9 |
| 75 % | 1 h | 0,16 | 0,06 | 0 | 0,22 |
| 75 % | 10 h | 1,58 | 0,63 | 0 | 2,21 |
| 75 % | 100 h | 15,75 | 6,3 | 0 | 22,05 |
| 100 % | 1 h | 0,18 | 0,07 | 0 | 0,25 |
| 100 % | 10 h | 1,8 | 0,72 | 0 | 2,52 |
| 100 % | 100 h | 18 | 7,2 | 0 | 25,2 |

### Semilla de tierra (fallas esperadas al iniciar cada vuelo)

| Desgaste | Fallas esperadas al inicio |
|---|---:|
| 0 % | 0,14 |
| 25 % | 0,17 |
| 50 % | 0,20 |
| 75 % | 0,23 |
| 100 % | 0,25 |

### Experiencia por vuelo (3 h)

| Desgaste | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |
|---|---:|---:|---:|---:|---:|
| 0 % | 0,38 | 31,5 % | 10,2 % | — | — |
| 25 % | 0,47 | 37,7 % | 12,6 % | — | — |
| 50 % | 0,57 | 43,3 % | 15,0 % | — | — |
| 75 % | 0,66 | 48,4 % | 17,2 % | — | — |
| 100 % | 0,76 | 53,0 % | 19,4 % | — | — |

## Perfil Moderado (nivel 2)

Multiplicador ×1 · desgaste máx ×2 · tope agravación 5 · tope tierra 2 · tregua crítica 120 min

### Fallas en vuelo por severidad

| Desgaste | Escala | Leves | Graves | Críticas | Total |
|---|---|---:|---:|---:|---:|
| 0 % | 1 h | 0,15 | 0,06 | 0,02 | 0,23 |
| 0 % | 10 h | 1,5 | 0,6 | 0,18 | 2,28 |
| 0 % | 100 h | 15 | 6 | 1,8 | 22,8 |
| 25 % | 1 h | 0,19 | 0,08 | 0,02 | 0,29 |
| 25 % | 10 h | 1,88 | 0,75 | 0,23 | 2,85 |
| 25 % | 100 h | 18,75 | 7,5 | 2,25 | 28,5 |
| 50 % | 1 h | 0,23 | 0,09 | 0,03 | 0,34 |
| 50 % | 10 h | 2,25 | 0,9 | 0,27 | 3,42 |
| 50 % | 100 h | 22,5 | 9 | 2,7 | 34,2 |
| 75 % | 1 h | 0,26 | 0,11 | 0,03 | 0,4 |
| 75 % | 10 h | 2,63 | 1,05 | 0,32 | 3,99 |
| 75 % | 100 h | 26,25 | 10,5 | 3,15 | 39,9 |
| 100 % | 1 h | 0,3 | 0,12 | 0,04 | 0,46 |
| 100 % | 10 h | 3 | 1,2 | 0,36 | 4,56 |
| 100 % | 100 h | 30 | 12 | 3,6 | 45,6 |

### Semilla de tierra (fallas esperadas al iniciar cada vuelo)

| Desgaste | Fallas esperadas al inicio |
|---|---:|
| 0 % | 0,24 |
| 25 % | 0,30 |
| 50 % | 0,36 |
| 75 % | 0,42 |
| 100 % | 0,47 |

### Experiencia por vuelo (3 h)

| Desgaste | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |
|---|---:|---:|---:|---:|---:|
| 0 % | 0,68 | 49,5 % | 16,5 % | 5,3 % | **19 vuelos** |
| 25 % | 0,86 | 57,5 % | 20,1 % | 6,5 % | **15 vuelos** |
| 50 % | 1,03 | 64,2 % | 23,7 % | 7,8 % | **12 vuelos** |
| 75 % | 1,2 | 69,8 % | 27,0 % | 9,0 % | **11 vuelos** |
| 100 % | 1,37 | 74,5 % | 30,2 % | 10,2 % | **9 vuelos** |

## Perfil Alto (nivel 3)

Multiplicador ×1,4 · desgaste máx ×2,5 · tope agravación 8 · tope tierra 3 · tregua crítica 60 min

### Fallas en vuelo por severidad

| Desgaste | Escala | Leves | Graves | Críticas | Total |
|---|---|---:|---:|---:|---:|
| 0 % | 1 h | 0,21 | 0,08 | 0,03 | 0,32 |
| 0 % | 10 h | 2,1 | 0,84 | 0,25 | 3,19 |
| 0 % | 100 h | 21 | 8,4 | 2,52 | 31,92 |
| 25 % | 1 h | 0,29 | 0,12 | 0,03 | 0,44 |
| 25 % | 10 h | 2,89 | 1,16 | 0,35 | 4,39 |
| 25 % | 100 h | 28,88 | 11,55 | 3,47 | 43,89 |
| 50 % | 1 h | 0,37 | 0,15 | 0,04 | 0,56 |
| 50 % | 10 h | 3,68 | 1,47 | 0,44 | 5,59 |
| 50 % | 100 h | 36,75 | 14,7 | 4,41 | 55,86 |
| 75 % | 1 h | 0,45 | 0,18 | 0,05 | 0,68 |
| 75 % | 10 h | 4,46 | 1,79 | 0,54 | 6,78 |
| 75 % | 100 h | 44,63 | 17,85 | 5,36 | 67,83 |
| 100 % | 1 h | 0,53 | 0,21 | 0,06 | 0,8 |
| 100 % | 10 h | 5,25 | 2,1 | 0,63 | 7,98 |
| 100 % | 100 h | 52,5 | 21 | 6,3 | 79,8 |

### Semilla de tierra (fallas esperadas al iniciar cada vuelo)

| Desgaste | Fallas esperadas al inicio |
|---|---:|
| 0 % | 0,34 |
| 25 % | 0,47 |
| 50 % | 0,60 |
| 75 % | 0,72 |
| 100 % | 0,84 |

### Experiencia por vuelo (3 h)

| Desgaste | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |
|---|---:|---:|---:|---:|---:|
| 0 % | 0,96 | 61,6 % | 22,3 % | 7,3 % | **13 vuelos** |
| 25 % | 1,32 | 73,2 % | 29,3 % | 9,9 % | **10 vuelos** |
| 50 % | 1,68 | 81,3 % | 35,7 % | 12,4 % | **8 vuelos** |
| 75 % | 2,03 | 86,9 % | 41,5 % | 14,8 % | **6 vuelos** |
| 100 % | 2,39 | 90,9 % | 46,7 % | 17,2 % | **5 vuelos** |

## Perfil 7700 (nivel 4)

Multiplicador ×2,2 · desgaste máx ×4 · tope agravación 10 · tope tierra 5 · tregua crítica 30 min

### Fallas en vuelo por severidad

| Desgaste | Escala | Leves | Graves | Críticas | Total |
|---|---|---:|---:|---:|---:|
| 0 % | 1 h | 0,33 | 0,13 | 0,04 | 0,5 |
| 0 % | 10 h | 3,3 | 1,32 | 0,4 | 5,02 |
| 0 % | 100 h | 33 | 13,2 | 3,96 | 50,16 |
| 25 % | 1 h | 0,58 | 0,23 | 0,07 | 0,88 |
| 25 % | 10 h | 5,78 | 2,31 | 0,69 | 8,78 |
| 25 % | 100 h | 57,75 | 23,1 | 6,93 | 87,78 |
| 50 % | 1 h | 0,83 | 0,33 | 0,1 | 1,25 |
| 50 % | 10 h | 8,25 | 3,3 | 0,99 | 12,54 |
| 50 % | 100 h | 82,5 | 33 | 9,9 | 125,4 |
| 75 % | 1 h | 1,07 | 0,43 | 0,13 | 1,63 |
| 75 % | 10 h | 10,73 | 4,29 | 1,29 | 16,3 |
| 75 % | 100 h | 107,25 | 42,9 | 12,87 | 163,02 |
| 100 % | 1 h | 1,32 | 0,53 | 0,16 | 2,01 |
| 100 % | 10 h | 13,2 | 5,28 | 1,58 | 20,06 |
| 100 % | 100 h | 132 | 52,8 | 15,84 | 200,64 |

### Semilla de tierra (fallas esperadas al iniciar cada vuelo)

| Desgaste | Fallas esperadas al inicio |
|---|---:|
| 0 % | 0,54 |
| 25 % | 0,94 |
| 50 % | 1,34 |
| 75 % | 1,74 |
| 100 % | 2,12 |

### Experiencia por vuelo (3 h)

| Desgaste | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |
|---|---:|---:|---:|---:|---:|
| 0 % | 1,5 | 77,8 % | 32,7 % | 11,2 % | **8 vuelos** |
| 25 % | 2,63 | 92,8 % | 50,0 % | 18,8 % | **5 vuelos** |
| 50 % | 3,76 | 97,7 % | 62,8 % | 25,7 % | **3 vuelos** |
| 75 % | 4,89 | 99,2 % | 72,4 % | 32,0 % | **3 vuelos** |
| 100 % | 6,02 | 99,8 % | 79,5 % | 37,8 % | **2 vuelos** |

