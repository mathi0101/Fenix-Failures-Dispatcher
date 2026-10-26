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
| Tasa base por falla leve elegible en vuelo, eventos/h (`FailureRatePerHourBase`) | 0,2 |
| Probabilidad base por falla leve en la semilla de tierra (`GroundSeedBaseProbability`) | 0,001 |
| Multiplicador de tasa leves / graves / críticas (`SeverityRateMultiplier`) | 1,2 / 0,4 / 0,12 |
| Peso de agravamiento leves / graves / críticas (`AggravationWeights`) | 1 / 2 / 4 |
| Agravación extra por misma ATA simultánea (`SameAtaAggravationExtra`) | 1 |
| Intervalo de evaluación del motor, s (`EvaluationIntervalSeconds`) | 60 |
| Semilla de tierra habilitada (`GroundFailureEnabled`) | sí |
| Umbral de aviso de sistema castigado, % (`Wear:ThresholdNoticePct`) | 80 |

| Perfil | Multiplicador | Desgaste máx | Tope agravación | Tope tierra | Tregua crítica |
|---|---:|---:|---:|---:|---:|
| **Bajo** | ×0,6 | ×2 | 3 | 1 | — |
| **Moderado** | ×1 | ×2,5 | 5 | 2 | 120 min |
| **Alto** | ×2,5 | ×3 | 8 | 3 | 60 min |
| **7700** | ×5 | ×4 | 10 | 5 | 30 min |

## Experiencia por vuelo (3 h; desgaste 0 / 25 / 100 %)

_Lo que siente el jugador por vuelo típico —la vista de 10 h de las tablas de abajo es para comparar perfiles—. Tres niveles de desgaste para comparar cómo empeora con el uso._

| Perfil | Desgaste | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |
|---|---|---:|---:|---:|---:|---:|
| **Bajo** | 0 % | 0,58 | 43,8 % | 13,4 % | — | — |
| **Bajo** | 25 % | 0,72 | 51,3 % | 16,5 % | — | — |
| **Bajo** | 100 % | 1,15 | 68,4 % | 25,0 % | — | — |
| **Moderado** | 0 % | 1,03 | 64,4 % | 21,3 % | 6,9 % | **14 vuelos** |
| **Moderado** | 25 % | 1,42 | 75,8 % | 28,1 % | 9,4 % | **10 vuelos** |
| **Moderado** | 100 % | 2,58 | 92,4 % | 45,1 % | 16,5 % | **6 vuelos** |
| **Alto** | 0 % | 2,58 | 92,4 % | 45,1 % | 16,5 % | **6 vuelos** |
| **Alto** | 25 % | 3,87 | 97,9 % | 59,3 % | 23,7 % | **4 vuelos** |
| **Alto** | 100 % | 7,74 | 100,0 % | 83,5 % | 41,7 % | **2 vuelos** |
| **7700** | 0 % | 5,16 | 99,4 % | 69,9 % | 30,2 % | **3 vuelos** |
| **7700** | 25 % | 9,03 | 100,0 % | 87,8 % | 46,7 % | **2 vuelos** |
| **7700** | 100 % | 20,64 | 100,0 % | 99,2 % | 76,3 % | **1 vuelos** |

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
| 0 % | 1 h | 0,14 | 0,05 | 0 | 0,19 |
| 0 % | 10 h | 1,44 | 0,48 | 0 | 1,92 |
| 0 % | 100 h | 14,4 | 4,8 | 0 | 19,2 |
| 25 % | 1 h | 0,18 | 0,06 | 0 | 0,24 |
| 25 % | 10 h | 1,8 | 0,6 | 0 | 2,4 |
| 25 % | 100 h | 18 | 6 | 0 | 24 |
| 50 % | 1 h | 0,22 | 0,07 | 0 | 0,29 |
| 50 % | 10 h | 2,16 | 0,72 | 0 | 2,88 |
| 50 % | 100 h | 21,6 | 7,2 | 0 | 28,8 |
| 75 % | 1 h | 0,25 | 0,08 | 0 | 0,34 |
| 75 % | 10 h | 2,52 | 0,84 | 0 | 3,36 |
| 75 % | 100 h | 25,2 | 8,4 | 0 | 33,6 |
| 100 % | 1 h | 0,29 | 0,1 | 0 | 0,38 |
| 100 % | 10 h | 2,88 | 0,96 | 0 | 3,84 |
| 100 % | 100 h | 28,8 | 9,6 | 0 | 38,4 |

