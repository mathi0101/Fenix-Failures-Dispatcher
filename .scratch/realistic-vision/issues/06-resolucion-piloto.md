# Ticket 06 — Resolución de fallas por el piloto

Type: grilling
Status: resolved
Blocked by: 04 (resuelto)

## Question

Cómo detecta el engine que el piloto resolvió o desactivó una falla desde el avión (la falla deja de estar activa según la API de Fenix sin que el engine haya enviado la orden). Registro de la acción por falla en el historial (solucionada / ignorada — si era temporal y el engine la desactivó, o el piloto la arregló). Qué implica para el cooldown y para la memoria de sesión. Caso especial: el usuario la desactiva desde el avión sin pausar el motor.

## Answer

- **Detección (Q56)**: por **observación** — el engine conoce las fallas activas esperadas; si Fenix reporta una inactiva que no fue desactivada por el engine ni es temporal vencida, la registra como `Solucionada por piloto` en el instante detectado. Es la señal natural del avión.
- **Registro (Q57)**: fecha/hora de detección + fase en que ocurrió + duración total de la falla (activa-desde → resuelta). Alimenta el historial minuto a minuto y la "acción del usuario". (Se descarta incorporar actitud del avión salvo pedido futuro del usuario.)
- **Durante la pausa (Q58)**: **no puede ocurrir** — si la pausa está activa, el engine **desactiva todas las fallas de forma forzosa** y el piloto las ve desaparecer del ECAM; no hay fallas activas que el piloto pueda resolver en ese estado. La resolución solo se observa con el motor fuera de pausa.
- **Re-armado tras resolución (Q59)**: **sí puede re-armarse más tarde** — es otra instancia; la memoria de la sesión respeta cooldown/plausibilidad, pero si narrativamente tiene sentido, el engine la vuelve a armar. El usuario **valora** esta idea (algo que falló y se reparó en vuelo suele ser eléctrico/menor y puede volver a fallar).
- **"Ignorada" (Q60)**: en el historial, `No resuelta (sesión terminó)` cubre la ignorada. La *acción* del piloto como tal no se infiere ni registra automáticamente (el engine no lee intenciones); solo registra quién la cerró (engine / piloto) o si quedó abierta.
