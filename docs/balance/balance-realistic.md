# Balance del modo Realistic — generado automáticamente

_Generado por `RealisticBalanceReportTests` con el código vigente del motor (FailureEngineSettings con sus perfiles; catálogo curado: 173 leves / 179 graves / 32 críticas). Regenerar: `docs/balance/balance.cmd` (herramienta manual, exige `RFF_BALANCE=1`). Versión formateada: `balance-realistic.xlsx`._

## Supuestos de la estimación

- Tasa en vuelo por severidad = `FailureRatePerHourBase` × multiplicador de severidad × multiplicador del perfil × factor de desgaste. Estimación global por severidad: no pondera por fase ni por la composición de presets.
- Terminología: **leves** = Minor, **graves** = Major, **críticas** = Critical.
- La semilla de tierra es un evento por vuelo (arranque de sesión); **no escala con las horas** y el motor solo arma fallas leves reparables ahí. El total esperado es λ₀ (`GroundSeedLambdaBase`) × multiplicador del perfil × desgaste, truncado por el tope del perfil: el catálogo elegible solo reparte la probabilidad entre candidatas (cuáles salen), nunca escala el total (cuántas).
- Cada vuelo sortea su semilla: arrancar **limpio (0 fallas) siempre tiene probabilidad** — P(0) = e^(−λ) — y el máximo por vuelo es el tope del perfil. La media de cada fila no es un resultado garantizado: es el promedio del sorteo sobre muchos vuelos.
- Perfil Bajo: el motor descarta fallas críticas, por lo que su cifra de críticas es siempre 0.
- La cadencia real la decide el motor tick a tick; esto es una estimación de presentación.
- Desgaste: al cerrar cada sesión, los sistemas sorteados (1,5 sistemas/h, mínimo 3) acumulan su `WearPerFlightHour` × horas de vuelo. La tabla de desgaste por horas promedia ese sorteo con vuelos de referencia.

## Configuración con la que se generó este reporte

_Defaults de código al generar el archivo (`FailureEngineSettings` y perfiles del catálogo — el reporte no lee appsettings). La tabla de desgaste por horas aplica el multiplicador global ×0,8. Si cambiaron parámetros, regenerar para reflejarlos._

| Parámetro | Valor |
|---|---:|
| Tasa base por falla leve elegible en vuelo, eventos/h (`FailureRatePerHourBase`) | 0,1 |
| Semillas de tierra esperadas por vuelo, λ₀ (`GroundSeedLambdaBase`) | 0,6 |
| Multiplicador de tasa leves / graves / críticas (`SeverityRateMultiplier`) | 1 / 0,4 / 0,14 |
| Peso de agravamiento leves / graves / críticas (`AggravationWeights`) | 1 / 2 / 4 |
| Agravación extra por misma ATA simultánea (`SameAtaAggravationExtra`) | 1 |
| Intervalo de evaluación del motor, s (`EvaluationIntervalSeconds`) | 60 |
| Semilla de tierra habilitada (`GroundFailureEnabled`) | sí |
| Multiplicador global de desgaste (`Wear:WearRateMultiplier`) | 0,8 |
| Umbral de aviso de sistema castigado, % (`Wear:ThresholdNoticePct`) | 80 |

| Perfil | Multiplicador | Desgaste máx | Tope agravación | Tope tierra | Tregua crítica |
|---|---:|---:|---:|---:|---:|
| **Bajo** | ×0,6 | ×3 | 3 | 1 | — |
| **Moderado** | ×1 | ×3 | 5 | 2 | 180 min |
| **Alto** | ×2,6 | ×3 | 8 | 3 | 120 min |
| **7700** | ×4 | ×3 | 10 | 5 | 60 min |

## Experiencia por vuelo (3 h; desgaste 0 / 25 / 100 %)

_Lo que siente el jugador por vuelo típico —la vista de 10 h de las tablas de abajo es para comparar perfiles—. Tres niveles de desgaste para comparar cómo empeora con el uso._

