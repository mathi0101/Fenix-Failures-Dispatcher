# Ticket 03 — Ciclo de vida de la sesión

Type: grilling
Status: resolved
Blocked by:

## Question

Máquina de estados de una sesión Realistic: iniciar (en tierra o en vuelo; varias sesiones por vuelo posibles), pausa del motor de fallas (desactiva las fallas activas en Fenix y bloquea nuevas; la sesión sigue viva), reanudar (por defecto sin fallas; con checkbox para restaurar el conjunto previo), terminar sesión (con confirmación si se está en vuelo), cierre de la app con sesión abierta (detectar al reabrir, finalizar manualmente, mostrar resultados). Transiciones válidas e inválidas entre estados. Qué pasa si SimConnect o Fenix se desconectan a mitad de sesión.

## Answer

- **Estados (Q33)**: `Inactiva` → `Activa (motor corriendo)` ⇄ `Motor pausado` → `Terminada`. Confirmado por el usuario.
- **Desconexión en pleno vuelo (Q34)**: la sesión **aguanta la reconexión** de SimConnect y/o Fenix — el motor entra en espera (no evalúa ni ejecuta) y al restablecerse **reanuda solo** (a diferencia del cierre de la app, que no auto-reanuda). Al volver, Fenix debe **re-consultarse la lista de fallas activas/inactivas reales** (ya existe el método en el código) para reconciliar el estado: las que siguen activas se reconocen, las que ya no están se registran.
- **Varias sesiones por vuelo (Q35)**: un **vuelo físico** puede contener varias sesiones, pero **cada sesión es un historial/vuelo por separado** (no se agrupan). El historial lista sesiones independientes; dos sesiones pueden referenciar el mismo OFP sin problema. El desgaste acumula a nivel de aeronave. *(Corregido por Q72 en el ticket 09: elimina la agrupación "vuelo físico → sesiones".)*
- **Sesión y OFP (Q36)**: **cada sesión se inicia descargando de nuevo el OFP de SimBrief** (si el usuario no creó otro, la API devuelve el mismo). Cada sesión es **independiente** y usa el OFP descargado al querer iniciarla.
- **Nuevo requisito (Q36, "ME ACABO DE DAR CUENTA")**: hoy se exige Fenix conectado para iniciar el modo Realistic; **esto debe cambiar**: con **SimConnect conectado** se puede iniciar el modo y todas sus funciones. Si Fenix no está operativo, la app **avisa** y el botón "Apagar fallas" indica que las fallas **no se pueden activar** hasta que el sistema Fenix esté operativo. Utilidad: mientras el simulador carga el avión, el jugador ya puede iniciar el modo Realistic; el motor piensa, analiza y **prepara fallas desde el arranque**, y cuando la API de Fenix responde (200), las envía. → **Ticket 12**.
- **Terminar sesión (Q37)**: terminar la sesión **limpia todas las fallas activas** que haya en Fenix.
