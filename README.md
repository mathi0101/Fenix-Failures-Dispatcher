# Real Fenix Failures

[![CI](https://github.com/mathi0101/Fenix-Failures-Dispatcher/actions/workflows/ci.yml/badge.svg)](https://github.com/mathi0101/Fenix-Failures-Dispatcher/actions/workflows/ci.yml)

Aplicación de escritorio para **Microsoft Flight Simulator 2020/2024** que genera **fallas realistas para los aviones de Fenix Simulations**, corriendo en segundo plano mientras volás.

> 🚧 **El proyecto está en beta.** El objetivo de esta etapa es probarla en vuelos reales, juntar feedback y corregir todos los bugs posibles para llegar a una **versión 1.0.0 bien pulida**. Si volás con Fenix, sos justo la persona que busco: [descargala](#descarga) y [contame todo lo que encuentres](#reportar-bugs-y-feedback).

## Por qué existe

Las fallas del simulador son pobres y aleatorias. Esta app dispara fallas que tienen sentido: dependen de la fase de vuelo, de las condiciones de la aeronave y del desgaste que el avión va acumulando vuelo a vuelo. No es "se rompe algo al azar", es "algo pasa porque tiene que pasar".

Es para los que les gusta aprender más o poner en práctica los SOPs y flows realistas de Airbus: falla lo que tiene que fallar, y te toca a vos diagnosticarlo y resolverlo como corresponde.

## Qué hace

- Corre en background junto al simulador, con log en tiempo real de eventos de vuelo.
- Lee el contexto del vuelo (fase, altitud, velocidad, estado) desde MSFS vía SimConnect.
- Consulta y dispara fallas reales del avión vía la API local de Fenix.
- Evalúa probabilidades por fase de vuelo y persiste sesiones, fallas disparadas, aeronaves del usuario y su desgaste en SQLite.
- Incluye un modo **Realistic** donde el avión envejece y se degrada con el uso, como un avión de verdad.

## Modos

- **Training** — presets armados para practicar escenarios puntuales.
- **Custom** — armás tus propios presets.
- **Realistic** — motor probabilístico con desgaste persistente y **4 perfiles de riesgo** para cubrir la identidad y el realismo que quiera tener cada piloto en la cabina:
  - **Bajo** — para vuelos tranquilos, sabiendo que vas a llegar a destino.
  - **Moderado** — el punto medio.
  - **Alto** — más probabilidad, agravación y fallas en tierra.
  - **7700** — para los más experimentados: el logro no es llegar a destino, sino salvar a tus pasajeros y tripulación de un trágico accidente.

## Descarga

📥 Descargá la última versión desde [Releases](https://github.com/mathi0101/Fenix-Failures-Dispatcher/releases) (ejecutable único, autocontenido para Windows x64: no requiere instalar .NET).

Qué necesitás para usarla:

- Windows 10/11 x64
- Microsoft Flight Simulator 2020 o 2024
- Cualquier avión de Fenix Simulations, con la API local habilitada (puerto predeterminado 8083)

## Reportar bugs y feedback

🐞 Abrí un [issue](https://github.com/mathi0101/Fenix-Failures-Dispatcher/issues) o pasame el reporte por donde prefieras. Todo feedback sirve: bugs, ideas, cosas confusas de la interfaz, lo que sea.

Si encontrás un bug, lo más útil es: qué estabas haciendo, qué esperabas que pasara y qué pasó finalmente. Si podés, adjuntá captura de pantalla y el log del día (queda en la carpeta `logs` de la app).

## Arquitectura híbrida

- **SimConnect** es la fuente de verdad del contexto del simulador (MSFS).
- **Fenix** es la fuente de verdad de los sistemas internos del avión y el punto de inyección de fallas (`/fenix/failures/manual` y `/fenix/failures/saveManual`).

El Fenix modela muchos sistemas fuera del simulador nativo, por eso las variables del sim no alcanzan como única fuente. SimConnect se eligió sobre FSUIPC por ser la API oficial, estar alineada con MSFS 2020/2024 y evitar capas de terceros (ver [docs/project.md](docs/project.md)).

## Stack

.NET 10 · WPF + MVVM · Clean Architecture (Domain / Application / Infrastructure / Integrations.Fenix / Integrations.SimConnect / UI) · EF Core + SQLite · Serilog · `IHost` + DI · xUnit.

## Estado honesto

- El CI compila y corre la suite en cada push/PR a `dev` y `master`; suite **230/230** a la fecha **2026-10-25** (verificada también en local, sin paquetes vulnerables según el último chequeo).
- La app se distribuye como ejecutable único self-contained win-x64, publicado por el workflow `Release` al taguear `v*`.
- La integración **real** MSFS/Fenix **no está verificada** de punta a punta en una sesión de vuelo: en DEBUG la DI registra mocks de SimConnect y Fenix. La compatibilidad con **MSFS 2020** se apoya en la API de SimConnect pero aún no se probó manualmente.
- El modo **Realistic** (motor v2 + UI) está implementado y commiteado; sus limitaciones restantes están detalladas en [docs/realistic-mode/estado-y-diseno.md](docs/realistic-mode/estado-y-diseno.md).

## Para desarrolladores

Requisitos: .NET 10 SDK y MSFS SimConnect SDK (por defecto en `MSFS SDK\SimConnect SDK\lib\managed\Microsoft.FlightSimulator.SimConnect.dll`).

Comandos mínimos (desde `Realistic Failures Solution`):

```sh
dotnet restore RealFenixFailures.slnx
dotnet build RealFenixFailures.slnx -c Debug
dotnet test RealFenixFailures.TestUnits/RealFenixFailures.TestUnits.csproj
dotnet run --project RealFenixFailures.UI/RealFenixFailures.UI.csproj
```

Documentación:

- [docs/README.md](docs/README.md) — índice de toda la documentación.
- [docs/arquitectura.md](docs/arquitectura.md) — capas, componentes y flujo vigente.
- [docs/desarrollo.md](docs/desarrollo.md) — requisitos, comandos, migrations y commits.
- [docs/realistic-mode/estado-y-diseno.md](docs/realistic-mode/estado-y-diseno.md) — estado del modo Realistic y limitaciones restantes.
- [docs/integraciones/](docs/integraciones/) — SimConnect y Fenix API.
- [docs/CHANGELOG.md](docs/CHANGELOG.md) — bitácora técnica de cambios.

## Licencia y donaciones

Proyecto bajo **Licencia Apache 2.0** (ver [LICENSE](LICENSE)). Si el software te resulta útil y querés apoyar su mejora, podés realizar una donación voluntaria.