| Perfil | Desgaste | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |
|---|---|---:|---:|---:|---:|---:|
| **Bajo** | 0 % | 0,25 | 22,3 % | 6,9 % | — | — |
| **Bajo** | 25 % | 0,38 | 31,5 % | 10,2 % | — | — |
| **Bajo** | 100 % | 0,76 | 53,0 % | 19,4 % | — | — |
| **Moderado** | 0 % | 0,46 | 37,0 % | 11,3 % | 4,1 % | **24 vuelos** |
| **Moderado** | 25 % | 0,69 | 50,0 % | 16,5 % | 6,1 % | **16 vuelos** |
| **Moderado** | 100 % | 1,39 | 75,0 % | 30,2 % | 11,8 % | **8 vuelos** |
| **Alto** | 0 % | 1,2 | 69,9 % | 26,8 % | 10,3 % | **9 vuelos** |
| **Alto** | 25 % | 1,8 | 83,5 % | 37,4 % | 15,1 % | **6 vuelos** |
| **Alto** | 100 % | 3,6 | 97,3 % | 60,8 % | 27,9 % | **3 vuelos** |
| **7700** | 0 % | 1,85 | 84,2 % | 38,1 % | 15,5 % | **6 vuelos** |
| **7700** | 25 % | 2,77 | 93,7 % | 51,3 % | 22,3 % | **4 vuelos** |
| **7700** | 100 % | 5,54 | 99,6 % | 76,3 % | 39,6 % | **2 vuelos** |

### Desgaste por horas de vuelo (desgaste ×0,8; 25 / 50 / 80 aviso / 100 %)

El desgaste acumula la tasa del sistema × horas de vuelo **solo en los sistemas sorteados** al cerrar cada sesión: con vuelos de 3 h salen 5 de 19 sistemas (escala 1,5 sistemas/h, mínimo 3). Horas de vuelo esperadas de la aeronave hasta cada nivel, por tasa del seed (`WearPerFlightHour`, 0,38–2,5 %/h); entre paréntesis, vuelos de 3 h. Multiplicador global de desgaste activo: ×0,8.

| Tasa del sistema | Hasta 25 % | Hasta 50 % | Hasta 80 % (aviso) | Hasta 100 % |
|---|---:|---:|---:|---:|
| 0,38 %/h (4 sist.) | 316,67 h (106 vuelos) | 633,33 h (211 vuelos) | 1013,33 h (338 vuelos) | 1266,67 h (422 vuelos) |
| 0,75 %/h (3 sist.) | 158,33 h (53 vuelos) | 316,67 h (106 vuelos) | 506,67 h (169 vuelos) | 633,33 h (211 vuelos) |
| 1 %/h (5 sist.) | 118,75 h (40 vuelos) | 237,5 h (79 vuelos) | 380 h (127 vuelos) | 475 h (158 vuelos) |
| 1,25 %/h (2 sist.) | 95 h (32 vuelos) | 190 h (63 vuelos) | 304 h (101 vuelos) | 380 h (127 vuelos) |
| 1,75 %/h (1 sist.) | 67,86 h (23 vuelos) | 135,71 h (45 vuelos) | 217,14 h (72 vuelos) | 271,43 h (90 vuelos) |
| 1,88 %/h (3 sist.) | 63,33 h (21 vuelos) | 126,67 h (42 vuelos) | 202,67 h (68 vuelos) | 253,33 h (84 vuelos) |
| 2,5 %/h (1 sist.) | 47,5 h (16 vuelos) | 95 h (32 vuelos) | 152 h (51 vuelos) | 190 h (63 vuelos) |

## Perfil Bajo (nivel 1)

Multiplicador ×0,6 · desgaste máx ×3 · tope agravación 3 · tope tierra 1 · tregua crítica —

### Fallas en vuelo por severidad

