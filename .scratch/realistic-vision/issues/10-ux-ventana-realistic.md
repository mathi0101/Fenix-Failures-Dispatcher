# Ticket 10 — UX de la ventana del modo Realistic

Type: prototype
Status: resolved
Blocked by: 03, 04, 07 (resueltos)

## Question

Prototipo de la UX de la ventana/panel del modo Realistic: fallas con estados por color (enviada a Fenix vs. activa confirmada vs. rechazada), pensamiento del engine oculto por defecto (ícono de ojito para mostrarlo/ocultarlo), acciones del usuario (pausar motor de fallas, reanudar con checkbox de restauración, terminar sesión con confirmación en vuelo, apagar todas las fallas), indicadores en vivo (fase de vuelo, perfil de riesgo, OFP, estado de conexión, duración de la sesión), log izquierdo con eventos validados. Maqueta de baja fidelidad para reaccionar.

## Answer

- **Layout (Q78)**: coincide con la propuesta de 4 zonas (cabecera del vuelo, fallas activas, pensamiento del engine con ojito, acciones), **adaptando a lo que ya existe hoy en el código** — solo cambios puntuales, no rediseño.
- **Colores de estados (Q79)**: **enviada a Fenix** (pendiente de confirmar) = **amarillo**; **activa Major o Minor** = **naranja**; **activa Critical** = **rojo**; **resuelta** (auto-resuelta o por piloto) = **verde**; **rechazada/no-evento** = **gris** (breve). Código de color por severidad, no uniforme.
- **Botón de pausa (Q80)**: **un solo botón** — el motor como tal **no se pausa**; sigue pensando (el avión sigue volando y moviéndose). El botón **solo desactiva las fallas activas en Fenix**. Debe ser visible y además **representar/explicar lo que hace** (texto claro). Funde lo que antes eran "Apagar fallas" (Q6) y "Pausar motor" (Q14) en una única acción de pausa del sistema de fallas: desactiva las activas y bloquea el armado de nuevas (la sesión sigue viva). Al reactivar, el checkbox de restauración del conjunto previo (Q15) queda como opción.
- **Resultados al terminar (Q81)**: al terminar la sesión, **modal de resultados automático** (resumen: cabecera + conteo por severidad) con acceso al historial completo (timeline, ticket 09) y al desgaste; el historial queda persistido en la pantalla Historial.
- **Estado de conexión (Q82)**: **ya está implementado hoy en el código** — dejar como está.
- **Notificaciones (Q83)**: **ninguna notificación del sistema** (ni toast, ni sonido) — le saca realismo. En el log izquierdo en tiempo real **no** deben aparecer fallas. Las fallas y el pensamiento del engine solo se ven en el **log del modo Realistic**, y solo si el usuario tiene la app abierta y el ojito de ver activado. Queda destinado al debug; en uso real casi nunca se ve (el piloto está volando).