### Semilla de tierra (fallas esperadas al iniciar cada vuelo)

| Desgaste | Fallas esperadas al inicio |
|---|---:|
| 0 % | 0,15 |
| 25 % | 0,19 |
| 50 % | 0,22 |
| 75 % | 0,25 |
| 100 % | 0,28 |

### Experiencia por vuelo (3 h)

| Desgaste | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |
|---|---:|---:|---:|---:|---:|
| 0 % | 0,58 | 43,8 % | 13,4 % | — | — |
| 25 % | 0,72 | 51,3 % | 16,5 % | — | — |
| 50 % | 0,86 | 57,9 % | 19,4 % | — | — |
| 75 % | 1,01 | 63,5 % | 22,3 % | — | — |
| 100 % | 1,15 | 68,4 % | 25,0 % | — | — |

## Perfil Moderado (nivel 2)

Multiplicador ×1 · desgaste máx ×2,5 · tope agravación 5 · tope tierra 2 · tregua crítica 120 min

### Fallas en vuelo por severidad

| Desgaste | Escala | Leves | Graves | Críticas | Total |
|---|---|---:|---:|---:|---:|
| 0 % | 1 h | 0,24 | 0,08 | 0,02 | 0,34 |
| 0 % | 10 h | 2,4 | 0,8 | 0,24 | 3,44 |
| 0 % | 100 h | 24 | 8 | 2,4 | 34,4 |
| 25 % | 1 h | 0,33 | 0,11 | 0,03 | 0,47 |
| 25 % | 10 h | 3,3 | 1,1 | 0,33 | 4,73 |
| 25 % | 100 h | 33 | 11 | 3,3 | 47,3 |
| 50 % | 1 h | 0,42 | 0,14 | 0,04 | 0,6 |
| 50 % | 10 h | 4,2 | 1,4 | 0,42 | 6,02 |
| 50 % | 100 h | 42 | 14 | 4,2 | 60,2 |
| 75 % | 1 h | 0,51 | 0,17 | 0,05 | 0,73 |
| 75 % | 10 h | 5,1 | 1,7 | 0,51 | 7,31 |
| 75 % | 100 h | 51 | 17 | 5,1 | 73,1 |
| 100 % | 1 h | 0,6 | 0,2 | 0,06 | 0,86 |
| 100 % | 10 h | 6 | 2 | 0,6 | 8,6 |
| 100 % | 100 h | 60 | 20 | 6 | 86 |

### Semilla de tierra (fallas esperadas al iniciar cada vuelo)

| Desgaste | Fallas esperadas al inicio |
|---|---:|
| 0 % | 0,28 |
| 25 % | 0,38 |
| 50 % | 0,47 |
| 75 % | 0,57 |
| 100 % | 0,66 |

### Experiencia por vuelo (3 h)

| Desgaste | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |
|---|---:|---:|---:|---:|---:|
| 0 % | 1,03 | 64,4 % | 21,3 % | 6,9 % | **14 vuelos** |
| 25 % | 1,42 | 75,8 % | 28,1 % | 9,4 % | **10 vuelos** |
| 50 % | 1,81 | 83,6 % | 34,3 % | 11,8 % | **8 vuelos** |
| 75 % | 2,19 | 88,8 % | 40,0 % | 14,2 % | **7 vuelos** |
| 100 % | 2,58 | 92,4 % | 45,1 % | 16,5 % | **6 vuelos** |

## Perfil Alto (nivel 3)

Multiplicador ×2,5 · desgaste máx ×3 · tope agravación 8 · tope tierra 3 · tregua crítica 60 min

### Fallas en vuelo por severidad

