# Comunidad: canales y configuración de GitHub

Fuente única para cómo se organizan los canales públicos del repo ([mathi0101/Fenix-Failures-Dispatcher](https://github.com/mathi0101/Fenix-Failures-Dispatcher)): qué va en cada canal, qué infraestructura se creó y qué pasos de configuración web quedan pendientes.

Toda la documentación **orientada al público** (README, templates, archivos de comunidad, posts de Discussions) está **bilingüe inglés + español** por decisión de producto: la app es de uso global. Los docs internos de código (`docs/`) siguen en español.

## Archivos de infraestructura de comunidad

| Archivo | Propósito |
|---|---|
| [.github/ISSUE_TEMPLATE/bug.yml](../../.github/ISSUE_TEMPLATE/bug.yml) | Formulario guiado de bug: qué pasó / pasos / esperado / modo / simulador / versión / log del día / checklist |
| [.github/ISSUE_TEMPLATE/feature_request.yml](../../.github/ISSUE_TEMPLATE/feature_request.yml) | Formulario para propuestas concretas (las ideas abiertas van a Discussions → Ideas) |
| [.github/ISSUE_TEMPLATE/config.yml](../../.github/ISSUE_TEMPLATE/config.yml) | Routing: bloquea issues en blanco y manda preguntas → Q&A, ideas abiertas → Ideas, capturas → Show and tell, seguridad → SECURITY.md |
| [.github/PULL_REQUEST_TEMPLATE.md](../../.github/PULL_REQUEST_TEMPLATE.md) | Checklist de PR: build, suite, sesión manual si cambió integración, docs + CHANGELOG, bilingüismo de docs públicas |
| [CONTRIBUTING.md](../../CONTRIBUTING.md) | Cómo contribuir: canales, cómo reportar un buen bug, setup de desarrollo, convenciones, licencia |
| [CODE_OF_CONDUCT.md](../../CODE_OF_CONDUCT.md) | Contributor Covenant 2.1 (inglés + español oficial) |
| [SECURITY.md](../../SECURITY.md) | Política de seguridad: reporte privado (GitHub Security tab o email), scope, tiempos |
| [SUPPORT.md](../../SUPPORT.md) | Dónde pedir ayuda y qué incluir (lo muestra GitHub al crear un issue) |
| [README.md](../../README.md) / [README.es.md](../../README.es.md) | README en inglés (idioma primario) y español, con selector de idioma y sección de feedback con routing |

## Mapa de canales (qué va dónde)

| Contenido | Canal |
|---|---|
| Bugs, propuestas concretas, tareas propias | **Issues** (formularios guiados) |
| Preguntas de instalación/uso | **Discussions → Q&A** (marcar respuesta correcta) |
| Ideas tempranas y sugerencias | **Discussions → Ideas** |
| Capturas de vuelos e historias | **Discussions → Show and tell** |
| Anuncios de release y de estado del proyecto | **Discussions → Announcements** (solo maintainers) |
| Encuestas para priorizar | **Discussions → Polls** |
| Vulnerabilidades | **SECURITY.md** (reporte privado; nunca issue público) |

Las categorías de Discussions ya existen (son las default de GitHub: Announcements, General, Ideas, Polls, Q&A, Show and tell); el cambio es solo de uso y descripciones.

## Pendiente de configuración en la web de GitHub (manual)

Estos pasos requieren sesión con la cuenta `mathi0101` (o un token de esa cuenta con push). En esta tarea no se pudieron automatizar: el token disponible en el credential manager pertenece a `mgonzalez-quaris`, que no tiene acceso de push al repo.

### 1. Social preview y About (repo → ⚙️ Settings, al lado del nombre)

- **Topics** (campo "Topics" del About, edit in place): `msfs`, `msfs2020`, `msfs2024`, `flight-simulator`, `fenix`, `a320`, `failure-simulation`, `simconnect`, `csharp`, `dotnet`, `wpf`, `flight-sim-tools`. Sumar estos mejora el descubrimiento en GitHub Search/Topics.
- **Website** (campo "Website" del About): `https://mathi0101.github.io/Fenix-Failures-Dispatcher/` (la página de demos).
- **Social preview** (Settings → General → Social preview): subir imagen 1280×640 — idealmente la app junto al avión (puede ser una captura del mock del modo realista con la marca).

### 2. Reporte privado de vulnerabilidades (Settings → Code security & analysis)

- Activar **"Private vulnerability reporting"**. Habilíta el botón *Report a vulnerability* en la pestaña Security, que es el canal preferido que anuncia `SECURITY.md`.

### 3. Labels nuevas (Issues → Labels → New label)

- `triage` — color gris/azul, descripción: "Needs review / Requiere revisión". Los dos formularios de issue la aplican al crear el issue; si no existe, crearla antes o después (GitHub la crea sola si falta al abrir un issue con template).
- Opcionales para el flujo de triage: `confirmed` ("Reproducido y confirmado / Reproduced and confirmed"), `waiting-for-info` ("Falta info del reportante / Awaiting reporter info").

### 4. Descripciones bilingües de categorías de Discussions (Discussions → categorías → ✏️ Edit)

Pegar tal cual (una por categoría):

- **Announcements**: `Updates from maintainers — releases and project status · Novedades de quien mantiene el proyecto: releases y estado`
- **General**: `Chat about anything and everything · Charla sobre lo que quieras`
- **Ideas**: `Share ideas for new features — early-stage suggestions welcome · Compartí ideas para nuevas funciones — las sugerencias en estado temprano son bienvenidas`
- **Polls**: `Take a vote from the community · Encuestas para la comunidad`
- **Q&A**: `Ask the community for help — mark the answer that solves it · Preguntale a la comunidad — marcá la respuesta que resuelve tu duda`
- **Show and tell**: `Show off your flights, screenshots and stories · Mostrá tus vuelos, capturas e historias`

### 5. Post de bienvenida fijado (Discussions → Announcements)

Crear una discussion en **Announcements** con el título `Welcome! Read this first / ¡Bienvenido/a! Leé esto primero` y este cuerpo (pegar tal cual). Después: **fijarla** con el PIN de la columna derecha (máximo 3 fijadas).

```markdown
Welcome to the Real Fenix Failures community! / ¡Bienvenido/a a la comunidad de Real Fenix Failures!

This is a realism-focused failure engine for the Fenix A320 in Microsoft Flight Simulator. The app is in **beta** — your feedback drives development. / Este es un motor de fallas enfocado en realismo para el Fenix A320 en Microsoft Flight Simulator. La app está en **beta**: tu feedback dirige el desarrollo.

✍️ **You can write in English or Spanish — both are welcome.** / **Podés escribir en inglés o español — ambos son bienvenidos.**

## Where does each thing go? / ¿Dónde va cada cosa?

| I want to... / Quiero... | Go to / Ir a |
|---|---|
| 🐛 Report a bug / Reportar un bug | [Issues → Bug report](https://github.com/mathi0101/Fenix-Failures-Dispatcher/issues/new/choose) |
| ❓ Ask how to install or use it / Preguntar cómo instalarla o usarla | [Q&A](https://github.com/mathi0101/Fenix-Failures-Dispatcher/discussions/categories/q-a) |
| 💡 Suggest an idea / Sugerir una idea | [Ideas](https://github.com/mathi0101/Fenix-Failures-Dispatcher/discussions/categories/ideas) |
| 🖼️ Share flights and screenshots / Compartir vuelos y capturas | [Show and tell](https://github.com/mathi0101/Fenix-Failures-Dispatcher/discussions/categories/show-and-tell) |
| 📣 See releases and news / Ver releases y novedades | [Announcements](https://github.com/mathi0101/Fenix-Failures-Dispatcher/discussions/categories/announcements) + [Releases](https://github.com/mathi0101/Fenix-Failures-Dispatcher/releases) |
| 🔒 Report a security issue / Reportar un problema de seguridad | [SECURITY.md](https://github.com/mathi0101/Fenix-Failures-Dispatcher/blob/master/SECURITY.md) — private report / reporte privado |

## Tips for a fast answer / Tips para una respuesta rápida

- Include your **app version** (e.g. `v0.4.1-beta.1`), simulator (MSFS 2020/2024) and what you were doing. / Incluí tu **versión de la app**, el simulador (MSFS 2020/2024) y qué estabas haciendo.
- For bugs, attach the **app log for the day**: `%AppData%\RealFenixFailures\logs\realfenix-<YYYYMMDD>.log` / Para bugs, adjuntá el **log del día**: `%AppData%\RealFenixFailures\logs\realfenix-<AAAAMMDD>.log`.
- Read the [Code of Conduct](https://github.com/mathi0101/Fenix-Failures-Dispatcher/blob/master/CODE_OF_CONDUCT.md) — be kind, we're all here for the flying. / Leé el [Código de Conducta](https://github.com/mathi0101/Fenix-Failures-Dispatcher/blob/master/CODE_OF_CONDUCT.md) — seamos amables, todos estamos acá por el vuelo.

Happy landings (mostly)! / ¡Felices aterrizajes (casi siempre)!
```

### 6. Milestones y roadmap (opcional pero recomendado)

- **Milestone** (Issues → Milestones → New milestone): `1.0.0`, descripción: `Polished stable release · Versión estable bien pulida`. Los issues de bugs/pendientes para la 1.0.0 se agrupan ahí; da a los usuarios una vista de "qué va a incluir la próxima etapa".
- **Roadmap** (Projects → New project → Board o Table): crearlo **público** con las columnas backlog / in progress / done (el template de table ya trae Status). Agregarlo al repo desde el proyecto (⚙️ → link repository) para que las cards se conviertan en issues del repo. Publicar el roadmap reduce el "¿cuándo sale X?" en Discussions.

### 7. Anuncio de release en Announcements

Cada release nueva: crear una discussion en Announcements con el título del tag (`v0.4.1-beta.1 released`) y copiar el cuerpo de `docs/releases/vX.Y.Z.md` (ya se genera bilingüe según el runbook de release). Se puede automatizar después agregando un paso al workflow `Release` (acciones `github-discussion`-style) — tarea futura.

## Wiki

La Wiki **tiene contenido** (no está vacía): `Home.md`, `Modo-Realista:-Especificación-y-Funcionamiento-Interno.md` y `Sistema-de-Cálculo-de-Probabilidades-en-Realistic-Fenix-Failures.md`, en español y sin versionar en el repo principal.

Decisiones posibles (a definir con el dueño del repo):

1. **Migrar** ese contenido a `docs/` (versionado, bilingüe si es público, en el índice de `docs/README.md`) y desactivar la Wiki (Settings → Features → Wikis) — es lo coherente con la regla "una fuente única por tema".
2. **Dejarla** pero bilingüizarla y linkearla desde el README como documentación de usuario.

⚠️ Nota técnica: el nombre `Modo-Realista:-Especificación-y-Funcionamiento-Interno.md` tiene dos puntos, lo que hace imposible clonar la wiki en Windows (path inválido). Si se migra o renombra, evitar `:` en los nombres.

## Mantenimiento de este esquema

- El triage de issues nuevo: aplicar `triage` (ya viene del formulario) → revisar → si es real: `confirmed` + sacar `triage`; si falta info: `waiting-for-info` y pedir en comentarios; cerrar si no responde en ~2 semanas.
- Los issues/PRs se cierran solos si el commit o PR del fix incluye `Fixes #N`.
- Convertir ↔ Issues/Discussions cuando alguien publique en el canal equivocado (botón lateral "Convert to discussion" / reconvertir desde Discussions).
