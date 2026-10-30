using RealFenixFailures.Domain.Enums;

namespace RealFenixFailures.Domain.Services;

/// <summary>Configuración global del motor (sección <c>FailureEngine</c> de appsettings): debug, polling y parámetros del modo Realistic.</summary>
public class FailureEngineSettings {
    public const string SectionName = "FailureEngine";

    /// <summary>
    /// Flag de debug del motor: activa herramientas de desarrollo desde appsettings en instalaciones de
    /// testing sin recompilar. Hoy gobierna: la visibilidad del bloque «Reloj de simulación» del panel
    /// Realistic y el log de diagnóstico de SimConnect (ex flag <c>SimConnectDiagnostics:Enabled</c>).
    /// Código = false; en producción el appsettings puede omitirlo o dejarlo en false.
    /// </summary>
    public bool Debug { get; set; } = false;

    /// <summary>Límite de entradas del log de eventos en vivo de la UI (recorte por la cola).</summary>
    public int MaxLogEntries { get; set; } = 50;

    /// <summary>Intervalo, s, entre chequeos de conexión con la API de Fenix (orquestador del motor).</summary>
    public int CheckConnectionIntervalSeconds { get; set; } = 30;
    /// <summary>Intervalo, s, del refresco periódico del estado en la ventana principal.</summary>
    public int CheckIntervalSeconds { get; set; } = 10;

    public RealisticModeSettings RealisticMode { get; set; } = new();
}

/// <summary>Parámetros del modo Realistic: cadencia del motor, colas, semilla de tierra, tregua, física global, perfiles de riesgo, ventana OFP y desgaste.</summary>
public class RealisticModeSettings {
    /// <summary>Frecuencia del tick de evaluación en vuelo, s: cada cuánto el motor tira los dados por cada falla elegible.</summary>
    public int EvaluationIntervalSeconds { get; set; } = 60;
    /// <summary>Capacidad de la cola de fallas preparadas (avisos previos armados esperando disparo); llena, el motor deja de preparar más.</summary>
    public int PreparedQueueLimit { get; set; } = 5;
    /// <summary>Habilita la semilla de fallas leves al armar la sesión en tierra; deshabilitado, todo vuelo arranca limpio.</summary>
    public bool GroundFailureEnabled { get; set; } = true;
    /// <summary>Tope de líneas del bloque de razonamiento del motor por evaluación (traza de thinking).</summary>
    public int ThinkingLinesCap { get; set; } = 50;
    /// <summary>Δt máximo, s, sobre el que se calcula la cadencia: un hueco mayor (pausa, sim cerrado) cuenta como este tope para no disparar una ráfaga por tiempo acumulado.</summary>
    public double EvaluationDeltaCapSeconds { get; set; } = 300;
    /// <summary>Física global: λ base, multiplicadores de severidad y agravamiento (compartidos por tierra y vuelo).</summary>
    public RealisticPhysicsSettings Physics { get; set; } = new();
    /// <summary>Perfil Bajo (nivel 1): default del perfil; appsettings puede pisar cualquier campo parcialmente (<c>FailureEngine:RealisticMode:Low</c>).</summary>
    public RealisticProfileSettings Low { get; set; } = new() {
        RiskLevel = 1, ProbabilityMultiplier = 1, WearMaxMultiplier = 2,
        AggravationCap = 3, GroundFailuresCap = 1, CriticalRespiteSeconds = 0
    };
    /// <summary>Perfil Moderado (nivel 2): default del perfil; appsettings puede pisar cualquier campo parcialmente (<c>FailureEngine:RealisticMode:Moderate</c>).</summary>
    public RealisticProfileSettings Moderate { get; set; } = new() {
        RiskLevel = 2, ProbabilityMultiplier = 1.5, WearMaxMultiplier = 2.6,
        AggravationCap = 5, GroundFailuresCap = 2, CriticalRespiteSeconds = 180 * 60
    };
    /// <summary>Perfil Alto (nivel 3): default del perfil; appsettings puede pisar cualquier campo parcialmente (<c>FailureEngine:RealisticMode:High</c>).</summary>
    public RealisticProfileSettings High { get; set; } = new() {
        RiskLevel = 3, ProbabilityMultiplier = 3.5, WearMaxMultiplier = 3,
        AggravationCap = 8, GroundFailuresCap = 3, CriticalRespiteSeconds = 120 * 60
    };
    /// <summary>Perfil Squawk 7700 (nivel 4): default del perfil; appsettings puede pisar cualquier campo parcialmente (<c>FailureEngine:RealisticMode:Squawk7700</c>).</summary>
    public RealisticProfileSettings Squawk7700 { get; set; } = new() {
        RiskLevel = 4, ProbabilityMultiplier = 6, WearMaxMultiplier = 3,
        AggravationCap = 10, GroundFailuresCap = 5, CriticalRespiteSeconds = 60 * 60
    };

    /// <summary>Devuelve el perfil vigente para el nivel de riesgo pedido (default de código o el pisado por configuración).</summary>
    public RealisticProfileSettings ProfileFor(RiskLevel risk) => risk switch {
        RiskLevel.Low => Low,
        RiskLevel.Moderate => Moderate,
        RiskLevel.High => High,
        RiskLevel.Squawk7700 => Squawk7700,
        _ => Moderate
    };
    /// <summary>Ventanas de vigencia del OFP de SimBrief.</summary>
    public OfpWindowSettings Ofp { get; set; } = new();
    /// <summary>Parámetros del desgaste de sistemas por sesión.</summary>
    public WearSettings Wear { get; set; } = new();

