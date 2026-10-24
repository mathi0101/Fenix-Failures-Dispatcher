# Modal de resultados

Type: prototype
Status: resolved
Blocked by: 01 (resuelto — desbloqueado)

## Question

¿Cómo es el modal de resultados que aparece automáticamente al terminar la sesión Realista (§13)?

## Decisions (usuario, 2026-10-23)

- **Mock visual antes de implementar**: primero un mock HTML publicado (iterable), después WPF. No se implementa directo.
- **Contenido base**: además del resumen, deben aparecer **detallados todos los eventos de vuelo** y **las fallas lanzadas**, y el **desglose del aumento de desgaste por componente (sistema) debido a la sesión** (delta por ATA, no solo el total de la aeronave).
- **Re-apertura**: sí, desde el historial del paso 2 para sesiones ya finalizadas (botón «Ver resultados»).
- **Cuándo aparece**: al detener con «DETENER» y también al finalizar una sesión huérfana recuperada.
- **Estética**: alineada a `RealisticRiskProfileWindow` (modal oscura existente); colores por estado/severidad según §14 (enviada amarillo, activa Minor/Major naranja, activa Critical rojo, resuelta verde, rechazada gris).

## Próximo paso

Construir el mock (datos sintéticos realistas de un vuelo), publicarlo y iterar con el usuario antes de pasar a WPF.

## Answer

Implementado (2026-10-23), sobre el mock aprobado por el usuario:

- `SessionResultWindow` + `SessionResultWindowViewModel`: cabecera (ruta, registro, perfil, inicio/fin/duración UTC), 6 tiles de resumen + severidad, cronología completa de `SessionEventLog`, fallas con severidad/estado/fuente y **desgaste por componente** (delta de la sesión + total, aviso ≥ 80 %).
- El delta por sistema se **persiste** al cerrar (nueva entidad `SessionSystemWearDelta`) para que el modal sea re-abrible desde el historial con el mismo desglose.
- Apertura: tras «DETENER» (`RealisticSessionManager.LastSessionResult`), al finalizar una huérfana recuperada y con el botón «Resultados» del historial.
- Verificado: build Debug/Release sin warnings + 193/193 tests. Pendiente: prueba real en la PC de testing.
