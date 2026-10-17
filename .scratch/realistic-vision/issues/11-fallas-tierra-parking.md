# Ticket 11 — Fallas de tierra en parking (fallas iniciales)

Type: grilling
Status: resolved
Blocked by: 02, 04 (resueltos)

## Question

Cuando una sesión comienza con el avión en parking y todo apagado, el engine puede activar **fallas iniciales** (Minor/Major, reparables) para dar la sensación de "tomar un avión con fallas", frecuentes en todos los perfiles (ticket 01, Q21), cargadas **idealmente antes** de conectar GPU o prender APU. Faltan definir: cómo se decide cuántas y cuáles (perfil, aleatorio, tope); si todas son reparables vía mantenimiento de Fenix FMGC; si se persisten como fallas de la sesión o son "preámbulo"; qué pasa si el usuario no las repara y larga igual; si consumen de la cuota del vuelo; y el caso de descartarlas si Fenix tardó (ticket 04, Q41).

## Answer

- **Cuántas y cuáles (Q44)**: aleatorio dentro del catálogo de fallas de tierra (Minor/Major reparables), con **cantidad acotada por perfil** (tope de seguridad, editable en appsettings): Bajo → 1-2, Moderado → 1-3, Alto → 2-4, 7700 → 3-5. Composición aleatoria respetando coherencia (no dos fallas del mismo sistema a la vez, no contradicciones).
- **No reparar y largar igual (Q45)**: decisión del usuario — la falla **sigue activa** (largar con un sistema fallado); el engine la deja estar (no la arregla ni la empeora). El historial la registra como **no resuelta al despegue**.
- **Cuota del vuelo (Q46)**: es **aparte** — la cuota del perfil (cadencia por hora) corre cuando el avión deja parking (primer taxi). Las fallas iniciales son añadido de inmersión en tierra. El tope de simultáneas (Q22) sí aplica en conjunto (tierra + vuelo ≤ tope).
- **Persistencia / historial (Q47)**: **cuentan** como parte de la sesión (qué, cuándo, si se repararon antes de salir). No son bloque aparte ni invisible.
- **Desgaste (Q48)**: las fallas **no** generan desgaste por sí mismas — el desgaste lo genera el **tiempo de vuelo** de cada sesión (ver ticket 08). Reparar una falla en tierra (mantenimiento del FMGC de Fenix) **resetea a cero el desgaste del sistema**; las fallas críticas que no son reparables desde mantenimiento se reparan **manualmente desde la UI** viendo el desgaste de la aeronave (reset por sistema, inmediato).
- **Cuándo (Q49)**: solo si la sesión empieza en parking (fase inicial con todo apagado); si se inicia en pleno vuelo no hay fallas iniciales (el engine arranca su narrativa normal de fases).
