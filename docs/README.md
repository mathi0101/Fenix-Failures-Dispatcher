# Documentación del proyecto

Índice de la documentación vigente del repositorio. Cada tema tiene **una fuente única**; si un tema aparece en más de un documento vigente, es un error: se consolida en el indicado y el resto remite a él.

## Vigente

| Documento | Propósito | Cuándo leerlo | Estado |
|---|---|---|---|
| [project.md](project.md) | Visión, alcance, fuentes de verdad, objetivos/no objetivos y decisión SimConnect vs FSUIPC | Antes de cualquier decisión de diseño o cambio de rumbo | Vigente |
| [arquitectura.md](arquitectura.md) | Capas, dependencias, componentes reales y flujo vigente de la app | Antes de tocar estructura, DI o flujo | Vigente |
| [desarrollo.md](desarrollo.md) | Requisitos, comandos, configuración, migrations, ramas y formato de commits | Siempre que se vaya a desarrollar, migrar o commitear | Vigente |
| [CHANGELOG.md](CHANGELOG.md) | Bitácora técnica de cambios (qué/por qué/áreas/pruebas/pendientes) | Al terminar cada cambio significativo | Vigente (crece con cada entrada) |
| [realistic-mode/estado-y-diseno.md](realistic-mode/estado-y-diseno.md) | Estado del modo Realistic: implementado, estabilización verificada, en desarrollo, pendiente, decisiones y limitaciones restantes | Antes de tocar cualquier archivo del modo Realistic | Vigente (evoluciona con el WIP) |
| [realistic-mode/especificacion-funcional.md](realistic-mode/especificacion-funcional.md) | Especificación funcional **deseada** del modo Realistic (visión de producto): aeronaves, desgaste, niveles de riesgo y lógica de fallas, con brechas respecto a la implementación actual | Antes de diseñar o priorizar trabajo nuevo del modo Realistic | Vigente |
| [integraciones/simconnect.md](integraciones/simconnect.md) | Cliente SimConnect real y mock, contratos, message pump, polling, thread safety y advertencias | Antes de tocar `Integrations.SimConnect` | Vigente |
| [integraciones/fenix-api.md](integraciones/fenix-api.md) | Endpoints Fenix, configuración por claves, cliente real/mock, reset y verificaciones | Antes de tocar `Integrations.Fenix` | Vigente |

## Histórico

Documentos que describen un estado pasado del proyecto. Se conservan como referencia únicamente; **no** son fuente de verdad del estado actual.

| Documento | Propósito | Cuándo leerlo | Estado |
|---|---|---|---|
| [historial/2026-simconnect-fixes.md](historial/2026-simconnect-fixes.md) | Fotografía de las correcciones de conexión SimConnect de 2026 (message pump, thread safety, tipos) | Solo para entender decisiones pasadas; el flujo actual está en [arquitectura.md](arquitectura.md) e [integraciones/simconnect.md](integraciones/simconnect.md) | Histórico |

## Fuente única por tema

- Contexto del simulador: [integraciones/simconnect.md](integraciones/simconnect.md) (datos) + [arquitectura.md](arquitectura.md) (flujo).
- Sistemas/fallas del Fenix: [integraciones/fenix-api.md](integraciones/fenix-api.md).
- Modo Realistic: [realistic-mode/especificacion-funcional.md](realistic-mode/especificacion-funcional.md) (qué debería hacer) + [realistic-mode/estado-y-diseno.md](realistic-mode/estado-y-diseno.md) (qué hace hoy el código; el prompt original de diseño quedó reemplazado por este doc).
- Convenciones de desarrollo y commits: [desarrollo.md](desarrollo.md).
- Bitácora de cambios: [CHANGELOG.md](CHANGELOG.md).

## Reglas

- Los documentos de `historial/` no se deben citar como estado actual ni reutilizar sus afirmaciones sin re-verificarlas.
- Todo cambio significativo actualiza el doc afectado + `CHANGELOG.md` en el mismo trabajo (ver `desarrollo.md`).
- No duplicar contenido: si falta información, se agrega en el doc de la fuente única del tema.