    /// <summary>Probabilidad (0–1) de que un sorteo aleatorio en vuelo salga temporal (se desactiva sola tras su vida). Solo leves y graves: las críticas nunca son temporales.</summary>
    public double TemporaryFailureProbability { get; set; } = 0.25;
    /// <summary>Vida mínima de una falla temporal, en minutos de tiempo de escenario, desde el armado.</summary>
    public int TemporaryLifetimeMinMinutes { get; set; } = 10;
    /// <summary>Vida máxima de una falla temporal, en minutos de tiempo de escenario (>= mínima; se invierte si viniera al revés).</summary>
    public int TemporaryLifetimeMaxMinutes { get; set; } = 30;
}

/// <summary>
/// Física global del motor (spec modelo-probabilístico §1.1): parámetros de la fórmula unificada y del
/// score de agravamiento, compartidos por tierra y vuelo. La política por perfil vive en
/// <see cref="RealisticProfileSettings"/>.
/// </summary>
public class RealisticPhysicsSettings {
    /// <summary>λ base por falla Minor elegible en vuelo, en eventos/hora. Subirlo escala linealmente toda la cadencia en vuelo.</summary>
    public double FailureRatePerHourBase { get; set; } = 0.05;
    /// <summary>Probabilidad base (0–1) de cada falla Minor individual en el seed de tierra, antes de multiplicadores.</summary>
    public double GroundSeedBaseProbability { get; set; } = 0.006;
    /// <summary>Cuánto más rara es cada severidad a igualdad de todo lo demás (multiplicador de λ en vuelo y de p en tierra).</summary>
    public PhysicsSeverityRateMultipliers SeverityRateMultiplier { get; set; } = new();
    /// <summary>Pesos con que cada falla activa suma al score de agravamiento de la sesión (contra el tope del perfil).</summary>
    public PhysicsAggravationWeights AggravationWeights { get; set; } = new();
    /// <summary>Puntos extra de agravamiento por cada falla previa simultánea del mismo sistema (ATA).</summary>
    public int SameAtaAggravationExtra { get; set; } = 1;
}

/// <summary>Cuánto más rara es cada severidad a igualdad de todo lo demás (multiplicador de λ / p).</summary>
public class PhysicsSeverityRateMultipliers {
    /// <summary>Multiplicador de las fallas leves (referencia de frecuencia relativa).</summary>
    public double Minor { get; set; } = 1;
    /// <summary>Multiplicador de las fallas graves: 0,4 = el 40 % de frecuentes que las leves a igualdad de lo demás.</summary>
    public double Major { get; set; } = 0.4;
    /// <summary>Multiplicador de las fallas críticas: 0,12 = ~1 crítica cada 8 leves a igualdad de lo demás.</summary>
    public double Critical { get; set; } = 0.14;

    public double For(FailureSeverity severity) => severity switch {
        FailureSeverity.Major => Major,
        FailureSeverity.Critical => Critical,
        _ => Minor
    };
}

/// <summary>Pesos con que cada severidad suma al score de agravamiento de la sesión.</summary>
public class PhysicsAggravationWeights {
    /// <summary>Puntos que suma cada falla leve activa al score de agravamiento.</summary>
    public int Minor { get; set; } = 1;
    /// <summary>Puntos que suma cada falla grave activa al score de agravamiento.</summary>
    public int Major { get; set; } = 2;
    /// <summary>Puntos que suma cada falla crítica activa al score de agravamiento.</summary>
    public int Critical { get; set; } = 4;

    public int For(FailureSeverity severity) => severity switch {
        FailureSeverity.Major => Major,
        FailureSeverity.Critical => Critical,
        _ => Minor
    };
}

/// <summary>Ventanas de vigencia del OFP de SimBrief: dentro de ellas el plan cuenta como vuelo armable.</summary>
public class OfpWindowSettings {
    /// <summary>Horas antes del offblock desde las que el OFP ya es válido (validFrom = offblock − valor).</summary>
    public double PreStartWindowHours { get; set; } = 2;
    /// <summary>Horas después del aterrizaje estimado en que el OFP deja de ser válido (validUntil = arribo + valor).</summary>
    public double PostArrivalWindowHours { get; set; } = 0.5;
}

/// <summary>Desgaste de sistemas acumulado por sesión: umbral del aviso de sistema castigado y tope de sistemas que acumulan.</summary>
public class WearSettings {
    /// <summary>Nivel de desgaste, %, desde el que el sistema se marca como castigado (requiere mantenimiento): fondo ámbar y «⚠ REQUIERE MANTENIMIENTO» en el modal de resultados, más entrada en el log al cerrar sesión.</summary>
    public int ThresholdNoticePct { get; set; } = 80;
    /// <summary>
    /// Tope opcional de sistemas que acumulan desgaste al cerrar la sesión. La cantidad base escala con las
    /// horas de vuelo (1,5 sistemas/h, mínimo 3, máximo todos los elegibles); un valor > 0 recota ese máximo.
    /// 0 o negativo = sin tope (default).
    /// </summary>
    public int SessionSystemsCap { get; set; } = 0;
}
