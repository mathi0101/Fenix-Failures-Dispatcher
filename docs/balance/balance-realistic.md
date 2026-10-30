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
| **Bajo** | ×1 | ×2 | 3 | 1 | — |
| **Moderado** | ×1,5 | ×2,6 | 5 | 2 | 120 min |
| **Alto** | ×3,5 | ×3 | 8 | 3 | 60 min |
| **7700** | ×6 | ×3 | 10 | 5 | 30 min |

## Experiencia por vuelo (3 h; desgaste 0 / 25 / 100 %)

_Lo que siente el jugador por vuelo típico —la vista de 10 h de las tablas de abajo es para comparar perfiles—. Tres niveles de desgaste para comparar cómo empeora con el uso._

| Perfil | Desgaste | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |
|---|---|---:|---:|---:|---:|---:|
| **Bajo** | 0 % | 0,21 | 18,9 % | 5,8 % | — | — |
| **Bajo** | 25 % | 0,26 | 23,1 % | 7,2 % | — | — |
| **Bajo** | 100 % | 0,42 | 34,3 % | 11,3 % | — | — |
| **Moderado** | 0 % | 0,35 | 29,3 % | 8,6 % | 3,1 % | **32 vuelos** |
| **Moderado** | 25 % | 0,49 | 38,4 % | 11,8 % | 4,3 % | **23 vuelos** |
| **Moderado** | 100 % | 0,9 | 59,4 % | 20,9 % | 7,9 % | **12 vuelos** |
| **Alto** | 0 % | 0,81 | 55,4 % | 18,9 % | 7,1 % | **14 vuelos** |
| **Alto** | 25 % | 1,21 | 70,3 % | 27,0 % | 10,4 % | **9 vuelos** |
| **Alto** | 100 % | 2,43 | 91,2 % | 46,7 % | 19,8 % | **5 vuelos** |
| **7700** | 0 % | 1,39 | 75,0 % | 30,2 % | 11,8 % | **8 vuelos** |
| **7700** | 25 % | 2,08 | 87,5 % | 41,7 % | 17,2 % | **5 vuelos** |
| **7700** | 100 % | 4,16 | 98,4 % | 66,0 % | 31,5 % | **3 vuelos** |

### Ancla temporal del desgaste

Vuelos de 3 h que tarda un sistema en acumular cada nivel de desgaste, según su tasa (`WearPerFlightHour`; el seed va de 0,1 a 0,6 %/h):

| Tasa del sistema | Hasta 25 % | Hasta 50 % | Hasta 80 % (aviso) |
|---|---:|---:|---:|
| 0,1 %/h | 84 | 167 | 267 |
| 0,3 %/h | 28 | 56 | 89 |
| 0,6 %/h | 14 | 28 | 45 |

## Perfil Bajo (nivel 1)

Multiplicador ×1 · desgaste máx ×2 · tope agravación 3 · tope tierra 1 · tregua crítica —

### Fallas en vuelo por severidad

| Desgaste | Escala | Leves | Graves | Críticas | Total |
|---|---|---:|---:|---:|---:|
| 0 % | 1 h | 0,05 | 0,02 | 0 | 0,07 |
| 0 % | 10 h | 0,5 | 0,2 | 0 | 0,7 |
| 0 % | 100 h | 5 | 2 | 0 | 7 |
| 25 % | 1 h | 0,06 | 0,03 | 0 | 0,09 |
| 25 % | 10 h | 0,63 | 0,25 | 0 | 0,88 |
| 25 % | 100 h | 6,25 | 2,5 | 0 | 8,75 |
| 50 % | 1 h | 0,08 | 0,03 | 0 | 0,11 |
| 50 % | 10 h | 0,75 | 0,3 | 0 | 1,05 |
| 50 % | 100 h | 7,5 | 3 | 0 | 10,5 |
| 75 % | 1 h | 0,09 | 0,04 | 0 | 0,12 |
| 75 % | 10 h | 0,88 | 0,35 | 0 | 1,23 |
| 75 % | 100 h | 8,75 | 3,5 | 0 | 12,25 |
| 100 % | 1 h | 0,1 | 0,04 | 0 | 0,14 |
| 100 % | 10 h | 1 | 0,4 | 0 | 1,4 |
| 100 % | 100 h | 10 | 4 | 0 | 14 |

