# Realistic A320 Fenix Failures

Aplicación de escritorio **WPF (.NET 10)** que se ejecuta en segundo plano mientras volás el **Fenix A320** en **Microsoft Flight Simulator 2024**, para generar fallas más realistas, dinámicas y contextuales que las del simulador por defecto.

## Qué hace

- Corre en background junto al simulador.
- Lee contexto general del vuelo (fase, altitud, velocidad, estado) desde MSFS.
- Consulta y dispara fallas reales del Fenix A320 vía su API local.
- Evalúa probabilidades por fase de vuelo y persistencia de sesiones y fallas en SQLite.
- Tres modos: **Training** (presets armados), **Custom** (presets propios) y **Realistic** (motor probabilístico en desarrollo).
- UI WPF + MVVM con log en tiempo real de eventos de vuelo.

## Arquitectura híbrida

- **SimConnect** es la fuente de verdad del contexto del simulador (MSFS 2024).
- **Fenix** es la fuente de verdad de sistemas internos del avión y el punto de inyección de fallas (`/fenix/failures/manual` y `/fenix/failures/saveManual`).

El Fenix modela muchos sistemas fuera del simulador nativo, por eso las variables del sim no alcanzan como única fuente. SimConnect se eligió sobre FSUIPC por ser la API oficial, estar alineada con MSFS 2020/2024 y evitar capas de terceros (ver [docs/project.md](docs/project.md)).

## Stack

.NET 10 · WPF + MVVM · Clean Architecture (Domain / Application / Infrastructure / Integrations.Fenix / Integrations.SimConnect / UI) · EF Core + SQLite · Serilog · `IHost` + DI · xUnit.

## Estado honesto

- Build Debug y Release pasan a fecha **2026-10-14** con **0 errores y 0 advertencias** en ambos; tests automatizados **29/29**; `dotnet list RealFenixFailures.slnx package --vulnerable --include-transitive` reporta **ningún paquete vulnerable**.
- La integración **real** MSFS/Fenix **no está verificada** de punta a punta: en DEBUG la DI registra mocks de SimConnect y Fenix.
- El modo **Realistic** está **en desarrollo**: el WIP base quedó committeado como `031a9e7` (branch `dev`) y la estabilización de 2026-10-14 (validación previa, null safety, scope por sesión, compensación, x64, paquetes actualizados) está aplicada en el árbol pero **aún sin commitear** (el commit queda a cargo del encargado principal).

## Requisitos

- Windows 10/11, .NET 10 SDK, MSFS 2024 con modo desarrollador y SDK oficial (SimConnect).
- Fenix Simulations A320 corriendo con la API local habilitada (puerto predeterminado 8083).
- MSFS SimConnect SDK (por defecto en `MSFS SDK\SimConnect SDK\lib\managed\Microsoft.FlightSimulator.SimConnect.dll`).

## Comandos mínimos (desde `Realistic Failures Solution`)

```sh
dotnet restore RealFenixFailures.slnx
dotnet build RealFenixFailures.slnx -c Debug
dotnet test RealFenixFailures.TestUnits/RealFenixFailures.TestUnits.csproj
dotnet run --project RealFenixFailures.UI/RealFenixFailures.UI.csproj
```

## Documentación

- [docs/README.md](docs/README.md) — índice de toda la documentación.
- [docs/arquitectura.md](docs/arquitectura.md) — capas, componentes y flujo vigente.
- [docs/desarrollo.md](docs/desarrollo.md) — requisitos, comandos, migrations y commits.
- [docs/realistic-mode/estado-y-diseno.md](docs/realistic-mode/estado-y-diseno.md) — estado del modo Realistic y limitaciones restantes.
- [docs/integraciones/](docs/integraciones/) — SimConnect y Fenix API.
- [docs/CHANGELOG.md](docs/CHANGELOG.md) — bitácora técnica de cambios.

## Licencia y donaciones

Proyecto bajo **Licencia Apache 2.0** (ver [LICENSE](LICENSE)). Si el software te resulta útil y querés apoyar su mejora, podés realizar una donación voluntaria.
