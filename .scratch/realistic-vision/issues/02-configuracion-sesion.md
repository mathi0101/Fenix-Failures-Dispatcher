# Ticket 02 — Configuración y arranque de la sesión

Type: grilling
Status: resolved
Blocked by:

## Question

Requisitos de inicio de una sesión Realistic: (1) SimConnect + Fenix activos; (2) aeronave registrada elegida; (3) OFP de SimBrief descargado y válido; (4) perfil de riesgo elegido. Qué hacer si no hay ID de SimBrief configurado o la descarga falla. Qué configuración persiste la app y dónde. Qué se guarda del OFP para el engine.

## Answer

- **Aeronave (Q28)**: ya resuelto en código y base — ABM de aeronaves en la UI (`RealisticModeViewModel`: alta con `Registration` + `IcaoTypeCode`); el usuario crea la aeronave y la elige por sesión. El desgaste queda atado a esa identidad.
- **Validación del OFP (Q29)**: (1) **ICAO**: el código ICAO del tipo de aeronave en el OFP debe coincidir con el ICAO del tipo de aeronave seleccionada (A319/A320/A321); (2) **campos mínimos** según recomendación (origen, destino, duración estimada), aunque el OFP de SimBrief siempre viene completo; (3) **ventana de tiempo (lo más importante)**: tomar el **offblock estimado** del OFP de salida → el usuario puede iniciar el modo Realistic hasta **2 horas antes** de ese offblock; tomar (o calcular) la **hora de aterrizaje estimada** del OFP de llegada → el OFP es válido hasta **2 horas después**. Margen para errores, pero el OFP debe corresponder a la hora a la que se va a volar (siempre UTC). Fuera de la ventana [offblock − 2 h, aterrizaje est. + 2 h] el OFP se rechaza.
- **Sin ID de SimBrief / descarga fallida (Q30)**: bloqueante; sin modo sin-OFP. La app pide el ID si falta, ofrece reintentar si falla; no arranca hasta que el OFP esté válido.
- **Persistencia (Q31)**: ya resuelto — ID de SimBrief en `%AppData%\RealFenixFailures\userprefs.json` (JSON del usuario, solo en su PC; **no** se persiste fuera, por privacidad); el resto (aeronaves, historial, desgaste) en BD. Los topes de seguridad de perfiles (Q22/Q23) son configurables en appsettings.
- **Contenido del OFP para el engine (Q32)**: mucho más que la duración — se validan los campos que se pueden extraer de la respuesta real de SimBrief ya documentada. Utilidad: **muy útil** = Flight log (perfil esperado por waypoint: FL, MN, viento, OAT, EFOB, TAS/GS, EET, ETO, distancias — el más rico para el motor), Fuel (trip, contingencias, alterno, final, mínimo, taxi, block fuel), Pesajes (MTOW/LAW/ZFW), TLR (pista, viento, QNH, pesos, flaps, límites); **secundario** = Routing, Weather, Encabezado OFP, ATC flight plan; **no prioritario** = NOTAMs (texto libre). Nota: se confirma que NOTAMs/weather no se guardan como tales; block fuel, ETOW, LW, rutas y FL CRZ sí son útiles.
- **Exclusión de fallas**: descartada (ticket 01, Q27) — no aplica a la configuración de sesión.
