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

## Migraciones EF (modo desarrollo)

**En desarrollo no se mantienen migraciones.** `App.xaml.cs` usa `Database.EnsureCreatedAsync()`: el esquema se crea **completo desde el modelo actual** en cada arranque si la base no existe (incluye los seeds `HasData`). No hay historial de migraciones ni `__EFMigrationsHistory`.

- Al cambiar entidades: entidad en Domain → ajuste en `RealFenixDbContext` → seed si corresponde → **borrar la base local** (`realfenixfailures.db`; la app la recrea sola en el próximo arranque). No hace falta generar, revisar ni aplicar migraciones.
- Las bases de la app son artefactos regenerables: `Realistic Failures Solution/realfenixfailures.db` (CWD de la solución), `RealFenixFailures.UI/realfenixfailures.db` (CWD del proyecto) y las de `bin/`.
- Los tests corren con `EnsureCreated()` sobre SQLite en memoria: no dependen de migraciones.
- **Cuando el esquema esté cercano a la v1**: generar una única migración inicial (`dotnet ef migrations add InitialRelease --project RealFenixFailures.Infrastructure/RealFenixFailures.Infrastructure.csproj --startup-project RealFenixFailures.UI/RealFenixFailures.UI.csproj --context RealFenixDbContext --configuration Release`) y volver a `MigrateAsync()` para entornos compartidos.

Comandos de EF (para cuando se retome el flujo clásico, desde `Realistic Failures Solution`; `--project` Infrastructure, `--startup-project` UI):

```sh
dotnet ef migrations add NombreDeLaMigracion --project RealFenixFailures.Infrastructure/RealFenixFailures.Infrastructure.csproj --startup-project RealFenixFailures.UI/RealFenixFailures.UI.csproj --context RealFenixDbContext --configuration Release
dotnet ef database update --project RealFenixFailures.Infrastructure/RealFenixFailures.Infrastructure.csproj --startup-project RealFenixFailures.UI/RealFenixFailures.UI.csproj --context RealFenixDbContext --configuration Release
dotnet ef migrations list --project RealFenixFailures.Infrastructure/RealFenixFailures.Infrastructure.csproj --startup-project RealFenixFailures.UI/RealFenixFailures.UI.csproj --context RealFenixDbContext --configuration Release
dotnet ef migrations remove --project RealFenixFailures.Infrastructure/RealFenixFailures.Infrastructure.csproj --startup-project RealFenixFailures.UI/RealFenixFailures.UI.csproj --context RealFenixDbContext --configuration Release
```

Reglas del flujo clásico (v1/compartido): no editar migraciones ya aplicadas en entornos compartidos — siempre crear migración nueva (en `RealFenixFailures.Infrastructure/Migrations`); revisar el código generado antes de aplicarlo (validar `Up` y `Down`, que no haya drop/create involuntario y que los `UpdateData` del seed sean correctos).

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

### Trabajo multi-agente (misma PC, misma rama)

Varios agentes de IA y el usuario editan y debuggean a la vez, en la misma PC y sobre la misma rama; el árbol de trabajo es el estado compartido y cambia sin aviso. Antes de editar, compilar o tocar el índice, contemplar que otra tarea puede estar trabajando o haber committeado en el medio:

- Releer los archivos justo antes de editarlos y aplicar los cambios sobre la versión vigente; no pisar ediciones, formato ni archivos de otras tareas.
- Verificar siempre `git status` y `git diff` antes de operar sobre el índice (staging, stash, checkout): otro agente puede estar commiteando en paralelo.
- **Cada tarea commitea solo sus cambios relacionados**: preparar explícitamente los archivos propios (`git add <archivos>`), nunca `git add -A`/`git add .`; revisar `git status` + `git diff --cached` antes del commit y dejar fuera (sin preparar) todo lo que no es de la tarea.
- No deshacer ni revertir cambios ajenos (checkout, stash, clean, reset, amend) sin pedido expreso del usuario; el árbol puede contener WIP de otra tarea.

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

## Fines de línea (EOL): política y autoridad

- **Autoridad**: `.gitattributes` (Git) y `.editorconfig` (editores) en la raíz del repositorio. Objetivo: **LF** para los textos en el árbol de trabajo y en el índice; excepción: **CRLF** solo en `.bat`/`.cmd`. Binarios declarados `-text` en `.gitattributes`: `dll`, `exe`, `png`, `jpg`, `jpeg`, `gif`, `ico`, `pdf`, `zip`.
- **Configuración local requerida**: `git config --local core.autocrlf false` y `git config --local core.eol lf`. Con `.gitattributes` no usar `core.autocrlf=true` (conversiones dobles y warnings). No se toca la configuración global ni `core.safecrlf`.
- **Archivos nuevos**: respetan la política (LF salvo `.bat`/`.cmd` CRLF). La política no impone indentación, trim de espacios, BOM, encoding ni nueva línea final: conservar el formato existente de los archivos que se editen.
- **Mantenimiento**: respetar esta política al editar; los avisos informativos de conversión no generan tareas ni reportes separados. Una renormalización masiva es excepcional. `git add --renormalize .` prepara también cambios funcionales existentes: usarlo solo cuando corresponda incluirlos en el commit, nunca como corrección automática de rutina.
