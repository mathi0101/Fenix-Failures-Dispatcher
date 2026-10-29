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

La configuración distribuida vive en `RealFenixFailures.UI/appsettings.json`, listo para ajustar manualmente antes del release. Usa Fenix local y logging Information como punto de partida. La configuración anterior se conserva en `appsettings.Development.json` (incluye IP de testing, mocks y logging Debug).

- **Debug**: carga appsettings.json y superpone appsettings.Development.json (opcional).
- **Release**: carga solo appsettings.json; Development no se copia al output Release ni al publish. La selección depende de la compilación, igual que las integraciones/seeds, no de DOTNET_ENVIRONMENT.
- Ambos se resuelven desde `AppContext.BaseDirectory`, junto al ejecutable, independientemente del directorio de trabajo. Las variables de entorno se aplican al final (claves anidadas con `__`).
- Los defaults de todas las opciones reflejan la configuración actual del motor y SimBrief. La URL por defecto de Fenix sigue siendo localhost: la IP de una PC concreta solo vive en Development. Una sección omitida usa sus defaults. Los perfiles de riesgo son propiedades nombradas en `FailureEngine:RealisticMode` (`Low`/`Moderate`/`High`/`Squawk7700`): pisar un campo de un perfil (p. ej. solo `Low:ProbabilityMultiplier`) conserva los demás campos y perfiles de sus defaults de código — el binder pisa solo las claves presentes.
- **Todas las claves tienen default en código** (opciones de cada sección y los 4 perfiles en `RealisticModeSettings` de `FailureEngineSettings.cs` —única fuente de calibración, cada propiedad documentada con su impacto—; `FailureEngine:Debug=false` y `MaxLogEntries=50` en la misma clase): el appsettings de producción queda mínimo (solo `Serilog`) y suma claves solo como ajuste manual. Única excepción: los overrides de `Serilog:MinimumLevel:Override`, sin los cuales la app funciona con más ruido de logs de framework.
- Las modificaciones requieren reiniciar para garantizar su aplicación: los consumidores pueden capturar las opciones al construirse.

Claves disponibles:

- `FailureEngine` — `Debug` (flag de herramientas de desarrollo: fuerza nivel Debug de Serilog a nivel raíz, activa el log de diagnóstico de SimConnect y hace visible el bloque «Reloj de simulación» del panel Realistic; default `false`), `MaxLogEntries` (límite del log de eventos en vivo de la UI, default 50), `CheckConnectionIntervalSeconds`, `CheckIntervalSeconds` y la sección `RealisticMode` (parámetros del motor Realistic). Los nombres de los catálogos embebidos (`failures.json`, `training_presets.json`, `realistic_presets.json`) **no son configurables**: son constantes en `RealFenixFailures.Domain/Services/EmbeddedResourceNames.cs` (quedaron fuera de appsettings).
- `FenixApi` — `UseMock` (solo Debug), `BaseUrl`, `Port` (predeterminado 8083), `ManualFailuresPath`, `SaveManualPath`, `HealthCheckIntervalSeconds`, `HealthCheckTimeout`.
- `SimBriefApi` — `BaseUrl`, `DownloadTimeoutSeconds`.
- `Telemetry` — ping anónimo de uso (ver [ui/telemetria.md](ui/telemetria.md)): `Enabled` (default `true`), `PostHogProjectApiKey` (token público de ingesta `phc_...`; **vacío en el repo**: el workflow Release lo inyecta desde el secreto `POSTHOG_PROJECT_API_KEY`; en desarrollo llega por User Secrets o variable de entorno `Telemetry__PostHogProjectApiKey`; vacío = no se envía nada), `PostHogHost` (default `https://us.i.posthog.com`). En builds DEBUG nunca se envía.
- `Serilog` — nivel mínimo y overrides. El sink de archivo **no** se configura acá: las rutas viven en el código (ver `AppPaths`). El ex flag `SimConnectDiagnostics:Enabled` fue absorbido por `FailureEngine:Debug`.

## Datos del usuario (BD, preferencias, logs)

