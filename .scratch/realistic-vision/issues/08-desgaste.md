# Ticket 08 — Modelo de desgaste de sistemas

Type: grilling
Status: resolved
Blocked by: 01 (resuelto)

## Question

Modelo de desgaste en la visión: qué sistemas desgastan y por qué, delta por vuelo/sesión, efecto amplificador sobre la probabilidad de falla, persistencia por aeronave registrada, y cómo se muestra. Corrección de Q48: el desgaste lo genera el tiempo de vuelo por sesión, no las fallas.

## Answer

- **Sistemas (Q67)**: la tabla **`FenixFailureSystems`** del proyecto, cuya fuente de verdad es **`failures.json` en Domain** (ata × shortTitle), NO una lista fija de 8. Sistemas reales (19 ATAs): `Air` (ATA 21), `AutoFlight` (22), `Communications` (23), `Elec` (24), `Fire` (26), `Flight controls` (27), `Fuel` (28), `Hydraulic` (29), `Ice/Rain` (30), `Indicating/Recording` (31), `Gear` (32), `Navigation` (34), `Oxygen` (35), `Pneumatic` (36), `Information` (46), `APU` (49), `Doors` (52), `Engines` (70), `Start Faults` (80). El desgaste es **por sistema** (por cada uno de estos 19). *(Nota: la migración antigua mostraba un seed parcial de 8 — no usar como referencia.)*
- **Tasas (Q68)**: tasas **distintas por sistema** (configurables en appsettings); motores (Engines) y tren (Gear) desgastan más por hora que Elec/Pneumatic; la fase puede acentuar (Gear en aproximación, Engines en despegue). Puramente por hora con matiz de fase.
- **Efecto sobre probabilidad (Q69)**: el desgaste sube la **elegibilidad** de las fallas del sistema (más plausible), pero **nunca** cruza la severidad máxima del perfil — el techo lo mantiene el perfil, no el desgaste. Más desgaste → más fallas dentro de lo permitido, nunca críticas donde no corresponde.
- **Persistencia (Q70)**: el desgaste por sistema es **atributo de la aeronave registrada** (Q28), se acumula entre vuelos/sesiones en BD, y se puede ver y reiniar manualmente desde la UI (Q48). El FMGC de Fenix (reparar en tierra) también resetea el sistema.
- **Umbral / envejecimiento (Q71)**: escala 0-100 % por sistema; al pasar un umbral alto (configurable, p. ej. 80 %) la app avisa "sistema requiere mantenimiento" (y el FMGC en tierra lo repara); el 100 % puede seguir subiendo probabilidad (el avión "funciona pero mal") pero la app lo muestra siempre.