### Semilla de tierra (fallas esperadas al iniciar cada vuelo)

| Desgaste | Fallas esperadas al inicio |
|---|---:|
| 0 % | 0,77 |
| 25 % | 0,84 |
| 50 % | 0,89 |
| 75 % | 0,92 |
| 100 % | 0,95 |

### Experiencia por vuelo (3 h)

| Desgaste | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |
|---|---:|---:|---:|---:|---:|
| 0 % | 0,21 | 18,9 % | 5,8 % | — | — |
| 25 % | 0,26 | 23,1 % | 7,2 % | — | — |
| 50 % | 0,32 | 27,0 % | 8,6 % | — | — |
| 75 % | 0,37 | 30,8 % | 10,0 % | — | — |
| 100 % | 0,42 | 34,3 % | 11,3 % | — | — |

## Perfil Moderado (nivel 2)

Multiplicador ×1,5 · desgaste máx ×2,6 · tope agravación 5 · tope tierra 2 · tregua crítica 120 min

### Fallas en vuelo por severidad

| Desgaste | Escala | Leves | Graves | Críticas | Total |
|---|---|---:|---:|---:|---:|
| 0 % | 1 h | 0,08 | 0,03 | 0,01 | 0,12 |
| 0 % | 10 h | 0,75 | 0,3 | 0,11 | 1,16 |
| 0 % | 100 h | 7,5 | 3 | 1,05 | 11,55 |
| 25 % | 1 h | 0,11 | 0,04 | 0,01 | 0,16 |
| 25 % | 10 h | 1,05 | 0,42 | 0,15 | 1,62 |
| 25 % | 100 h | 10,5 | 4,2 | 1,47 | 16,17 |
| 50 % | 1 h | 0,14 | 0,05 | 0,02 | 0,21 |
| 50 % | 10 h | 1,35 | 0,54 | 0,19 | 2,08 |
| 50 % | 100 h | 13,5 | 5,4 | 1,89 | 20,79 |
| 75 % | 1 h | 0,17 | 0,07 | 0,02 | 0,25 |
| 75 % | 10 h | 1,65 | 0,66 | 0,23 | 2,54 |
| 75 % | 100 h | 16,5 | 6,6 | 2,31 | 25,41 |
| 100 % | 1 h | 0,2 | 0,08 | 0,03 | 0,3 |
| 100 % | 10 h | 1,95 | 0,78 | 0,27 | 3 |
| 100 % | 100 h | 19,5 | 7,8 | 2,73 | 30,03 |

### Semilla de tierra (fallas esperadas al iniciar cada vuelo)

| Desgaste | Fallas esperadas al inicio |
|---|---:|
| 0 % | 1,54 |
| 25 % | 1,77 |
| 50 % | 1,89 |
| 75 % | 1,95 |
| 100 % | 1,97 |

### Experiencia por vuelo (3 h)

| Desgaste | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |
|---|---:|---:|---:|---:|---:|
| 0 % | 0,35 | 29,3 % | 8,6 % | 3,1 % | **32 vuelos** |
| 25 % | 0,49 | 38,4 % | 11,8 % | 4,3 % | **23 vuelos** |
| 50 % | 0,62 | 46,4 % | 15,0 % | 5,5 % | **18 vuelos** |
| 75 % | 0,76 | 53,3 % | 18,0 % | 6,7 % | **14 vuelos** |
| 100 % | 0,9 | 59,4 % | 20,9 % | 7,9 % | **12 vuelos** |

## Perfil Alto (nivel 3)

Multiplicador ×3,5 · desgaste máx ×3 · tope agravación 8 · tope tierra 3 · tregua crítica 60 min

### Fallas en vuelo por severidad