Todo lo persistente del usuario vive en una sola carpeta base (**%AppData%\RealFenixFailures**), centralizada en `RealFenixFailures.Infrastructure/AppPaths.cs`:

- BD SQLite: `%AppData%\RealFenixFailures\realfenixfailures.db` (connection string calculado en código; **no** es configurable por appsettings).
- Preferencias: `%AppData%\RealFenixFailures\userprefs.json` (panel de Ajustes).
- Logs Serilog (rolling diario): `%AppData%\RealFenixFailures\logs\realfenix-.log` y `logs\realfenix-*.log` / `simconnect-diagnostic-.log` (flag de diagnóstico).

La carpeta del ejecutable es solo "programa": así los datos sobreviven a reinstalaciones y funcionan aun si la app vive en una carpeta protegida (Program Files) o sincronizada (OneDrive).

## Migraciones EF (vigente desde la primera pre-release)

El arranque usa `Database.MigrateAsync()` antes de los initializers: una base nueva recibe `InitialRelease` y `__EFMigrationsHistory`; una existente con historial recibe solo las migraciones pendientes. La inicial incluye el modelo actual completo y los seeds estáticos (tipos de preset y sistemas de desgaste). Catálogos/presets embebidos siguen cargándose en los initializers; la aeronave de ejemplo sigue siendo exclusiva de Debug.

**Transición única de bases anteriores a la pre-release:** las bases creadas con EnsureCreated, y las que tengan un historial anterior a esta nueva inicial, no son compatibles automáticamente con InitialRelease. Con la app cerrada, respaldar la carpeta de datos y retirar la base de desarrollo del path activo para que el siguiente arranque cree una nueva con migraciones. Retirar también los sidecars SQLite si existen después de cerrar todos los procesos. Esto reinicia aeronaves, sesiones y desgaste del entorno; las preferencias y logs se conservan. No se implementa importación ni baseline automático. **Esta tarea no borra ni modifica las bases locales o del share de testing**; la transición se hace manualmente y con backup. No aplicar database update directamente sobre esas bases antiguas.

De ahora en adelante: cambiar entidad/modelo/seed → generar una migración con nombre descriptivo → revisar Up/Down → probar en una base aislada → versionar migración + Designer + snapshot. Nunca borrar la base para aplicar cambios futuros ni modificar migraciones ya compartidas. Los tests de reglas pueden seguir con EnsureCreated en memoria; las pruebas de migración ejercitan Migrate, reinicio y rollback/reaplicación.

Desde `Realistic Failures Solution`, con dotnet-ef de la misma versión que los paquetes EF (10.0.13):

```sh
dotnet ef migrations add NombreDeLaMigracion --project RealFenixFailures.Infrastructure/RealFenixFailures.Infrastructure.csproj --startup-project RealFenixFailures.UI/RealFenixFailures.UI.csproj --context RealFenixDbContext --configuration Release

dotnet ef database update --project RealFenixFailures.Infrastructure/RealFenixFailures.Infrastructure.csproj --startup-project RealFenixFailures.UI/RealFenixFailures.UI.csproj --context RealFenixDbContext --configuration Release

dotnet ef migrations list --project RealFenixFailures.Infrastructure/RealFenixFailures.Infrastructure.csproj --startup-project RealFenixFailures.UI/RealFenixFailures.UI.csproj --context RealFenixDbContext --configuration Release

dotnet ef migrations remove --project RealFenixFailures.Infrastructure/RealFenixFailures.Infrastructure.csproj --startup-project RealFenixFailures.UI/RealFenixFailures.UI.csproj --context RealFenixDbContext --configuration Release
```

La factory de diseño usa la misma ruta local de AppData: `migrations add` no aplica cambios a la base, pero `database update` sí. Para probar fuera de AppData usar `--connection "Data Source=ruta-a-base-de-prueba.db"` con database update. No apuntar al share vivo. Si el build interno de EF falla sin diagnóstico, compilar explícitamente y reintentar el comando EF con `--no-build`.