| Desgaste | Escala | Leves | Graves | Críticas | Total |
|---|---|---:|---:|---:|---:|
| 0 % | 1 h | 0,06 | 0,02 | 0 | 0,08 |
| 0 % | 10 h | 0,6 | 0,24 | 0 | 0,84 |
| 0 % | 100 h | 6 | 2,4 | 0 | 8,4 |
| 25 % | 1 h | 0,09 | 0,04 | 0 | 0,13 |
| 25 % | 10 h | 0,9 | 0,36 | 0 | 1,26 |
| 25 % | 100 h | 9 | 3,6 | 0 | 12,6 |
| 50 % | 1 h | 0,12 | 0,05 | 0 | 0,17 |
| 50 % | 10 h | 1,2 | 0,48 | 0 | 1,68 |
| 50 % | 100 h | 12 | 4,8 | 0 | 16,8 |
| 75 % | 1 h | 0,15 | 0,06 | 0 | 0,21 |
| 75 % | 10 h | 1,5 | 0,6 | 0 | 2,1 |
| 75 % | 100 h | 15 | 6 | 0 | 21 |
| 100 % | 1 h | 0,18 | 0,07 | 0 | 0,25 |
| 100 % | 10 h | 1,8 | 0,72 | 0 | 2,52 |
| 100 % | 100 h | 18 | 7,2 | 0 | 25,2 |

### Semilla de tierra al iniciar cada vuelo (distribución)

Cada vuelo **sortea** su arranque entre 0 y el tope del perfil: P(k) es la probabilidad de arrancar con exactamente k fallas de tierra y P(0) la de arrancar limpio. La media resume el conjunto; no es lo que ocurre en cada vuelo.

| Desgaste | P(0) | P(1) | Media |
|---|---:|---:|---:|
| 0 % | 69,8 % | 30,2 % | 0,30 |
| 25 % | 58,3 % | 41,7 % | 0,42 |
| 50 % | 48,7 % | 51,3 % | 0,51 |
| 75 % | 40,7 % | 59,3 % | 0,59 |
| 100 % | 34,0 % | 66,0 % | 0,66 |

### Experiencia por vuelo (3 h)

| Desgaste | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |
|---|---:|---:|---:|---:|---:|
| 0 % | 0,25 | 22,3 % | 6,9 % | — | — |
| 25 % | 0,38 | 31,5 % | 10,2 % | — | — |
| 50 % | 0,5 | 39,6 % | 13,4 % | — | — |
| 75 % | 0,63 | 46,7 % | 16,5 % | — | — |
| 100 % | 0,76 | 53,0 % | 19,4 % | — | — |

## Perfil Moderado (nivel 2)

Multiplicador ×1 · desgaste máx ×3 · tope agravación 5 · tope tierra 2 · tregua crítica 180 min

### Fallas en vuelo por severidad

| Desgaste | Escala | Leves | Graves | Críticas | Total |
|---|---|---:|---:|---:|---:|
| 0 % | 1 h | 0,1 | 0,04 | 0,01 | 0,15 |
| 0 % | 10 h | 1 | 0,4 | 0,14 | 1,54 |
| 0 % | 100 h | 10 | 4 | 1,4 | 15,4 |
| 25 % | 1 h | 0,15 | 0,06 | 0,02 | 0,23 |
| 25 % | 10 h | 1,5 | 0,6 | 0,21 | 2,31 |
| 25 % | 100 h | 15 | 6 | 2,1 | 23,1 |
| 50 % | 1 h | 0,2 | 0,08 | 0,03 | 0,31 |
| 50 % | 10 h | 2 | 0,8 | 0,28 | 3,08 |
| 50 % | 100 h | 20 | 8 | 2,8 | 30,8 |
| 75 % | 1 h | 0,25 | 0,1 | 0,04 | 0,39 |
| 75 % | 10 h | 2,5 | 1 | 0,35 | 3,85 |
| 75 % | 100 h | 25 | 10 | 3,5 | 38,5 |
| 100 % | 1 h | 0,3 | 0,12 | 0,04 | 0,46 |
| 100 % | 10 h | 3 | 1,2 | 0,42 | 4,62 |
| 100 % | 100 h | 30 | 12 | 4,2 | 46,2 |

### Semilla de tierra al iniciar cada vuelo (distribución)

