# Desarrollo

Guía operativa para desarrollar, migrar y commitear. Todos los comandos se ejecutan desde el directorio `Realistic Failures Solution`, salvo indicación contraria.

## Requisitos

- Windows 10/11.
- .NET 10 SDK.
- MSFS 2024 con modo desarrollador y SDK oficial (SimConnect) para integración real.
- Fenix Simulations A320 con API local disponible para pruebas reales.

## Comandos

```sh
dotnet restore RealFenixFailures.slnx
dotnet build RealFenixFailures.slnx -c Debug
dotnet build RealFenixFailures.slnx -c Release
dotnet test RealFenixFailures.TestUnits/RealFenixFailures.TestUnits.csproj
dotnet list RealFenixFailures.slnx package --vulnerable --include-transitive
dotnet run --project RealFenixFailures.UI/RealFenixFailures.UI.csproj
```

## Configuración (por nombres de claves)

La configuración vive en `RealFenixFailures.UI/appsettings.json`. Acá se listan las claves; los valores son locales de cada máquina y no se copian a la documentación:

- `ConnectionStrings:Sqlite` — cadena de conexión SQLite.
- `FailureEngine` — `CheckConnectionIntervalSeconds`, `CheckIntervalSeconds`, `FailuresJson`, `TrainingPresetsJson` (los dos últimos nombran los EmbeddedResource del Domain).
- `FenixApi` — `BaseUrl`, `Port` (predeterminado 8083), `ManualFailuresPath`, `SaveManualPath`, `HealthCheckIntervalSeconds`, `HealthCheckTimeout`.
- `Serilog` — nivel mínimo y `WriteTo:File` (`logs/realfenix-.log`, rolling diario).

## Migraciones EF

Rutas desde `Realistic Failures Solution` (`--project` es Infrastructure y `--startup-project` es UI):

```sh
dotnet ef migrations add NombreDeLaMigracion --project RealFenixFailures.Infrastructure/RealFenixFailures.Infrastructure.csproj --startup-project RealFenixFailures.UI/RealFenixFailures.UI.csproj --context RealFenixDbContext
dotnet ef database update --project RealFenixFailures.Infrastructure/RealFenixFailures.Infrastructure.csproj --startup-project RealFenixFailures.UI/RealFenixFailures.UI.csproj --context RealFenixDbContext
dotnet ef migrations list --project RealFenixFailures.Infrastructure/RealFenixFailures.Infrastructure.csproj --startup-project RealFenixFailures.UI/RealFenixFailures.UI.csproj --context RealFenixDbContext
dotnet ef migrations remove --project RealFenixFailures.Infrastructure/RealFenixFailures.Infrastructure.csproj --startup-project RealFenixFailures.UI/RealFenixFailures.UI.csproj --context RealFenixDbContext
```

Reglas:

- No editar migraciones ya aplicadas en entornos compartidos: siempre crear migración nueva (en `RealFenixFailures.Infrastructure/Migrations`).
- Revisar el código generado antes de aplicarlo: validar `Up` y `Down`, que no haya drop/create involuntario y que los `UpdateData` del seed sean correctos.
- Al cambiar entidades: entidad en Domain → ajuste en `RealFenixDbContext` → seed si corresponde → migración nueva → aplicar → build Debug y Release.

## Validación

- Antes de terminar un cambio: build Debug y Release + `dotnet test` sin errores; auditoría de dependencias (`dotnet list RealFenixFailures.slnx package --vulnerable --include-transitive`) sin paquetes vulnerables y builds con **0 advertencias**.
- Pruebas de integración real (MSFS/Fenix): manuales; no declararlas como pasadas si no se ejecutaron.
- Comprobación documental al final: `git diff --check` y revisión de enlaces locales y referencias viejas en docs.

### Advertencias conocidas de build (históricas, resueltas)

Históricas: se registran para evitar regresión y no como avisos presentes. Los builds Debug y Release pasan a **0 advertencias** desde el 2026-10-14.

- `MSB3270` (SimConnect AMD64 frente a MSIL): **resuelto** fijando `PlatformTarget=x64` en UI, TestUnits e Integrations.SimConnect. Al tocar esos csproj, no volver a MSIL ni cambiar la arquitectura objetivo; verificar el build de la integración.
- `NU1903` (avisos de seguridad alta; `SQLitePCLRaw` 2.1.11 y `System.Security.Cryptography.Xml` 10.0.8 al 2026-10-14): **resuelto** actualizando a `SQLitePCLRaw.bundle_e_sqlite3` 3.0.5 y `System.Security.Cryptography.Xml` 10.0.13. Si un paquete vuelve a dar NU1903, actualizarlo de forma controlada y registrar el cambio en el CHANGELOG.
- Advertencias de nulabilidad, `ExecutionEngineException` obsoleta y variables de excepción sin uso: **corregidas** en la estabilización; no reintroducirlas al editar.

## Ramas y commits

- `master` estable; `dev` es la rama de integración.
- Ramas de trabajo desde `dev`: `feature/*`, `fix/*`, `docs/*`, `refactor/*`, `test/*`, `chore/*`.
- No commitear sin pedido expreso.
- No reescribir historia existente (sin rebase/amend sobre commits ya compartidos).
- Todo cambio significativo actualiza el doc de estado afectado y agrega entrada a [CHANGELOG.md](CHANGELOG.md) en el mismo trabajo (refactors triviales y typos no necesitan entrada).

### Formato obligatorio de commits

Asunto con Conventional Commits `tipo(scope): descripción`, tipos permitidos `feat`, `fix`, `docs`, `refactor`, `test`, `chore`; asunto claro y breve en imperativo. Línea en blanco y cuerpo detallado donde cada implementación va en una línea que empieza con un guion `- ...`. Nunca afirmar en el cuerpo una prueba que no se haya ejecutado.

Ejemplo de formato:

```
feat(campaigns): exponer reglas y sincronizar sus parámetros

- SNBD-136: incluye las reglas y condiciones asociadas en el detalle de campaña.
- Sincroniza los RuleParameters con origen campaign.validity al modificar la vigencia.
- Conserva sin cambios los parámetros definidos manualmente.
- SNBD-268: valida la creación y persistencia de RuleParameters para valores manuales y vigencia de campaña.
- Agrega pruebas de integración para creación, consulta y actualización de reglas.
```