## Checklist: publicar una versión (v1 en adelante)

**Antes de publicar v1.0.0 (releases de GitHub):**

1. **Migración inicial**: InitialRelease ya está generada y el arranque usa MigrateAsync. Mantener sus tres archivos versionados y verificar una base nueva con historial; completar la transición manual de bases de desarrollo anteriores antes de usar el nuevo arranque.
2. **Versión**: actualizar `<Version>` en `Realistic Failures Solution/Directory.Build.props` (p. ej. `1.0.0`) y crear el tag `v1.0.0` en GitHub coincidiendo con ese valor.
3. **Datos del usuario**: verificar que `%AppData%\RealFenixFailures\` contenga BD, preferencias y logs (ya centralizado en `AppPaths`; la carpeta del exe no debe quedar con `realfenixfailures.db` ni `logs/`).
4. **Distribución**: compilar `dotnet publish` y publicar ZIP portable en GitHub Releases (pre-releases para testers: marca "Set as a pre-release"). La pestaña Actualizaciones de Ajustes las detecta.
5. **AppSettings de producción**: ajustar manualmente appsettings.json, comprobar endpoints/parámetros/logging y verificar que Development no viaje en el publish/ZIP.

**De v1.1 en adelante:** cada cambio de esquema → migración nueva incremental (nunca editar las aplicadas), versión `X.Y.0` + tag en el mismo commit del release.

### Runbook de release (dev → master → tag → GitHub Release)

Ciclo completo para publicar una versión. La fuente de verdad del número es `<Version>` en `Realistic Failures Solution/Directory.Build.props` (el tag `vX.Y.Z` debe coincidir con ese valor); la pestaña Actualizaciones de la app detecta releases de GitHub y distingue estables de pre-releases.

1. **Precondiciones de calidad** (en `dev`, con las features del release mergeadas): build Debug y Release 0 errores / 0 advertencias, `dotnet test` en verde, auditoría de paquetes sin vulnerables, smoke test manual si el release toca integración real (MSFS/Fenix) y CHANGELOG al día.

2. **Elegir versión (SemVer)**: feature nueva → `0.Y.0`; fix → `0.Y.Z`; ruptura de esquema/datos pre-1.0 → `0.Y.0` (antes de 1.0 la API interna puede cambiar en minor). Para canal de testers: `X.Y.Z-beta.N` marcado como pre-release.

3. **Cerrar la versión en `dev`**: actualizar `<Version>` en `Directory.Build.props` quitando el sufijo `-dev` (p. ej. `0.2.0`), commit `chore(release): v0.2.0` (solo ese archivo) y push a `dev`.

4. **`dev` → `master`**: PR `gh pr create --base master --head dev --title "Release v0.2.0"` y merge, o merge directo mientras el ruleset de protección tenga bypass para admin. Si activás "Require a pull request" en el ruleset, este paso es siempre PR.

5. **Tag anotado** (en `master` actualizado, el commit del merge):

   ```sh
   git tag -a v0.2.0 -m "Release v0.2.0"
   git push origin master --follow-tags
   ```

6. **Publicar el build portable**: automático con el workflow `Release` (`.github/workflows/release.yml`): al pushear el tag, GitHub Actions restaura, compila en Release y corre la suite completa (**gate**: si los tests fallan no se publica nada), después compila el publish single-file self-contained win-x64, publica en una carpeta explícita (`${{ runner.temp }}/RealFenixFailures-publish`), arma el ZIP desde esa misma carpeta (README.md generado en el momento: instrucciones de instalación de `docs/releases/README-INSTALACION.md` + notas curadas de `docs/releases/vX.Y.Z.md` si existe, exe + `Microsoft.FlightSimulator.SimConnect.dll` + `SimConnect.dll` nativa + `appsettings.json`, sin BD/logs) y crea el release. **Anuncio automático en Discussions**: si el release se publicó OK, un paso final crea en **Announcements** la discussion `<tag> released` con el cuerpo de `docs/releases/vX.Y.Z.md` (o el body del release) más el link de descarga; es best-effort (su fallo no marca el release como fallido) y el flag "announcement" del post se marca a mano (la API de GitHub no lo expone). No busca ejecutables recursivamente en bin/Release; si falta un archivo requerido, el empaquetado falla. A mano sería `dotnet publish RealFenixFailures.UI/RealFenixFailures.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true` + zipear esos cinco archivos. El `SimConnect.dll` nativo es **obligatorio** junto al exe: sin él el wrapper managed (mixto C++/CLI) no carga y la app falla al conectar (ver [simconnect](integraciones/simconnect.md)).

7. **Release notes curadas y GitHub Release** (desde `master`): las notas de cada release se curan en `docs/releases/vX.Y.Z.md` — lenguaje para el usuario final (qué cambia para su juego, sin jerga técnica), con secciones p. ej. Novedades / Mejoras / Correcciones / Actualización y el link *Full Changelog* `vAnterior...vNuevo`. El agente la genera **antes de crear el tag** a partir de `git log vAnterior..HEAD --oneline` y de las entradas de [CHANGELOG.md](CHANGELOG.md) posteriores a la fecha del tag anterior; se commitea en `dev` para que entre al merge a `master`. El workflow `Release` al pushear el tag usa ese archivo como cuerpo del release (`--notes-file docs/releases/vX.Y.Z.md`) si existe; si no, cae a `--generate-notes` (lista cruda de commits). Los tags con sufijo, p. ej. `v0.2.0-beta.1`, se marcan pre-release y el updater los muestra como tales. A mano:

   ```sh
   gh release create v0.2.0 --target master --notes-file docs/releases/v0.2.0.md --title "v0.2.0" ruta/al/zip.zip
   ```

8. **Post-release**: subir `<Version>` en `Directory.Build.props` de `dev` a la próxima de desarrollo (p. ej. `0.3.0-dev.0`) y push. Verificar manualmente que la pestaña Actualizaciones detecta el release publicado.

9. **Hotfix**: rama `fix/*` desde `master` → PR/merge a `master` → release `v0.Y.(Z+1)` (pasos 3, 5, 6, 7 pero con `<Version>` y tag patch, sin pasar por `dev`) → back-merge del fix a `dev` para no perderlo.

10. **CI/CD (GitHub Actions)**: `ci.yml` compila y corre la suite en cada push a `dev`/`master` y en cada PR (runner windows-latest limpio: si algo solo anda por dependencias locales de la PC, el CI lo delata). `release.yml` automatiza los pasos 6-7 con el push del tag `v*` (y admite ejecución manual `workflow_dispatch` que solo arma y sube el ZIP como artefacto de prueba, sin crear release). El workflow de release asume publish **self-contained** (no exige .NET Desktop Runtime en las PCs de los usuarios).

10. **Protección y PRs (reglas vigentes)**: el ruleset `Protección dev/master` (Settings → Rules → Rulesets) bloquea borrado y force-push de ambas ramas con bypass "Always" para el rol admin. Forzar PR en `master` es recomendable **recién cuando haya colaboradores con write** (mismo ruleset: activar "Require a pull request before merging"); trabajando solo, el bypass ya deja el push directo y un PR por release solo agrega fricción. Opcional: segundo ruleset sobre tags `v*` (target tags, mismas reglas de borrado/force-push).

### Sugerencia de versión por tarea de agente

Cada tarea de agente que cambie código cierra su resumen con un **bump de versión sugerido, no aplicado**: `feat` → minor, `fix` → patch, `docs`/`refactor`/`test`/`chore` → sin bump (queda pendiente para la próxima tarea que sí lo amerite). El número se calcula desde el `<Version>` vigente de `Directory.Build.props` y se informa como `Bump sugerido: X.Y.Z(-dev.N)`.

El agente no edita `Directory.Build.props` ni crea tags: la versión se define recién en los pasos 2-3 y 8 del runbook (elegir/cerrar/post-release), a decisión del usuario. Así se evitan conflictos multi-agente sobre ese archivo y bumps inflados por cambios que no ameritan release.

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
