# Real Fenix Failures

[![CI](https://github.com/mathi0101/Fenix-Failures-Dispatcher/actions/workflows/ci.yml/badge.svg)](https://github.com/mathi0101/Fenix-Failures-Dispatcher/actions/workflows/ci.yml)

A desktop app for **Microsoft Flight Simulator 2020/2024** that generates **realistic failures for Fenix Simulations aircraft**, running in the background while you fly.

> 🚧 **The project is in beta.** The goal of this stage is to test it on real flights, gather feedback and fix every possible bug on the way to a **well-polished 1.0.0**. If you fly Fenix aircraft, you're exactly the person I'm looking for: [download it](#download) and [tell me everything you find](#reporting-bugs-and-feedback).

🌍 **Languages / Idiomas:** [English](README.md) · [Español](README.es.md)

## Why it exists

The simulator's built-in failures are poor and random. This app triggers failures that make sense: they depend on the flight phase, on the aircraft's condition and on the wear the airframe accumulates flight after flight. It's not "something breaks at random" — it's "something happens because it has to happen".

It's for those who like to learn more, or to put real Airbus SOPs and flows into practice: whatever has to fail, fails — and it's up to you to diagnose and resolve it the way it should be done.

## What it does

- Runs in the background next to the simulator, with a real-time log of flight events.
- Reads the flight context (phase, altitude, speed, state) from MSFS via SimConnect.
- Queries and triggers real aircraft failures via Fenix's local API.
- Evaluates probabilities per flight phase and persists sessions, triggered failures, the user's aircraft and its wear in SQLite.
- Includes a **Realistic** mode where the aircraft ages and degrades with use, like a real one.

## Modes

- **Training** — ready-made presets to practice specific scenarios.
- **Custom** — build your own presets.
- **Realistic** — a probabilistic engine with persistent wear and **4 risk profiles**, to cover the identity and realism each pilot wants in the cockpit:
  - **Low** — for calm flights, knowing you'll reach your destination.
  - **Moderate** — the middle ground.
  - **High** — more probability, aggravation and failures on the ground.
  - **7700** — for the most experienced: the achievement is not reaching your destination, but saving your passengers and crew from a tragic accident.

## Interface demos

🖼️ If you want to see what the app looks like before downloading it, there are **navigable demos** (UI mocks with fictional data):

- **[Open all demos](https://mathi0101.github.io/Fenix-Failures-Dispatcher/demos/)** — one page with all four.
  - [Main app · Realistic mode](https://mathi0101.github.io/Fenix-Failures-Dispatcher/demos/demo-app-modo-realista.html)
  - [Risk profile selector](https://mathi0101.github.io/Fenix-Failures-Dispatcher/demos/mock-perfiles-riesgo.html)
  - [Session results](https://mathi0101.github.io/Fenix-Failures-Dispatcher/demos/mock-resultados-sesion.html)
  - [Main app · Training mode](https://mathi0101.github.io/Fenix-Failures-Dispatcher/demos/demo-app-modo-entrenamiento.html)

⚠️ They are HTML/CSS simulations: **the values shown do not represent real simulator values**. If the link doesn't work yet, GitHub Pages hasn't been enabled yet (see [docs/demos/](docs/demos/) — the files can be opened locally).

## Download

📥 Download the latest version from [Releases](https://github.com/mathi0101/Fenix-Failures-Dispatcher/releases) (single self-contained executable for Windows x64: no .NET installation required).

What you need to run it:

- Windows 10/11 x64
- Microsoft Flight Simulator 2020 or 2024
- Any Fenix Simulations aircraft, with the local API enabled (default port 8083)

## Reporting bugs and feedback

- 🐛 **Bug** → open a [bug report](https://github.com/mathi0101/Fenix-Failures-Dispatcher/issues/new/choose) (guided form).
- ❓ **Question about installing or using the app** → [Q&A in Discussions](https://github.com/mathi0101/Fenix-Failures-Dispatcher/discussions/categories/q-a).
- 💡 **Idea or suggestion** → [Ideas in Discussions](https://github.com/mathi0101/Fenix-Failures-Dispatcher/discussions/categories/ideas), or the *feature request* issue form if it's a concrete proposal.
- 🖼️ **Show off your flights** → [Show and tell](https://github.com/mathi0101/Fenix-Failures-Dispatcher/discussions/categories/show-and-tell).
- 🔒 **Security vulnerability** → see [SECURITY.md](SECURITY.md) (private report; never a public issue).

If you found a bug, the most useful things are: what you were doing, what you expected to happen and what actually happened. If you can, attach a screenshot and the app log for the day (it's in the app's `logs` folder).

✍️ You can write in English or Spanish — both are welcome.

## Privacy and telemetry

The app sends **anonymous usage statistics** to see how it is used and make it better with every version. Flight data, simulator content, routes or anything personal are never sent.

You can turn it off in **Settings → General → "Send anonymous usage statistics"**, and the app works exactly the same without it. With the option off, the only outbound traffic is the update check (GitHub) and SimBrief (if you loaded your ID).

## Hybrid architecture

- **SimConnect** is the source of truth for the simulator's context (MSFS).
- **Fenix** is the source of truth for the aircraft's internal systems and the injection point for failures (`/fenix/failures/manual` and `/fenix/failures/saveManual`).

Fenix models many systems outside the native simulator, which is why sim variables alone are not enough as a single source. SimConnect was chosen over FSUIPC for being the official API, aligned with MSFS 2020/2024 and free of third-party layers (see [docs/project.md](docs/project.md)).

## Stack

.NET 10 · WPF + MVVM · Clean Architecture (Domain / Application / Infrastructure / Integrations.Fenix / Integrations.SimConnect / UI) · EF Core + SQLite · Serilog · `IHost` + DI · xUnit.

## Honest status

- CI builds and runs the test suite on every push/PR to `dev` and `master`; suite **230/230** as of **2026-10-25** (also verified locally, no vulnerable packages per the last check).
- The app ships as a single self-contained win-x64 executable, published by the `Release` workflow when tagging `v*`.
- The **real** MSFS/Fenix integration is **not verified** end to end in a flight session: in DEBUG, DI registers SimConnect and Fenix mocks. **MSFS 2020** compatibility relies on the SimConnect API but hasn't been manually tested yet.
- The **Realistic** mode (engine v2 + UI) is implemented and committed; its remaining limitations are detailed in [docs/realistic-mode/estado-y-diseno.md](docs/realistic-mode/estado-y-diseno.md).

## For developers

Requirements: .NET 10 SDK and the MSFS SimConnect SDK (by default at `MSFS SDK\SimConnect SDK\lib\managed\Microsoft.FlightSimulator.SimConnect.dll`).

Minimal commands (from `Realistic Failures Solution`):

```sh
dotnet restore RealFenixFailures.slnx
dotnet build RealFenixFailures.slnx -c Debug
dotnet test RealFenixFailures.TestUnits/RealFenixFailures.TestUnits.csproj
dotnet run --project RealFenixFailures.UI/RealFenixFailures.UI.csproj
```

Documentation:

- [docs/README.md](docs/README.md) — index of all the documentation.
- [docs/arquitectura.md](docs/arquitectura.md) — layers, components and current flow.
- [docs/desarrollo.md](docs/desarrollo.md) — requirements, commands, migrations and commits.
- [docs/realistic-mode/estado-y-diseno.md](docs/realistic-mode/estado-y-diseno.md) — Realistic mode status and remaining limitations.
- [docs/integraciones/](docs/integraciones/) — SimConnect and Fenix API.
- [docs/CHANGELOG.md](docs/CHANGELOG.md) — technical change log.

Want to contribute? See [CONTRIBUTING.md](CONTRIBUTING.md). For support, see [SUPPORT.md](SUPPORT.md).

## License and donations

This project is under the **Apache License 2.0** (see [LICENSE](LICENSE)). If the software is useful to you and you'd like to support its improvement, you can make a voluntary donation. This is an independent, one-person project: nobody else works on it — it's a hobby fueled by the wish for more realism in every flight. Every dollar helps push the project forward, keeps me motivated to keep improving the app, and lets me put more resources into it.

By contributing, you agree that your contributions will be licensed under the same license (see [CONTRIBUTING.md](CONTRIBUTING.md)).
