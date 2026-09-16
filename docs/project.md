Proyecto: Realistic A320 Fenix Failures

Objetivo:
Desarrollar una aplicación que funcione en segundo plano y se comunique con Fenix Simulations A320 para generar fallas más realistas, dinámicas y contextuales que las disponibles actualmente.

Idea principal:
La app debe monitorear el estado del vuelo, interpretar el contexto operativo y disparar fallas en momentos coherentes según la fase de vuelo, condiciones del avión y lógica de probabilidad configurable.

Arquitectura general planteada:
- La app corre en segundo plano fuera del simulador.
- Se conecta al entorno del simulador mediante SimConnect.
- Se conecta al sistema del Fenix A320 mediante su mecanismo de integración disponible.
- La lógica interna decide cuándo, cómo y bajo qué condiciones generar una falla.
- El objetivo no es solo disparar fallas aleatorias, sino generar eventos plausibles y realistas.

Fuentes de datos previstas:
1. Fenix Simulations
- Lectura y accionamiento de fallas del avión.
- Acceso a variables y estados internos del A320 que no dependen del modelo nativo de MSFS.
- Uso prioritario para sistemas complejos del avión: hidráulico, eléctrico, neumático, combustible, etc.

2. Microsoft Flight Simulator 2024
- Acceso a datos del simulador mediante SimConnect.
- Uso para contexto general del vuelo y variables de entorno.

Por qué SimConnect:
- Es la API oficial de Microsoft Flight Simulator.
- Es la opción recomendada para MSFS 2020 y MSFS 2024.
- Permite leer información como:
  - altitud
  - velocidad
  - posición
  - heading
  - estado general de la aeronave
  - datos de entorno y fase de vuelo
- Es preferible a FSUIPC para este proyecto.

Sobre FSUIPC:
- Es una alternativa posible, pero no la opción principal.
- Requiere una capa externa adicional.
- Tiene más sentido en proyectos legacy o compatibilidad con herramientas antiguas.
- Para MSFS2024 y un proyecto nuevo, conviene priorizar SimConnect.

Limitación importante:
Fenix A320 no depende completamente del modelo interno del simulador.
Eso significa que muchos sistemas complejos no deben tomarse desde SimConnect como fuente principal de verdad.
Por eso la estrategia correcta es:
- SimConnect para contexto general del vuelo
- Fenix para sistemas reales del avión y fallas

Decisión técnica actual:
Usar una arquitectura híbrida:
- SimConnect como fuente de contexto del simulador
- Fenix como fuente real de sistemas y punto de inyección de fallas

Casos de uso esperados:
- Disparar fallas según fase de vuelo
- Generar fallas condicionadas por tiempo, uso o contexto
- Introducir eventos raros pero plausibles
- Simular desgaste o degradación progresiva
- Configurar perfiles de realismo o severidad

Lenguaje y stack previsto:
- C# como opción principal por compatibilidad con SimConnect y entorno Windows
- Aplicación de escritorio o servicio en background
- Posible interfaz futura para configuración de reglas, severidad y tipos de falla

SDK y acceso a SimConnect:
- SimConnect se obtiene desde el SDK oficial de Microsoft Flight Simulator
- No se recomienda depender de paquetes NuGet no oficiales como base del proyecto
- La DLL administrada habitual para C# es:
  Microsoft.FlightSimulator.SimConnect.dll

Ruta típica:
MSFS SDK\SimConnect SDK\lib\managed\Microsoft.FlightSimulator.SimConnect.dll

Instalación del SDK:
- Abrir Microsoft Flight Simulator
- Activar Developer Mode
- Ir a Help
- Usar SDK Installer

Objetivo final del proyecto:
Crear una experiencia de fallas mucho más inmersiva y realista para el Fenix A320, usando lógica contextual, integración con el simulador y control directo sobre los sistemas del avión.