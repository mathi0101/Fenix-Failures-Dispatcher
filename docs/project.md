# Proyecto

## Visión

Generar fallas realistas del Fenix A320 en Microsoft Flight Simulator 2024 que tengan sentido en contexto, sean plausibles durante la operación y agreguen desafío significativo a cada vuelo: no fallas aleatorias, sino eventos coherentes con la fase de vuelo, las condiciones del avión y la lógica de probabilidad configurable.

## Alcance

Aplicación de escritorio WPF (.NET 10) que corre en segundo plano mientras el usuario vuela:

- Lee contexto del simulador y del avión.
- Interpreta la fase de vuelo y las condiciones operativas.
- Dispara fallas reales en el Fenix A320 a través de su API local.
- Persiste sesiones, fallas disparadas, aeronaves del usuario y su desgaste.

## Fuentes de verdad

- **SimConnect** — fuente de verdad del *contexto* del simulador: altitud, posición, velocidad, heading, fase de vuelo, entorno. API oficial de MSFS.
- **Fenix** — fuente de verdad de los *sistemas reales del avión* y punto de *inyección de fallas*. El Fenix modela muchos sistemas fuera del modelo nativo de MSFS, por lo que las variables del simulador no bastan como única fuente.

Consecuencia de diseño: arquitectura híbrida, SimConnect para contexto general y Fenix para sistemas y fallas.

## Objetivos

- Disparar fallas según fase de vuelo.
- Generar fallas condicionadas por tiempo, uso o contexto.
- Introducir eventos raros pero plausibles.
- Simular desgaste o degradación progresiva (modo Realistic).
- Configurar perfiles de realismo o severidad.

## No objetivos

- Reemplazar a Fenix: el sistema de fallas se inyecta en el avión real vía su API, no se simula por fuera.
- Depender de FSUIPC: se descarta como base (ver decisión).
- Simular el avión completo: solo fallas con lógica contextual.
- Interfaces multiplataforma: app de escritorio Windows.

## Decisión: SimConnect sobre FSUIPC

- SimConnect es la API oficial de Microsoft, alineada con MSFS 2020/2024.
- Evita capas de terceros innecesarias.
- Provee el contexto del simulador que necesita el motor de fallas.
- FSUIPC queda como alternativa solo para compatibilidad legacy, no para este proyecto.

## Notas

- El stack técnico detallado, la estructura de la solución y el flujo vigente están en [arquitectura.md](arquitectura.md).
- Las convenciones operativas (comandos, migrations, ramas, commits) están en [desarrollo.md](desarrollo.md); este documento no duplica instrucciones operativas.