Cada vuelo **sortea** su arranque entre 0 y el tope del perfil: P(k) es la probabilidad de arrancar con exactamente k fallas de tierra y P(0) la de arrancar limpio. La media resume el conjunto; no es lo que ocurre en cada vuelo.

| Desgaste | P(0) | P(1) | P(2) | Media |
|---|---:|---:|---:|---:|
| 0 % | 54,9 % | 32,9 % | 12,2 % | 0,57 |
| 25 % | 40,7 % | 36,6 % | 22,8 % | 0,82 |
| 50 % | 30,1 % | 36,1 % | 33,7 % | 1,04 |
| 75 % | 22,3 % | 33,5 % | 44,2 % | 1,22 |
| 100 % | 16,5 % | 29,8 % | 53,7 % | 1,37 |

### Experiencia por vuelo (3 h)

| Desgaste | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |
|---|---:|---:|---:|---:|---:|
| 0 % | 0,46 | 37,0 % | 11,3 % | 4,1 % | **24 vuelos** |
| 25 % | 0,69 | 50,0 % | 16,5 % | 6,1 % | **16 vuelos** |
| 50 % | 0,92 | 60,3 % | 21,3 % | 8,1 % | **12 vuelos** |
| 75 % | 1,16 | 68,5 % | 25,9 % | 10,0 % | **10 vuelos** |
| 100 % | 1,39 | 75,0 % | 30,2 % | 11,8 % | **8 vuelos** |

## Perfil Alto (nivel 3)

Multiplicador ×2,6 · desgaste máx ×3 · tope agravación 8 · tope tierra 3 · tregua crítica 120 min

### Fallas en vuelo por severidad

| Desgaste | Escala | Leves | Graves | Críticas | Total |
|---|---|---:|---:|---:|---:|
| 0 % | 1 h | 0,26 | 0,1 | 0,04 | 0,4 |
| 0 % | 10 h | 2,6 | 1,04 | 0,36 | 4 |
| 0 % | 100 h | 26 | 10,4 | 3,64 | 40,04 |
| 25 % | 1 h | 0,39 | 0,16 | 0,05 | 0,6 |
| 25 % | 10 h | 3,9 | 1,56 | 0,55 | 6,01 |
| 25 % | 100 h | 39 | 15,6 | 5,46 | 60,06 |
| 50 % | 1 h | 0,52 | 0,21 | 0,07 | 0,8 |
| 50 % | 10 h | 5,2 | 2,08 | 0,73 | 8,01 |
| 50 % | 100 h | 52 | 20,8 | 7,28 | 80,08 |
| 75 % | 1 h | 0,65 | 0,26 | 0,09 | 1 |
| 75 % | 10 h | 6,5 | 2,6 | 0,91 | 10,01 |
| 75 % | 100 h | 65 | 26 | 9,1 | 100,1 |
| 100 % | 1 h | 0,78 | 0,31 | 0,11 | 1,2 |
| 100 % | 10 h | 7,8 | 3,12 | 1,09 | 12,01 |
| 100 % | 100 h | 78 | 31,2 | 10,92 | 120,12 |

### Semilla de tierra al iniciar cada vuelo (distribución)

Cada vuelo **sortea** su arranque entre 0 y el tope del perfil: P(k) es la probabilidad de arrancar con exactamente k fallas de tierra y P(0) la de arrancar limpio. La media resume el conjunto; no es lo que ocurre en cada vuelo.

| Desgaste | P(0) | P(1) | P(2) | P(3) | Media |
|---|---:|---:|---:|---:|---:|
| 0 % | 21,0 % | 32,8 % | 25,6 % | 20,6 % | 1,46 |
| 25 % | 9,6 % | 22,5 % | 26,4 % | 41,5 % | 2,00 |
| 50 % | 4,4 % | 13,8 % | 21,5 % | 60,3 % | 2,38 |
| 75 % | 2,0 % | 7,9 % | 15,4 % | 74,7 % | 2,63 |
| 100 % | 0,9 % | 4,3 % | 10,2 % | 84,6 % | 2,78 |

### Experiencia por vuelo (3 h)

