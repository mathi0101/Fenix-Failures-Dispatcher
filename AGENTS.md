# AGENTS.md — Guía para agentes de IA

Guía operativa para trabajar en este repositorio. Léela antes de tocar cualquier cosa; la documentación de dominio vive en `docs/`.

## Proyecto

Aplicación de escritorio **WPF (.NET 10)** que genera fallas realistas del **Fenix A320** en **Microsoft Flight Simulator 2024**, en segundo plano. Arquitectura híbrida: **SimConnect** aporta contexto del simulador y **Fenix** aporta sistemas reales del avión y el punto de inyección de fallas (API HTTP local).

La solución es `Realistic Failures Solution/RealFenixFailures.slnx`. Todos los comandos se ejecutan desde el directorio `Realistic Failures Solution`.

## Comandos

```sh
dotnet restore RealFenixFailures.slnx
dotnet build RealFenixFailures.slnx -c Debug
dotnet build RealFenixFailures.slnx -c Release
dotnet test RealFenixFailures.TestUnits/RealFenixFailures.TestUnits.csproj
dotnet run --project RealFenixFailures.UI/RealFenixFailures.UI.csproj
```

## Layout y dependencias

- `RealFenixFailures.Domain` — entidades, enums, contratos y reglas puras. Sin dependencias de infraestructura.
- `RealFenixFailures.Application` — casos de uso, orquestación, DTOs, contratos de integración. Depende solo de Domain.
- `RealFenixFailures.Infrastructure` — EF Core (DbContext, repositorios, migrations, seed), logging. Depende de Application y Domain.
- `RealFenixFailures.Integrations.Fenix` — cliente HTTP de la API de Fenix y su mock. Depende de Application y Domain.
- `RealFenixFailures.Integrations.SimConnect` — cliente real y mock de SimConnect. Depende de Application y Domain.
- `RealFenixFailures.UI` — WPF + MVVM. Compone el host y toda la DI. Depende de todas las demás.
- `RealFenixFailures.TestUnits` — pruebas xUnit. Depende de Domain y de Integrations.SimConnect.

Regla de capas: las integraciones no conocen lógica de dominio de los modos; la UI consume Application a través de DI; Domain no conoce a nadie.

## Documentación (leer según lo que se toque)

- [docs/project.md](docs/project.md) — visión, alcance y decisiones de diseño (fuente única del "por qué").
- [docs/arquitectura.md](docs/arquitectura.md) — capas, componentes reales y flujo vigente.
- [docs/desarrollo.md](docs/desarrollo.md) — requisitos, comandos, config, migrations, ramas y formato de commits.
- [docs/realistic-mode/estado-y-diseno.md](docs/realistic-mode/estado-y-diseno.md) — modo Realistic: qué está implementado, en desarrollo, pendiente, decisiones y limitaciones restantes.
- [docs/integraciones/simconnect.md](docs/integraciones/simconnect.md) — cliente SimConnect real y mock, contratos y advertencias.
- [docs/integraciones/fenix-api.md](docs/integraciones/fenix-api.md) — endpoints, configuración y cliente Fenix.
- [docs/CHANGELOG.md](docs/CHANGELOG.md) — bitácora técnica de cambios (siempre con entrada nueva por cambio significativo).

## Regla obligatoria de documentación

- Todo cambio significativo en comportamiento, lógica, sistema, esquema, integración o arquitectura actualiza **en el mismo trabajo** el doc de estado afectado y agrega una entrada a [docs/CHANGELOG.md](docs/CHANGELOG.md).
- Refactors triviales o correcciones de typos **no** necesitan entrada.
- Nunca afirmar una prueba que no se haya ejecutado: si no se corrió, se dice "no verificado". Cualquier cambio de integración real (MSFS/Fenix) debe probarse manualmente antes de declararse funcionando.
- No repetir documentación extensa en respuestas o commits: citar el doc y el cambio puntual.

## Migraciones EF

- No editar migraciones ya aplicadas.
- Para cada cambio de esquema: modificar entidades → ajustar `RealFenixDbContext`/seed si corresponde → crear **migración nueva** → revisar `Up` y `Down` antes de aplicarla.

## Git

- `master` estable; `dev` es la rama de integración; trabajar en ramas `feature/*`, `fix/*`, `docs/*`, `refactor/*`, `test/*` o `chore/*` creadas desde `dev`.
- No hacer commits salvo pedido expreso del usuario.
- Commits con **Conventional Commits** (`feat/fix/docs/refactor/test/chore`, opcional scope) y cuerpo de viñetas según [docs/desarrollo.md](docs/desarrollo.md).

## Gotchas verificadas

- Solo Windows: WPF y SimConnect.
- Arquitectura objetivo **x64**: `PlatformTarget=x64` fijado en UI, TestUnits e Integrations.SimConnect; resuelve **MSB3270**. Al tocar esos csproj, no volver a MSIL ni desalinear la arquitectura.
- Fenix corre en la máquina local; la API es configurable por claves y el puerto predeterminado es **8083**.
- `RealFenixFailures.Domain/Resources/failures.json` y `training_presets.json` son **EmbeddedResource indispensables** (el app no funciona sin ellos) y ya están **trackeados/versionados** (el `.gitignore` los excluye del ignore): no borrarlos ni renombrarlos.
- Preservar el EOL y la codificación de los archivos que se editen.
- El WIP del modo **Realistic** ya está committeado como base en `031a9e7` (branch `dev`); la **estabilización** (validación previa, null safety, scope por sesión, compensación, x64, paquetes actualizados) está aplicada en el árbol de trabajo pero **pendiente de commit**: lo commiteará el agente principal, no lo adelantes.

## Afirmaciones sobre modos

- No declarar que los modos **Training** o **Custom** "funcionan" salvo que se hayan probado manualmente en sesión real; lo verificado a la fecha es build y test automatizados.
- La integración real con MSFS/Fenix todavía no está verificada de punta a punta; en DEBUG se usan mocks por DI.