| Desgaste | Escala | Leves | Graves | Críticas | Total |
|---|---|---:|---:|---:|---:|
| 0 % | 1 h | 0,6 | 0,2 | 0,06 | 0,86 |
| 0 % | 10 h | 6 | 2 | 0,6 | 8,6 |
| 0 % | 100 h | 60 | 20 | 6 | 86 |
| 25 % | 1 h | 0,9 | 0,3 | 0,09 | 1,29 |
| 25 % | 10 h | 9 | 3 | 0,9 | 12,9 |
| 25 % | 100 h | 90 | 30 | 9 | 129 |
| 50 % | 1 h | 1,2 | 0,4 | 0,12 | 1,72 |
| 50 % | 10 h | 12 | 4 | 1,2 | 17,2 |
| 50 % | 100 h | 120 | 40 | 12 | 172 |
| 75 % | 1 h | 1,5 | 0,5 | 0,15 | 2,15 |
| 75 % | 10 h | 15 | 5 | 1,5 | 21,5 |
| 75 % | 100 h | 150 | 50 | 15 | 215 |
| 100 % | 1 h | 1,8 | 0,6 | 0,18 | 2,58 |
| 100 % | 10 h | 18 | 6 | 1,8 | 25,8 |
| 100 % | 100 h | 180 | 60 | 18 | 258 |

### Semilla de tierra (fallas esperadas al iniciar cada vuelo)

| Desgaste | Fallas esperadas al inicio |
|---|---:|
| 0 % | 0,69 |
| 25 % | 1,02 |
| 50 % | 1,32 |
| 75 % | 1,60 |
| 100 % | 1,84 |

### Experiencia por vuelo (3 h)

| Desgaste | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |
|---|---:|---:|---:|---:|---:|
| 0 % | 2,58 | 92,4 % | 45,1 % | 16,5 % | **6 vuelos** |
| 25 % | 3,87 | 97,9 % | 59,3 % | 23,7 % | **4 vuelos** |
| 50 % | 5,16 | 99,4 % | 69,9 % | 30,2 % | **3 vuelos** |
| 75 % | 6,45 | 99,8 % | 77,7 % | 36,2 % | **2 vuelos** |
| 100 % | 7,74 | 100,0 % | 83,5 % | 41,7 % | **2 vuelos** |

## Perfil 7700 (nivel 4)

Multiplicador ×5 · desgaste máx ×4 · tope agravación 10 · tope tierra 5 · tregua crítica 30 min

### Fallas en vuelo por severidad

| Desgaste | Escala | Leves | Graves | Críticas | Total |
|---|---|---:|---:|---:|---:|
| 0 % | 1 h | 1,2 | 0,4 | 0,12 | 1,72 |
| 0 % | 10 h | 12 | 4 | 1,2 | 17,2 |
| 0 % | 100 h | 120 | 40 | 12 | 172 |
| 25 % | 1 h | 2,1 | 0,7 | 0,21 | 3,01 |
| 25 % | 10 h | 21 | 7 | 2,1 | 30,1 |
| 25 % | 100 h | 210 | 70 | 21 | 301 |
| 50 % | 1 h | 3 | 1 | 0,3 | 4,3 |
| 50 % | 10 h | 30 | 10 | 3 | 43 |
| 50 % | 100 h | 300 | 100 | 30 | 430 |
| 75 % | 1 h | 3,9 | 1,3 | 0,39 | 5,59 |
| 75 % | 10 h | 39 | 13 | 3,9 | 55,9 |
| 75 % | 100 h | 390 | 130 | 39 | 559 |
| 100 % | 1 h | 4,8 | 1,6 | 0,48 | 6,88 |
| 100 % | 10 h | 48 | 16 | 4,8 | 68,8 |
| 100 % | 100 h | 480 | 160 | 48 | 688 |

### Semilla de tierra (fallas esperadas al iniciar cada vuelo)

| Desgaste | Fallas esperadas al inicio |
|---|---:|
| 0 % | 1,39 |
| 25 % | 2,39 |
| 50 % | 3,24 |
| 75 % | 3,90 |
| 100 % | 4,35 |

### Experiencia por vuelo (3 h)

| Desgaste | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |
|---|---:|---:|---:|---:|---:|
| 0 % | 5,16 | 99,4 % | 69,9 % | 30,2 % | **3 vuelos** |
| 25 % | 9,03 | 100,0 % | 87,8 % | 46,7 % | **2 vuelos** |
| 50 % | 12,9 | 100,0 % | 95,0 % | 59,3 % | **1 vuelos** |
| 75 % | 16,77 | 100,0 % | 98,0 % | 69,0 % | **1 vuelos** |
| 100 % | 20,64 | 100,0 % | 99,2 % | 76,3 % | **1 vuelos** |

