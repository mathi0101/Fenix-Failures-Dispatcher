# Skills — `.agents/skills`

Las skills instaladas en `.abacusai/skills/` son **herramientas de trabajo reutilizables** (metodologías y procedimientos de trabajo para agentes), **no contexto del dominio** de este proyecto. No describen cómo funciona la app ni reemplazan la documentación: la fuente del proyecto son `AGENTS.md` y `docs/`.

No se mueven ni se borran skills; la selección depende de la tarea concreta. Esta tabla clasifica cuáles conviene considerar por defecto en este repositorio.

## Recomendadas para este repo

| Skill | Cuándo |
|---|---|
| `tdd` | Al implementar o corregir comportamiento (motor de fallas, servicios, detector) |
| `diagnosing-bugs` | Ante un bug o fallo reportado, incluyendo los riesgos conocidos del modo Realistic |
| `code-review` | Al revisar cambios de código ajenos o propios antes de cerrar una iteración |
| `codebase-design` | Antes de decidir estructura, capas o interfaces nuevas |
| `resolving-merge-conflicts` | Al integrar ramas con conflictos |
| `research` | Cuando hay que investigar APIs externas (SimConnect, Fenix, .NET) o decisiones técnicas |
| `writing-for-agents` | Al escribir documentación o instrucciones para agentes |
| `handoff` | Al entregar trabajo a otro agente o al usuario |
| `implement-spec` | Al implementar una especificación puntual ya definida |

## Condicionales (según la tarea)

- `implement`, `to-spec`, `to-tickets`, `to-questionnaire`, `triage`, `grill-me`, `grill-with-docs`, `grilling`, `ask-matt`, `wayfinder`, `retro`, `prototype`, `domain-modeling` (modelado de dominio), `improve-codebase-architecture` (refactor arquitectónico grande), `migrate-to-shoehorn`, `loop-me`, `git-guardrails-claude-code`, `setup-pre-commit`, `scaffold-exercises`, `wizard`, `wait-what`.
- `documentación/escritura`: `writing-shape`, `writing-fragments`, `writing-beats` — solo si el encargo es específicamente de documento con esa forma.

## No aplicables por defecto

- `setup-ts-deep-modules` — TypeScript; fuera del stack (.NET/WPF).
- `teach`, `setup-matt-pocock-skills` — cursos/talleres; no aplica al desarrollo del producto.
- `claude-handoff` — específica de otro contexto de agente; preferir `handoff`.

## Regla

Ante dudas entre skills y documentación, manda **[AGENTS.md](../AGENTS.md) + [docs/](../docs/)**: la selección de skill es un medio, no el objetivo de la tarea.
