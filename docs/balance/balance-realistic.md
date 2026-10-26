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
| **Moderado** | ×1 | ×3 | 5 | 2 | 120 min |
| **Alto** | ×1,5 | ×4 | 8 | 3 | 60 min |
| **7700** | ×2,5 | ×5,5 | 10 | 5 | 30 min |

## Experiencia por vuelo (1,5 h, desgaste 25 %)

_Lo que siente el jugador por vuelo típico (la vista de 10 h de las tablas de abajo es para comparar perfiles)._

| Perfil | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |
|---|---:|---:|---:|---:|---:|
| **Bajo** | 0,36 | 30,2 % | 8,6 % | — | — |
| **Moderado** | 0,77 | 53,9 % | 16,5 % | 5,3 % | **19 vuelos** |
| **Alto** | 1,35 | 74,2 % | 27,0 % | 9,0 % | **11 vuelos** |
| **7700** | 2,74 | 93,6 % | 47,1 % | 17,4 % | **5 vuelos** |

### Ancla temporal del desgaste

Vuelos de 1,5 h que tarda un sistema en acumular cada nivel de desgaste, según su tasa (`WearPerFlightHour`; el seed va de 0,1 a 0,6 %/h):

| Tasa del sistema | Hasta 25 % | Hasta 50 % | Hasta 80 % (aviso) |
|---|---:|---:|---:|
| 0,1 %/h | 167 | 334 | 534 |
| 0,3 %/h | 56 | 112 | 178 |
| 0,6 %/h | 28 | 56 | 89 |

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

## Perfil Moderado (nivel 2)

Multiplicador ×1 · desgaste máx ×3 · tope agravación 5 · tope tierra 2 · tregua crítica 120 min

### Fallas en vuelo por severidad

| Desgaste | Escala | Leves | Graves | Críticas | Total |
|---|---|---:|---:|---:|---:|
| 0 % | 1 h | 0,24 | 0,08 | 0,02 | 0,34 |
| 0 % | 10 h | 2,4 | 0,8 | 0,24 | 3,44 |
| 0 % | 100 h | 24 | 8 | 2,4 | 34,4 |
| 25 % | 1 h | 0,36 | 0,12 | 0,04 | 0,52 |
| 25 % | 10 h | 3,6 | 1,2 | 0,36 | 5,16 |
| 25 % | 100 h | 36 | 12 | 3,6 | 51,6 |
| 50 % | 1 h | 0,48 | 0,16 | 0,05 | 0,69 |
| 50 % | 10 h | 4,8 | 1,6 | 0,48 | 6,88 |
| 50 % | 100 h | 48 | 16 | 4,8 | 68,8 |
| 75 % | 1 h | 0,6 | 0,2 | 0,06 | 0,86 |
| 75 % | 10 h | 6 | 2 | 0,6 | 8,6 |
| 75 % | 100 h | 60 | 20 | 6 | 86 |
| 100 % | 1 h | 0,72 | 0,24 | 0,07 | 1,03 |
| 100 % | 10 h | 7,2 | 2,4 | 0,72 | 10,32 |
| 100 % | 100 h | 72 | 24 | 7,2 | 103,2 |

### Semilla de tierra (fallas esperadas al iniciar cada vuelo)

| Desgaste | Fallas esperadas al inicio |
|---|---:|
| 0 % | 0,28 |
| 25 % | 0,41 |
| 50 % | 0,54 |
| 75 % | 0,66 |
| 100 % | 0,77 |

## Perfil Alto (nivel 3)

Multiplicador ×1,5 · desgaste máx ×4 · tope agravación 8 · tope tierra 3 · tregua crítica 60 min

### Fallas en vuelo por severidad

| Desgaste | Escala | Leves | Graves | Críticas | Total |
|---|---|---:|---:|---:|---:|
| 0 % | 1 h | 0,36 | 0,12 | 0,04 | 0,52 |
| 0 % | 10 h | 3,6 | 1,2 | 0,36 | 5,16 |
| 0 % | 100 h | 36 | 12 | 3,6 | 51,6 |
| 25 % | 1 h | 0,63 | 0,21 | 0,06 | 0,9 |
| 25 % | 10 h | 6,3 | 2,1 | 0,63 | 9,03 |
| 25 % | 100 h | 63 | 21 | 6,3 | 90,3 |
| 50 % | 1 h | 0,9 | 0,3 | 0,09 | 1,29 |
| 50 % | 10 h | 9 | 3 | 0,9 | 12,9 |
| 50 % | 100 h | 90 | 30 | 9 | 129 |
| 75 % | 1 h | 1,17 | 0,39 | 0,12 | 1,68 |
| 75 % | 10 h | 11,7 | 3,9 | 1,17 | 16,77 |
| 75 % | 100 h | 117 | 39 | 11,7 | 167,7 |
| 100 % | 1 h | 1,44 | 0,48 | 0,14 | 2,06 |
| 100 % | 10 h | 14,4 | 4,8 | 1,44 | 20,64 |
| 100 % | 100 h | 144 | 48 | 14,4 | 206,4 |

### Semilla de tierra (fallas esperadas al iniciar cada vuelo)

| Desgaste | Fallas esperadas al inicio |
|---|---:|
| 0 % | 0,42 |
| 25 % | 0,73 |
| 50 % | 1,02 |
| 75 % | 1,30 |
| 100 % | 1,55 |

## Perfil 7700 (nivel 4)

Multiplicador ×2,5 · desgaste máx ×5,5 · tope agravación 10 · tope tierra 5 · tregua crítica 30 min

### Fallas en vuelo por severidad

| Desgaste | Escala | Leves | Graves | Críticas | Total |
|---|---|---:|---:|---:|---:|
| 0 % | 1 h | 0,6 | 0,2 | 0,06 | 0,86 |
| 0 % | 10 h | 6 | 2 | 0,6 | 8,6 |
| 0 % | 100 h | 60 | 20 | 6 | 86 |
| 25 % | 1 h | 1,28 | 0,43 | 0,13 | 1,83 |
| 25 % | 10 h | 12,75 | 4,25 | 1,28 | 18,28 |
| 25 % | 100 h | 127,5 | 42,5 | 12,75 | 182,75 |
| 50 % | 1 h | 1,95 | 0,65 | 0,2 | 2,8 |
| 50 % | 10 h | 19,5 | 6,5 | 1,95 | 27,95 |
| 50 % | 100 h | 195 | 65 | 19,5 | 279,5 |
| 75 % | 1 h | 2,63 | 0,88 | 0,26 | 3,76 |
| 75 % | 10 h | 26,25 | 8,75 | 2,63 | 37,63 |
| 75 % | 100 h | 262,5 | 87,5 | 26,25 | 376,25 |
| 100 % | 1 h | 3,3 | 1,1 | 0,33 | 4,73 |
| 100 % | 10 h | 33 | 11 | 3,3 | 47,3 |
| 100 % | 100 h | 330 | 110 | 33 | 473 |

### Semilla de tierra (fallas esperadas al iniciar cada vuelo)

| Desgaste | Fallas esperadas al inicio |
|---|---:|
| 0 % | 0,70 |
| 25 % | 1,48 |
| 50 % | 2,23 |
| 75 % | 2,91 |
| 100 % | 3,49 |

