# Feed de fallas integrado al bloque de pensamiento del motor

Type: task
Status: resolved

## Question / Spec (usuario, 2026-10-23)

En el paso 3 del Modo Realista (`RealisticModePanel`), la vista de fallas en tiempo real debe **unirse e integrarse dentro del bloque de pensamiento del motor** (bloque del «ojito»):

- **Por defecto oculto** (estado actual del ojito).
- Al habilitarlo se ve: detalles en vivo del funcionamiento del motor (**implementación futura** — dejar lugar para ello) **y** lo que ya existe hoy: **las fallas enviadas y sus estados, tal cual hoy** (mismas entradas, colores y estados del feed actual `LiveTriggeredFailures`/§14).
- El feed independiente desaparece como bloque separado: su contenido vive dentro del bloque de pensamiento.

Notas de implementación:
- Reutilizar `LiveFailureEntryViewModel` y los colores/estados actuales; no duplicar estado.
- El bloque combinado solo aparece con sesión activa (§14: el pensamiento «solo aparece con una sesión activa»).
- Verificar convivencia con el scroll custom del paso 3 y la franja de acciones.
