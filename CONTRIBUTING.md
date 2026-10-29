# Contributing / Contribuir

🌍 **English** · [Español](#espa%C3%B1ol) — You can write in English or Spanish in every channel: issues, discussions and reviews. / Podés escribir en inglés o español en todos los canales: issues, discussions y reviews.

First off, thanks for wanting to help! This project is in beta and every bug report, idea and flight session counts. There are many ways to contribute — most don't require writing a single line of code.

## Ways to contribute

| You want to... | Where | How |
|---|---|---|
| Report a bug | [Issues](https://github.com/mathi0101/Fenix-Failures-Dispatcher/issues/new/choose) | Use the **bug report** form (guided, asks for version, mode and log) |
| Propose a concrete improvement | [Issues](https://github.com/mathi0101/Fenix-Failures-Dispatcher/issues/new/choose) | Use the **feature request** form |
| Share an early idea or ask "would this be possible?" | [Ideas in Discussions](https://github.com/mathi0101/Fenix-Failures-Dispatcher/discussions/categories/ideas) | Open thread, no form needed |
| Ask how to install, configure or use the app | [Q&A in Discussions](https://github.com/mathi0101/Fenix-Failures-Dispatcher/discussions/categories/q-a) | Open thread; mark the answer that solves it |
| Share flights, screenshots, stories | [Show and tell](https://github.com/mathi0101/Fenix-Failures-Dispatcher/discussions/categories/show-and-tell) | Open thread |
| Report a security vulnerability | [SECURITY.md](SECURITY.md) | **Privately**, never a public issue |
| Contribute code or docs | Pull requests | See "Development setup" below |

## Reporting a good bug

The more reproducible a report, the faster the fix. The bug form asks for this, but in short:

1. **What you were doing** (flight phase, app mode, what you had configured).
2. **What you expected to happen** and **what actually happened**.
3. **App version** (e.g. `v0.4.1-beta.1`).
4. **The log for the day**: `%AppData%\RealFenixFailures\logs\realfenix-<YYYYMMDD>.log` — remove anything you consider personal before attaching.
5. A screenshot or short recording, if it's visual.

Please search [existing issues](https://github.com/mathi0101/Fenix-Failures-Dispatcher/issues) and [discussions](https://github.com/mathi0101/Fenix-Failures-Dispatcher/discussions) before opening a new one; if you find a duplicate, add a 👍 and your details there.

## Development setup

Requirements:

- **.NET 10 SDK** (the app targets .NET 10)
- **MSFS SimConnect SDK** (managed DLL at `MSFS SDK\SimConnect SDK\lib\managed\Microsoft.FlightSimulator.SimConnect.dll`)
- Windows x64 (WPF + SimConnect are Windows-only)
- Any editor; Visual Studio 2022 / VS Code / Rider all work

Commands (from `Realistic Failures Solution`):

```sh
dotnet restore RealFenixFailures.slnx
dotnet build RealFenixFailures.slnx -c Debug
dotnet test RealFenixFailures.TestUnits/RealFenixFailures.TestUnits.csproj
dotnet run --project RealFenixFailures.UI/RealFenixFailures.UI.csproj
```

Project layout and layering rules live in [docs/arquitectura.md](docs/arquitectura.md); the development conventions (branches, commits, migrations) in [docs/desarrollo.md](docs/desarrollo.md). Please read them before your first PR — the short version:

- Branches off `dev` named `feature/*`, `fix/*`, `docs/*`, `refactor/*`, `test/*` or `chore/*`.
- **Conventional Commits** (`feat`, `fix`, `docs`, `refactor`, `test`, `chore`, optional scope) with bullet bodies.
- Meaningful changes update the affected status doc under `docs/` **and** add an entry to [docs/CHANGELOG.md](docs/CHANGELOG.md) in the same work.
- Public-facing documentation (README, community files, anything users read) must be **bilingual English + Spanish**. Internal code docs under `docs/` are written in Spanish.
- Don't claim MSFS/Fenix integration works without a real manual session; automated build + tests alone is not the bar.

The PR template includes a checklist so you don't forget any of this.

## Pull requests

1. Fork (or branch off `dev` if you have write access).
2. Keep the change as small as the purpose allows.
3. Make sure the build is clean and the test suite passes.
4. If you touched UI, attach screenshots.
5. Fill in the PR template.

New contributors are welcome: if you want to take an issue, comment on it and it's yours. Issues labeled [`good first issue`](https://github.com/mathi0101/Fenix-Failures-Dispatcher/labels/good%20first%20issue) are a good starting point.

## License

By contributing, you agree that your contributions will be licensed under the [Apache License 2.0](LICENSE) that covers this project.

---

<a id="espa%C3%B1ol"></a>
# Español

🌍 [English](#contributing--contribuir) · **Español** — Podés escribir en inglés o español en todos los canales: issues, discussions y reviews.

Antes que nada, ¡gracias por querer ayudar! Este proyecto está en beta y cada reporte de bug, idea y sesión de vuelo suma. Hay muchas formas de contribuir — la mayoría no requiere escribir ni una línea de código.

## Formas de contribuir

| Querés... | Dónde | Cómo |
|---|---|---|
| Reportar un bug | [Issues](https://github.com/mathi0101/Fenix-Failures-Dispatcher/issues/new/choose) | Usá el formulario de **reporte de bug** (guiado, pide versión, modo y log) |
| Proponer una mejora concreta | [Issues](https://github.com/mathi0101/Fenix-Failures-Dispatcher/issues/new/choose) | Usá el formulario de **feature request** |
| Compartir una idea temprana o preguntar "¿se puede hacer esto?" | [Ideas en Discussions](https://github.com/mathi0101/Fenix-Failures-Dispatcher/discussions/categories/ideas) | Abrí un thread, sin formulario |
| Preguntar cómo instalar, configurar o usar la app | [Q&A en Discussions](https://github.com/mathi0101/Fenix-Failures-Dispatcher/discussions/categories/q-a) | Abrí un thread; marcá la respuesta que lo resuelve |
| Compartir vuelos, capturas, historias | [Show and tell](https://github.com/mathi0101/Fenix-Failures-Dispatcher/discussions/categories/show-and-tell) | Abrí un thread |
| Reportar una vulnerabilidad de seguridad | [SECURITY.md](SECURITY.md) | **De forma privada**, nunca un issue público |
| Aportar código o documentación | Pull requests | Mirá "Setup de desarrollo" acá abajo |

## Reportar un buen bug

Cuanto más reproducible es un reporte, más rápido sale el fix. El formulario te lo pide, pero en corto:

1. **Qué estabas haciendo** (fase de vuelo, modo de la app, qué tenías configurado).
2. **Qué esperabas que pasara** y **qué pasó finalmente**.
3. **Versión de la app** (ej. `v0.4.1-beta.1`).
4. **El log del día**: `%AppData%\RealFenixFailures\logs\realfenix-<AAAAMMDD>.log` — borrá todo lo que consideres personal antes de adjuntarlo.
5. Una captura o grabación corta, si es visual.

Por favor buscá en los [issues existentes](https://github.com/mathi0101/Fenix-Failures-Dispatcher/issues) y las [discussions](https://github.com/mathi0101/Fenix-Failures-Dispatcher/discussions) antes de abrir uno nuevo; si encontrás un duplicado, sumá un 👍 y tus datos ahí.

## Setup de desarrollo

Requisitos:

- **.NET 10 SDK** (la app apunta a .NET 10)
- **MSFS SimConnect SDK** (DLL managed en `MSFS SDK\SimConnect SDK\lib\managed\Microsoft.FlightSimulator.SimConnect.dll`)
- Windows x64 (WPF + SimConnect son solo Windows)
- El editor que quieras; Visual Studio 2022 / VS Code / Rider funcionan todos

Comandos (desde `Realistic Failures Solution`):

```sh
dotnet restore RealFenixFailures.slnx
dotnet build RealFenixFailures.slnx -c Debug
dotnet test RealFenixFailures.TestUnits/RealFenixFailures.TestUnits.csproj
dotnet run --project RealFenixFailures.UI/RealFenixFailures.UI.csproj
```

El layout del proyecto y las reglas de capas viven en [docs/arquitectura.md](docs/arquitectura.md); las convenciones de desarrollo (ramas, commits, migrations) en [docs/desarrollo.md](docs/desarrollo.md). Léalas antes de tu primer PR — la versión corta:

- Ramas desde `dev` nombradas `feature/*`, `fix/*`, `docs/*`, `refactor/*`, `test/*` o `chore/*`.
- **Conventional Commits** (`feat`, `fix`, `docs`, `refactor`, `test`, `chore`, scope opcional) con cuerpo de viñetas.
- Los cambios significativos actualizan el doc de estado afectado bajo `docs/` **y** agregan una entrada a [docs/CHANGELOG.md](docs/CHANGELOG.md) en el mismo trabajo.
- La documentación pública (README, archivos de comunidad, todo lo que lee un usuario) debe estar **bilingüe inglés + español**. Los docs internos de código bajo `docs/` se escriben en español.
- No afirmes que la integración MSFS/Fenix funciona sin una sesión manual real; build + tests automatizados no alcanzan.

La plantilla de PR incluye un checklist para que no se te olvide nada de esto.

## Pull requests

1. Hacé fork (o rama desde `dev` si tenés acceso de escritura).
2. Mantené el cambio tan chico como lo permita el propósito.
3. Asegurate de que el build esté limpio y la suite de tests pase.
4. Si tocaste UI, adjuntá capturas.
5. Completá la plantilla de PR.

Los nuevos contributors son bienvenidos: si querés tomar un issue, comentá en el issue y es tuyo. Los issues con la etiqueta [`good first issue`](https://github.com/mathi0101/Fenix-Failures-Dispatcher/labels/good%20first%20issue) son un buen punto de partida.

## Licencia

Al contribuir, aceptás que tus aportes se licencien bajo la [Apache License 2.0](LICENSE) que cubre este proyecto.
