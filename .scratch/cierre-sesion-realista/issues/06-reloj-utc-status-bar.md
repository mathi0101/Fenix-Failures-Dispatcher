# Reloj UTC en la barra de estado

Type: grilling
Status: resolved
Blocked by: (ninguno)

## Question

¿Dónde, cómo y con qué formato va el reloj de hora UTC en tiempo real en la MainWindow?

## Answer

Decidido por el usuario (2026-10-23):

- **Ubicación**: en la status bar de la `MainWindow.xaml` (Grid.Row=1), justo al lado de la píldora «FASE:» (columna 3, hoy centrada; la píldora vive en las líneas ~123-132 del XAML). El reloj se agrega como elemento adyacente dentro de esa columna (misma banda, sin ocupar otra).
- **Formato**: `HH:MM:SS UTC` (texto literal «UTC» incluido).
- **Actualización**: en tiempo real (cada segundo, `DispatcherTimer` de 1 s o equivalente).

Supuestos de implementación registrados (veto posible hasta ejecutarse):
- El reloj muestra **hora UTC real de pared** (`TimeProvider.System.GetUtcNow`), **no** el tiempo de escenario del `ScaledTimeProvider` DEBUG: «Zulu» es la hora del piloto; en sesión real MSFS ambas coinciden.
- Render directo en la status bar (TextBlock + timer en code-behind de la ventana o propiedad del `MainWindowViewModel`), sin depender del timer del motor.