| Desgaste | Escala | Leves | Graves | Críticas | Total |
|---|---|---:|---:|---:|---:|
| 0 % | 1 h | 0,18 | 0,07 | 0,02 | 0,27 |
| 0 % | 10 h | 1,75 | 0,7 | 0,25 | 2,7 |
| 0 % | 100 h | 17,5 | 7 | 2,45 | 26,95 |
| 25 % | 1 h | 0,26 | 0,11 | 0,04 | 0,4 |
| 25 % | 10 h | 2,63 | 1,05 | 0,37 | 4,04 |
| 25 % | 100 h | 26,25 | 10,5 | 3,68 | 40,43 |
| 50 % | 1 h | 0,35 | 0,14 | 0,05 | 0,54 |
| 50 % | 10 h | 3,5 | 1,4 | 0,49 | 5,39 |
| 50 % | 100 h | 35 | 14 | 4,9 | 53,9 |
| 75 % | 1 h | 0,44 | 0,18 | 0,06 | 0,67 |
| 75 % | 10 h | 4,38 | 1,75 | 0,61 | 6,74 |
| 75 % | 100 h | 43,75 | 17,5 | 6,13 | 67,38 |
| 100 % | 1 h | 0,53 | 0,21 | 0,07 | 0,81 |
| 100 % | 10 h | 5,25 | 2,1 | 0,74 | 8,09 |
| 100 % | 100 h | 52,5 | 21 | 7,35 | 80,85 |

### Semilla de tierra (fallas esperadas al iniciar cada vuelo)

| Desgaste | Fallas esperadas al inicio |
|---|---:|
| 0 % | 2,84 |
| 25 % | 2,98 |
| 50 % | 3,00 |
| 75 % | 3,00 |
| 100 % | 3,00 |

### Experiencia por vuelo (3 h)

| Desgaste | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |
|---|---:|---:|---:|---:|---:|
| 0 % | 0,81 | 55,4 % | 18,9 % | 7,1 % | **14 vuelos** |
| 25 % | 1,21 | 70,3 % | 27,0 % | 10,4 % | **9 vuelos** |
| 50 % | 1,62 | 80,2 % | 34,3 % | 13,7 % | **7 vuelos** |
| 75 % | 2,02 | 86,8 % | 40,8 % | 16,8 % | **5 vuelos** |
| 100 % | 2,43 | 91,2 % | 46,7 % | 19,8 % | **5 vuelos** |

## Perfil 7700 (nivel 4)

Multiplicador ×6 · desgaste máx ×3 · tope agravación 10 · tope tierra 5 · tregua crítica 30 min

### Fallas en vuelo por severidad

| Desgaste | Escala | Leves | Graves | Críticas | Total |
|---|---|---:|---:|---:|---:|
| 0 % | 1 h | 0,3 | 0,12 | 0,04 | 0,46 |
| 0 % | 10 h | 3 | 1,2 | 0,42 | 4,62 |
| 0 % | 100 h | 30 | 12 | 4,2 | 46,2 |
| 25 % | 1 h | 0,45 | 0,18 | 0,06 | 0,69 |
| 25 % | 10 h | 4,5 | 1,8 | 0,63 | 6,93 |
| 25 % | 100 h | 45 | 18 | 6,3 | 69,3 |
| 50 % | 1 h | 0,6 | 0,24 | 0,08 | 0,92 |
| 50 % | 10 h | 6 | 2,4 | 0,84 | 9,24 |
| 50 % | 100 h | 60 | 24 | 8,4 | 92,4 |
| 75 % | 1 h | 0,75 | 0,3 | 0,11 | 1,16 |
| 75 % | 10 h | 7,5 | 3 | 1,05 | 11,55 |
| 75 % | 100 h | 75 | 30 | 10,5 | 115,5 |
| 100 % | 1 h | 0,9 | 0,36 | 0,13 | 1,39 |
| 100 % | 10 h | 9 | 3,6 | 1,26 | 13,86 |
| 100 % | 100 h | 90 | 36 | 12,6 | 138,6 |

### Semilla de tierra (fallas esperadas al iniciar cada vuelo)

| Desgaste | Fallas esperadas al inicio |
|---|---:|
| 0 % | 4,90 |
| 25 % | 5,00 |
| 50 % | 5,00 |
| 75 % | 5,00 |
| 100 % | 5,00 |

### Experiencia por vuelo (3 h)

| Desgaste | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |
|---|---:|---:|---:|---:|---:|
| 0 % | 1,39 | 75,0 % | 30,2 % | 11,8 % | **8 vuelos** |
| 25 % | 2,08 | 87,5 % | 41,7 % | 17,2 % | **5 vuelos** |
| 50 % | 2,77 | 93,7 % | 51,3 % | 22,3 % | **4 vuelos** |
| 75 % | 3,47 | 96,9 % | 59,3 % | 27,0 % | **3 vuelos** |
| 100 % | 4,16 | 98,4 % | 66,0 % | 31,5 % | **3 vuelos** |

