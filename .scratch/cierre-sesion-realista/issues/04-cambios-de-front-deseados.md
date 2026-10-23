# Cambios de front deseados

Type: grilling
Status: resolved

## Question

¿Qué cambios de front querés hacer? El usuario pidió «unos cambios en el front» sin nombrarlos: este ticket los levanta uno por uno, los afila (qué, dónde, por qué) y decide si cada uno entra a este mapa como ticket propio, se agrega a un ticket existente (por ejemplo, al «Modal de resultados» si toca la misma pantalla) o queda para otro esfuerzo.

## Answer

Resuelto (2026-10-23): el usuario nombró dos cambios; cada uno se graduó a ticket propio (create-then-wire):

1. **Todas las fechas y horas mostradas en la app en hora Zulu/UTC, «incluso los logs»** → ticket [Horas en UTC en toda la app](05-horas-en-utc.md) (abierto: falta fijar alcance de los `.log` en disco y formatos).
2. **Reloj UTC en tiempo real en la MainWindow, al lado de la fase, arriba en la status bar, formato `HH:MM:SS UTC`** → ticket [Reloj UTC en la barra de estado](06-reloj-utc-status-bar.md) (resuelto: la spec del usuario era completa).

No se agregaron al «Modal de resultados»: ninguna de las dos toca esa pantalla.
