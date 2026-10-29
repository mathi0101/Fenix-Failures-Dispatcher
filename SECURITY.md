# Security Policy / Política de Seguridad

## Supported versions / Versiones soportadas

The project is in **beta**: only the latest published release is supported. If you are running an older version, please update before reporting an issue.

El proyecto está en **beta**: solo se soporta la última release publicada. Si estás usando una versión anterior, actualizá antes de reportar un problema.

| Version / Versión | Supported / Soportada |
|---|---|
| latest release / última release | ✅ |
| older versions / versiones anteriores | ❌ |

## Reporting a vulnerability / Cómo reportar una vulnerabilidad

🔒 **Please do not open a public issue for security problems.** / 🔒 **Por favor no abras un issue público para problemas de seguridad.**

Preferred channels, in order:

1. **GitHub private vulnerability reporting**: go to the repository's **Security** tab → *Report a vulnerability*. Only the maintainer sees the report.
2. If that is unavailable, contact the maintainer privately by email: **mthiasgonzalez0101@gmail.com** (subject: "Fenix Failures Dispatcher security").

Canales preferidos, en orden:

1. **Reporte privado de vulnerabilidades de GitHub**: andá a la pestaña **Security** del repositorio → *Report a vulnerability*. Solo quien mantiene el proyecto ve el reporte.
2. Si no está disponible, contactá de forma privada por correo: **mthiasgonzalez0101@gmail.com** (asunto: "Fenix Failures Dispatcher security").

Please include / Por favor incluí:

- What the vulnerability is and how it can be exploited / Qué es la vulnerabilidad y cómo se puede explotar
- The affected version / La versión afectada
- Steps to reproduce or a proof of concept / Pasos para reproducirla o una prueba de concepto
- Any log lines or output that help diagnose it / Líneas de log o salida que ayuden a diagnosticarla

## What to expect / Qué esperar

- **Acknowledgment / Acuse de recibo**: within 7 days / dentro de los 7 días.
- **Fix or mitigation / Fix o mitigación**: as soon as possible, depending on severity; you'll be kept informed / lo antes posible según la severidad; se te va a mantener informado/a.
- **Credit / Crédito**: public credit in the release notes if you want it / crédito público en las release notes si lo querés.

## Scope / Alcance

In scope / Dentro del alcance:

- The Real Fenix Failures application itself (code, installer, release artifacts) / La aplicación Real Fenix Failures en sí (código, instalador, artefactos de release)
- How the app stores credentials, logs and telemetry opt-out / Cómo la app guarda credenciales, logs y el opt-out de telemetría

Out of scope / Fuera del alcance:

- Vulnerabilities in the Fenix API, the Fenix aircraft, Microsoft Flight Simulator or SimConnect themselves / Vulnerabilidades de la API de Fenix, del avión Fenix, de Microsoft Flight Simulator o de SimConnect en sí mismos — report those to their vendors / reportalas a sus proveedores
- Social engineering of end users / Ingeniería social contra usuarios finales
- Reports from automated scanners without a demonstrated impact / Reportes de escanners automatizados sin impacto demostrado
