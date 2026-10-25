## Requisitos

- Windows 10/11 (64 bits).
- Microsoft Flight Simulator 2024 con el Fenix A320 instalado: la app se comunica con la API local del addon.
- No requiere instalar .NET ni ningún otro componente: todo lo necesario va incluido en el ZIP.

## Cómo instalar

1. Descargá el ZIP de esta versión y extraelo en una carpeta de tu gusto (por ejemplo `C:\FNX Realistic Failures`). No lo extraigas dentro de las carpetas del simulador ni de `Community`.
2. Ejecutá `RealFenixFailures.UI.exe`. Podés crear un acceso directo en el escritorio; la app es portable y no tiene instalador: dejá la carpeta donde está.
3. Si Windows te pregunta sobre permisos de red la primera vez, aceptá: la app habla con la API del Fenix en tu propia PC (puerto 8083 por defecto, configurable en `appsettings.json`).
4. Abrí MSFS con el Fenix y configurá la app desde la pestaña de ajustes (la base de datos y los logs se crean solos en `%AppData%\RealFenixFailures`).

## Cómo actualizar

- Reemplazá los archivos de la carpeta por los del ZIP nuevo (la base de datos y tus preferencias no se tocan: no están en la carpeta).
- La app también avisa sola cuando hay una versión nueva: pestaña Actualizaciones.

## Cómo desinstalar

- Borrá la carpeta de la app y, si querés limpiar todo, también `%AppData%\RealFenixFailures`.

## Problemas comunes

- **No conecta con el simulador**: MSFS 2024 debe estar corriendo en la misma PC antes de intentar conectar.
- **No conecta con el Fenix**: el addon debe estar cargado en el simulador; verificá que la API de Fenix esté habilitada y que el puerto de `appsettings.json` coincida con el del addon.
- Más ayuda y releases en: https://github.com/mathi0101/FNX-Realistic-Failures-System/releases