| Desgaste | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |
|---|---:|---:|---:|---:|---:|
| 0 % | 1,2 | 69,9 % | 26,8 % | 10,3 % | **9 vuelos** |
| 25 % | 1,8 | 83,5 % | 37,4 % | 15,1 % | **6 vuelos** |
| 50 % | 2,4 | 90,9 % | 46,4 % | 19,6 % | **5 vuelos** |
| 75 % | 3 | 95,0 % | 54,2 % | 23,9 % | **4 vuelos** |
| 100 % | 3,6 | 97,3 % | 60,8 % | 27,9 % | **3 vuelos** |

## Perfil 7700 (nivel 4)

Multiplicador ×4 · desgaste máx ×3 · tope agravación 10 · tope tierra 5 · tregua crítica 60 min

### Fallas en vuelo por severidad

| Desgaste | Escala | Leves | Graves | Críticas | Total |
|---|---|---:|---:|---:|---:|
| 0 % | 1 h | 0,4 | 0,16 | 0,06 | 0,62 |
| 0 % | 10 h | 4 | 1,6 | 0,56 | 6,16 |
| 0 % | 100 h | 40 | 16 | 5,6 | 61,6 |
| 25 % | 1 h | 0,6 | 0,24 | 0,08 | 0,92 |
| 25 % | 10 h | 6 | 2,4 | 0,84 | 9,24 |
| 25 % | 100 h | 60 | 24 | 8,4 | 92,4 |
| 50 % | 1 h | 0,8 | 0,32 | 0,11 | 1,23 |
| 50 % | 10 h | 8 | 3,2 | 1,12 | 12,32 |
| 50 % | 100 h | 80 | 32 | 11,2 | 123,2 |
| 75 % | 1 h | 1 | 0,4 | 0,14 | 1,54 |
| 75 % | 10 h | 10 | 4 | 1,4 | 15,4 |
| 75 % | 100 h | 100 | 40 | 14 | 154 |
| 100 % | 1 h | 1,2 | 0,48 | 0,17 | 1,85 |
| 100 % | 10 h | 12 | 4,8 | 1,68 | 18,48 |
| 100 % | 100 h | 120 | 48 | 16,8 | 184,8 |

### Semilla de tierra al iniciar cada vuelo (distribución)

Cada vuelo **sortea** su arranque entre 0 y el tope del perfil: P(k) es la probabilidad de arrancar con exactamente k fallas de tierra y P(0) la de arrancar limpio. La media resume el conjunto; no es lo que ocurre en cada vuelo.

| Desgaste | P(0) | P(1) | P(2) | P(3) | P(4) | P(5) | Media |
|---|---:|---:|---:|---:|---:|---:|---:|
| 0 % | 9,1 % | 21,8 % | 26,1 % | 20,9 % | 12,5 % | 9,6 % | 2,35 |
| 25 % | 2,7 % | 9,8 % | 17,7 % | 21,2 % | 19,1 % | 29,4 % | 3,32 |
| 50 % | 0,8 % | 4,0 % | 9,5 % | 15,2 % | 18,2 % | 52,4 % | 4,03 |
| 75 % | 0,2 % | 1,5 % | 4,5 % | 8,9 % | 13,4 % | 71,5 % | 4,48 |
| 100 % | 0,1 % | 0,5 % | 1,9 % | 4,6 % | 8,4 % | 84,4 % | 4,74 |

### Experiencia por vuelo (3 h)

| Desgaste | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |
|---|---:|---:|---:|---:|---:|
| 0 % | 1,85 | 84,2 % | 38,1 % | 15,5 % | **6 vuelos** |
| 25 % | 2,77 | 93,7 % | 51,3 % | 22,3 % | **4 vuelos** |
| 50 % | 3,7 | 97,5 % | 61,7 % | 28,5 % | **3 vuelos** |
| 75 % | 4,62 | 99,0 % | 69,9 % | 34,3 % | **2 vuelos** |
| 100 % | 5,54 | 99,6 % | 76,3 % | 39,6 % | **2 vuelos** |

