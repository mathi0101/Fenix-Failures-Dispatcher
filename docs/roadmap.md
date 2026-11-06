# Roadmap

Alcance acordado por release. Es la fuente única de «qué va en cada milestone»; el diseño detallado de cada ítem se define al implementar y se documenta en el doc de estado que corresponda. El backlog general del modo Realistic (ítems no asignados a un milestone) sigue en [realistic-mode/estado-y-diseno.md](realistic-mode/estado-y-diseno.md).

Cuando se cree el milestone `1.0.0` en GitHub (paso pendiente, ver [comunidad/canales-y-configuracion.md](comunidad/canales-y-configuracion.md) §6), estos ítems son los que se agrupan ahí.

## Contexto de pruebas

La app se prueba en vivo con sesiones reales MSFS/Fenix de forma continua desde la v0.2.0: hay vuelos completos en modo Realistic funcionando de punta a punta, el modo debug (reloj de simulación escalable con sus knobs) fue probado, y la interfaz también fue verificada visualmente. El milestone 1.0.0 apunta al pulido y a estas cuatro funcionalidades, no a validar la base.

## Milestone 1.0.0 — release oficial estable

Definido el 2026-10-31. Cuatro ítems:

1. **App bilingüe ES/EN, seleccionable y 100 % traducida (visual)** — toda la capa de usuario traducida: ventanas, paneles, tooltips, mensajes de validación, diálogos, modal de resultados, splash y bandeja. Selector de idioma en Ajustes, persistido en `userprefs.json`. Abiertos de diseño: mecanismo de localización WPF (recursos por cultura) y si el cambio de idioma aplica en vivo o requiere reiniciar la app.
2. **Mantenimiento de sistemas desgastados** — funcionalidad para que el usuario haga mantenimiento a los sistemas con desgaste acumulado y este baje (`AircraftSystemWear`), desde la UI. Conecta con lo previsto en [realistic-mode/diseno-objetivo.md](realistic-mode/diseno-objetivo.md) (reset de desgaste manual desde la UI, por sistema). Abiertos de diseño: reducción total o parcial, por sistema o global, y su interacción con el aviso «REQUIERE MANTENIMIENTO» (`Wear:ThresholdNoticePct`).
3. **Revisión y rediseño del botón para frenar fallas** — repasar el par «Pausar fallas» / «Reanudar» del paso 3 del panel Realistic: naming claro para el piloto, estados visuales, feedback de qué pasa con las fallas activas (desarme en Fenix, sesión sigue viva) y comportamiento sin Fenix. La semántica vigente (pausa desarma fallas en Fenix y bloquea armado nuevo sin cerrar la sesión) se conserva como base del rediseño.
4. **Prompt de finalización al detectar vuelo terminado** — al abrir la app, si se detecta que la aeronave de una sesión Realistic sin finalizar aterrizó, llegó a gate y apagó motores, mostrar el prompt para finalizar la sesión (con resultados). Se apoya en el flujo existente de sesiones sin finalizar (`UnfinishedSessionsWindow`: Finalizar y ver resultados / Descartar / Dejar para después). Abiertos de diseño: fuente de la detección (telemetría SimConnect al conectar vs eventos registrados de la sesión) y si el prompt aparece solo al abrir o también en vivo durante la sesión.
