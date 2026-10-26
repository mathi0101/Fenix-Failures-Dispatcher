using Microsoft.Extensions.Options;
using OfficeOpenXml;
using OfficeOpenXml.Drawing.Chart;
using OfficeOpenXml.Style;
using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Application.Services;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Services;
using System.Drawing;
using System.Globalization;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace RealFenixFailures.TestUnits.Tools;

/// <summary>
/// Herramienta de balance (correrla regenera el reporte; no verifica calibración): genera
/// docs/balance/balance-realistic.xlsx con hojas Resumen (KPIs + estadísticas), Gráficas (tendencias
/// por desgaste y comparativa), Configuración (parámetros del código con los que se generó el archivo)
/// y una hoja cruda por perfil (1/10/100 h × desgaste 0–100 %, semilla de tierra), más su versión .md
/// diffable. Estimación global por severidad con el código real del motor (física, perfiles, conteos
/// del catálogo curado); sin presets ni fases de vuelo.
/// Ejecución: docs/balance/balance.cmd (define RFF_BALANCE=1 y corre el filtro BalanceReport); sin esa variable el
/// test no hace nada, así que ninguna corrida automatizada (CI o suite completa) regenera el reporte.
/// El detalle aparece en consola con --logger "console;verbosity=detailed".
/// </summary>
public class RealisticBalanceReportTests(ITestOutputHelper output) {
    private static readonly double[] WearLevels = [0, 25, 50, 75, 100];
    private static readonly int[] HourScales = [1, 10, 100];
    private static readonly RiskLevel[] Risks = [RiskLevel.Low, RiskLevel.Moderate, RiskLevel.High, RiskLevel.Squawk7700];

    private const int ChartWidth = 620;
    private const int ChartHeight = 320;
    private const int ChartGap = 60;

    private static string ProfileName(RiskLevel risk) => risk switch {
        RiskLevel.Low => "Bajo",
        RiskLevel.Moderate => "Moderado",
        RiskLevel.High => "Alto",
        _ => "7700",
    };

    private static Color Accent(RiskLevel risk) => risk switch {
        RiskLevel.Low => Color.FromArgb(0x4C, 0xAF, 0x50),
        RiskLevel.Moderate => Color.FromArgb(0xF5, 0x9E, 0x0B),
        RiskLevel.High => Color.FromArgb(0xEF, 0x44, 0x44),
        _ => Color.FromArgb(0x8B, 0x5C, 0xF6),
    };

    [Fact]
    public void BalanceReport() {
        // Herramienta manual: solo genera con RFF_BALANCE=1 (script docs/balance/balance.cmd); ninguna corrida automatizada (CI o suite completa) regenera el reporte.
        if (Environment.GetEnvironmentVariable("RFF_BALANCE") != "1") {
            output.WriteLine("Saltado: herramienta manual. Regenerar el reporte con docs/balance/balance.cmd (define RFF_BALANCE=1).");
            return;
        }
        // Estimación global por severidad: nada de presets ni fases — la fórmula usa los conteos del
        // catálogo curado (para la semilla de tierra) y los parámetros de FailureEngineSettings.
        var settings = new FailureEngineSettings();
        var counts = FailureCatalogStats.SeverityCounts();
        var service = new RealisticExpectationService(Options.Create(settings));

        var wearByProfile = new Dictionary<RiskLevel, List<ProfileExpectationRow>>();
        foreach (var wear in WearLevels) {
            var expectations = service.ComputePerProfileAsync(wear).GetAwaiter().GetResult();
            foreach (var dto in expectations) {
                var list = wearByProfile.TryGetValue(dto.Risk, out var value) ? value : (wearByProfile[dto.Risk] = []);
                list.Add(new ProfileExpectationRow(wear, dto));
            }
        }

        var (xlsxPath, mdPath) = ResolveReportPaths();
        Directory.CreateDirectory(Path.GetDirectoryName(xlsxPath)!);
        using (var package = new ExcelPackage())
        {
            BuildWorkbook(package, wearByProfile, counts, settings);
            package.SaveAs(new FileInfo(xlsxPath));
        }
        File.WriteAllText(mdPath, BuildMarkdown(wearByProfile, counts, settings)
            .ReplaceLineEndings(Environment.NewLine), new UTF8Encoding(false));

        output.WriteLine(File.ReadAllText(mdPath));
        Assert.True(File.Exists(xlsxPath));
        Assert.True(File.Exists(mdPath));
    }

    // ------------------------------------------------------------------ workbook

    private static void BuildWorkbook(ExcelPackage package, Dictionary<RiskLevel, List<ProfileExpectationRow>> wearByProfile, (int Minor, int Major, int Critical) counts, FailureEngineSettings settings) {
        var summary = package.Workbook.Worksheets.Add("Resumen");
        foreach (var risk in Risks)
            BuildProfileSheet(package, risk, wearByProfile[risk]);
        BuildSummaryContent(summary, wearByProfile, counts);
        BuildChartsSheet(package);
        BuildConfigSheet(package, settings, wearByProfile);
    }

    private static void BuildSummaryContent(ExcelWorksheet sheet, Dictionary<RiskLevel, List<ProfileExpectationRow>> wearByProfile, (int Minor, int Major, int Critical) counts) {
        WriteTitle(sheet, "Balance del modo Realistic — generado automáticamente con el motor vigente", 1, 8);
        WriteSubtitle(sheet, $"Fallas en vuelo por perfil y severidad; semilla de tierra (arranque de sesión) aparte. "
            + $"catálogo curado: {counts.Minor} leves / {counts.Major} graves / {counts.Critical} críticas. Estimación global por severidad (sin presets ni fases). Regenerar: docs/balance/balance.cmd", 2, 8);

        // KPIs al desgaste de referencia (25 %), la vista que refleja la pantalla de perfiles.
        sheet.Cells[4, 1].Value = "Referencia con desgaste medio (25 %) — cada 10 horas de vuelo";
        sheet.Cells[4, 1].Style.Font.Bold = true;
        WriteHeaderRow(sheet, 5, "Perfil", "Leves", "Graves", "Críticas", "Total");
        var row = 6;
        foreach (var risk in Risks) {
            var e = wearByProfile[risk].First(r => r.WearPct == 25).Expectation;
            var nameCell = sheet.Cells[row, 1];
            nameCell.Value = ProfileName(risk);
            nameCell.Style.Font.Bold = true;
            nameCell.Style.Font.Color.SetColor(Accent(risk));
            WriteRate(sheet.Cells[row, 2], e.MinorPerHour * 10);
            WriteRate(sheet.Cells[row, 3], e.MajorPerHour * 10);
            WriteRate(sheet.Cells[row, 4], e.CriticalPerHour * 10);
            WriteRate(sheet.Cells[row, 5], e.TotalPerHour * 10);
            row++;
        }
        FormatTable(sheet, 5, row - 1, columns: 5);

        // Estadísticas: promedio/min/max entre desgastes y una crítica cada X horas (al 25 %).
        row += 2;
        sheet.Cells[row, 1].Value = "Estadísticas (cada 10 h, promedio entre desgastes 0–100 %)";
        sheet.Cells[row, 1].Style.Font.Bold = true;
        row++;
        var statsHeader = row;
        WriteHeaderRow(sheet, row, "Perfil", "Graves prom.", "Graves mín.", "Graves máx.", "Críticas prom.", "1 crítica cada… (25 %)");
        row++;
        foreach (var risk in Risks) {
            var rows = wearByProfile[risk];
            var majors = rows.Select(r => r.Expectation.MajorPerHour * 10).ToList();
            var criticals = rows.Select(r => r.Expectation.CriticalPerHour * 10).ToList();
            var referenceCritical = rows.First(r => r.WearPct == 25).Expectation.CriticalPerHour * 10;

            var nameCell = sheet.Cells[row, 1];
            nameCell.Value = ProfileName(risk);
            nameCell.Style.Font.Bold = true;
            nameCell.Style.Font.Color.SetColor(Accent(risk));
            WriteRate(sheet.Cells[row, 2], majors.Average());
            WriteRate(sheet.Cells[row, 3], majors.Min());
            WriteRate(sheet.Cells[row, 4], majors.Max());
            WriteRate(sheet.Cells[row, 5], criticals.Average());
            var hoursPerCritical = sheet.Cells[row, 6];
            hoursPerCritical.Value = referenceCritical > 0 ? Math.Round(10 / referenceCritical, 1) : 0;
            hoursPerCritical.Style.Numberformat.Format = "0.0 \"h\"";
            if (referenceCritical == 0) hoursPerCritical.Style.Font.Color.SetColor(Color.FromArgb(0x6B, 0x74, 0x88));
            row++;
        }
        FormatTable(sheet, statsHeader, row - 1, columns: 6);

        // Experiencia por vuelo de referencia (1,5 h al 25 %): lo que siente el jugador, no tasas por hora.
        row += 2;
        sheet.Cells[row, 1].Value = "Experiencia por vuelo (1,5 h, desgaste 25 %)";
        sheet.Cells[row, 1].Style.Font.Bold = true;
        row++;
        var expHeader = row;
        WriteHeaderRow(sheet, row, "Perfil", "E[fallas]", "P(≥1 falla)", "P(≥1 grave)", "P(≥1 crítica)", "1 crítica cada");
        row++;
        foreach (var risk in Risks) {
            var e = wearByProfile[risk].First(r => r.WearPct == 25).Expectation;
            var total = e.TotalPerHour * 1.5;
            var nameCell = sheet.Cells[row, 1];
            nameCell.Value = ProfileName(risk);
            nameCell.Style.Font.Bold = true;
            nameCell.Style.Font.Color.SetColor(Accent(risk));
            WriteRate(sheet.Cells[row, 2], total);
            var anyCell = sheet.Cells[row, 3];
            anyCell.Value = (1 - Math.Exp(-total)) * 100;
            anyCell.Style.Numberformat.Format = "0.0 \" %\"";
            var majorCell = sheet.Cells[row, 4];
            majorCell.Value = (1 - Math.Exp(-e.MajorPerHour * 1.5)) * 100;
            majorCell.Style.Numberformat.Format = "0.0 \" %\"";
            if (risk == RiskLevel.Low) {
                sheet.Cells[row, 5].Value = "—";
                sheet.Cells[row, 6].Value = "—";
            } else {
                var criticalCell = sheet.Cells[row, 5];
                criticalCell.Value = (1 - Math.Exp(-e.CriticalPerHour * 1.5)) * 100;
                criticalCell.Style.Numberformat.Format = "0.0 \" %\"";
                sheet.Cells[row, 6].Value = Math.Round(1 / (e.CriticalPerHour * 1.5));
                sheet.Cells[row, 6].Style.Numberformat.Format = "0 \"vuelos\"";
            }
            row++;
        }
        FormatTable(sheet, expHeader, row - 1, columns: 6);

        // Ancla temporal: cuántos vuelos de 1,5 h tarda un sistema en acumular cada nivel de desgaste.
        row += 2;
        sheet.Cells[row, 1].Value = "Ancla temporal del desgaste (vuelos de 1,5 h hasta cada nivel; el seed va de 0,1 a 0,6 %/h)";
        sheet.Cells[row, 1].Style.Font.Bold = true;
        row++;
        var anchorHeader = row;
        WriteHeaderRow(sheet, row, "Tasa del sistema", "Hasta 25 %", "Hasta 50 %", "Hasta 80 % (aviso)");
        row++;
        foreach (var rate in new[] { 0.1, 0.3, 0.6 }) {
            sheet.Cells[row, 1].Value = $"{rate:0.##} %/h";
            for (var i = 0; i < 3; i++) {
                var cell = sheet.Cells[row, 2 + i];
                cell.Value = Math.Ceiling(new[] { 25.0, 50.0, 80.0 }[i] / (rate * 1.5));
                cell.Style.Numberformat.Format = "0";
            }
            row++;
        }
        FormatTable(sheet, anchorHeader, row - 1, columns: 4);

        sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
    }

    /// <summary>Hoja cruda de un perfil: tabla por escala (1/10/100 h) con el desgaste como filas, más la semilla de tierra.</summary>
    private static void BuildProfileSheet(ExcelPackage package, RiskLevel risk, List<ProfileExpectationRow> rows) {
        var sheet = package.Workbook.Worksheets.Add(ProfileName(risk));
        var profile = rows[0].Expectation.Profile;
        WriteTitle(sheet, $"Perfil {ProfileName(risk)} (nivel {profile.RiskLevel})", 1, 5);
        WriteSubtitle(sheet, $"Multiplicador ×{profile.ProbabilityMultiplier:0.##} · desgaste máx ×{profile.WearMaxMultiplier:0.##} · "
            + $"tope agravación {profile.AggravationCap} · tope tierra {profile.GroundFailuresCap} · tregua crítica "
            + (profile.CriticalRespiteSeconds == 0 ? "—" : $"{profile.CriticalRespiteSeconds / 60:0} min"), 2, 5);

        var accent = Accent(risk);
        var currentRow = 4;
        var tenHoursHeader = 0;
        foreach (var hours in HourScales) {
            sheet.Cells[currentRow, 1].Value = hours == 1 ? "Cada 1 h de vuelo" : $"Cada {hours} h de vuelo";
            sheet.Cells[currentRow, 1].Style.Font.Bold = true;
            sheet.Cells[currentRow, 1].Style.Font.Color.SetColor(accent);
            currentRow++;
            WriteHeaderRow(sheet, currentRow, "Desgaste", "Leves", "Graves", "Críticas", "Total");
            currentRow++;
            var tableStart = currentRow;
            foreach (var row in rows) {
                var e = row.Expectation;
                WriteWear(sheet.Cells[currentRow, 1], row.WearPct);
                WriteRate(sheet.Cells[currentRow, 2], e.MinorPerHour * hours);
                WriteRate(sheet.Cells[currentRow, 3], e.MajorPerHour * hours);
                WriteRate(sheet.Cells[currentRow, 4], e.CriticalPerHour * hours);
                WriteRate(sheet.Cells[currentRow, 5], e.TotalPerHour * hours);
                if (row.WearPct == 25)
                    HighlightRow(sheet, currentRow, 5);
                currentRow++;
            }
            FormatTable(sheet, tableStart, currentRow - 1, columns: 5);
            if (hours == 10) tenHoursHeader = tableStart - 1;
            currentRow++;
        }

        sheet.Cells[currentRow, 1].Value = "Semilla de tierra (fallas esperadas al iniciar cada vuelo)";
        sheet.Cells[currentRow, 1].Style.Font.Bold = true;
        currentRow++;
        WriteHeaderRow(sheet, currentRow, "Desgaste", "Fallas al inicio");
        currentRow++;
        var seedStart = currentRow;
        foreach (var row in rows) {
            WriteWear(sheet.Cells[currentRow, 1], row.WearPct);
            sheet.Cells[currentRow, 2].Value = row.Expectation.GroundSeedsPerFlight;
            sheet.Cells[currentRow, 2].Style.Numberformat.Format = "0.00";
            currentRow++;
        }
        FormatTable(sheet, seedStart, currentRow - 1, columns: 2);

        sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
        // Rango de la tabla de 10 h, lo usan las gráficas (A = desgaste, C = graves, D = críticas).
        TenHoursRange[ProfileName(risk)] = (
            Wear: sheet.Cells[tenHoursHeader + 1, 1, tenHoursHeader + rows.Count, 1],
            Graves: sheet.Cells[tenHoursHeader + 1, 3, tenHoursHeader + rows.Count, 3],
            Criticals: sheet.Cells[tenHoursHeader + 1, 4, tenHoursHeader + rows.Count, 4]);
    }

    /// <summary>Hoja Gráficas: tendencias por desgaste (serie por perfil con su color) y comparativa entre perfiles.</summary>
    private static void BuildChartsSheet(ExcelPackage package) {
        var sheet = package.Workbook.Worksheets.Add("Gráficas");
        WriteTitle(sheet, "Tendencias del balance", 1, 10);

        // Línea 1: graves cada 10 h según desgaste, una serie por perfil con el color del perfil.
        var majors = (ExcelLineChart)sheet.Drawings.AddChart("GravesPorDesgaste", eChartType.LineMarkers);
        majors.Title.Text = "Fallas graves (Major) cada 10 h de vuelo, según desgaste";
        majors.Legend.Position = eLegendPosition.Bottom;
        majors.SetPosition(2, 0, 0, 0);
        majors.SetSize(ChartWidth, ChartHeight);
        foreach (var risk in Risks) {
            var (wear, graves, _) = TenHoursRange[ProfileName(risk)];
            var serie = majors.Series.Add(graves, wear);
            serie.Header = ProfileName(risk);
            serie.Border.LineStyle = OfficeOpenXml.Drawing.eLineStyle.Solid;
            serie.Border.Fill.Color = Accent(risk);
        }

        // Línea 2: críticas cada 100 h según desgaste (las graves dominan la escala, las críticas van aparte).
        var criticals = (ExcelLineChart)sheet.Drawings.AddChart("CriticasPorDesgaste", eChartType.LineMarkers);
        criticals.Title.Text = "Fallas críticas (Critical) cada 100 h de vuelo, según desgaste";
        criticals.Legend.Position = eLegendPosition.Bottom;
        criticals.SetPosition(2, ChartHeight + ChartGap, 0, 0);
        criticals.SetSize(ChartWidth, ChartHeight);
        foreach (var risk in Risks) {
            var (wear, _, criticalRange) = TenHoursRange[ProfileName(risk)];
            var serie = criticals.Series.Add(criticalRange, wear);
            serie.Header = ProfileName(risk);
            serie.Border.LineStyle = OfficeOpenXml.Drawing.eLineStyle.Solid;
            serie.Border.Fill.Color = Accent(risk);
        }

        // Barras: comparativa de graves a desgaste medio (25 %), la referencia de la pantalla.
        var summary = package.Workbook.Worksheets["Resumen"];
        var bars = (ExcelBarChart)sheet.Drawings.AddChart("GravesPorPerfil", eChartType.ColumnClustered);
        bars.Title.Text = "Graves cada 10 h por perfil (desgaste 25 %)";
        bars.Legend.Position = eLegendPosition.Bottom;
        bars.SetPosition(2, 2 * (ChartHeight + ChartGap), 0, 0);
        bars.SetSize(ChartWidth, ChartHeight);
        var barSerie = bars.Series.Add(summary.Cells[6, 3, 9, 3], summary.Cells[6, 1, 9, 1]);
        barSerie.Header = "Graves / 10 h";
        barSerie.Fill.Color = Color.FromArgb(0x3B, 0x82, 0xF6);
    }

    /// <summary>Hoja Configuración: parámetros vigentes del código con los que se generó el reporte.</summary>
    private static void BuildConfigSheet(ExcelPackage package, FailureEngineSettings settings, Dictionary<RiskLevel, List<ProfileExpectationRow>> wearByProfile) {
        var sheet = package.Workbook.Worksheets.Add("Configuración");
        WriteTitle(sheet, "Configuración del código con la que se generó este reporte", 1, 6);
        WriteSubtitle(sheet, "Valores leídos del código al generar el archivo (FailureEngineSettings y perfiles del catálogo): "
            + "si cambiaron parámetros, regenerar para reflejarlos aquí.", 2, 6);

        var currentRow = 4;
        void Section(string name) {
            sheet.Cells[currentRow, 1].Value = name;
            sheet.Cells[currentRow, 1].Style.Font.Bold = true;
            currentRow++;
            WriteHeaderRow(sheet, currentRow, "Parámetro", "Valor");
            currentRow++;
        }
        void Param(string name, string value) {
            sheet.Cells[currentRow, 1].Value = name;
            sheet.Cells[currentRow, 2].Value = value;
            currentRow++;
        }
        void ParamNum(string name, double value, string format = "0.00") {
            sheet.Cells[currentRow, 1].Value = name;
            var cell = sheet.Cells[currentRow, 2];
            cell.Value = value;
            cell.Style.Numberformat.Format = format;
            currentRow++;
        }

        var physics = settings.RealisticMode.Physics;
        Section("Física del motor (Physics)");
        ParamNum("Tasa base por falla leve elegible en vuelo, eventos/h (FailureRatePerHourBase)", physics.FailureRatePerHourBase);
        ParamNum("Probabilidad base por falla leve en la semilla de tierra (GroundSeedBaseProbability)", physics.GroundSeedBaseProbability, "0.000");
        ParamNum("Multiplicador de tasa: leves (SeverityRateMultiplier.Minor)", physics.SeverityRateMultiplier.Minor);
        ParamNum("Multiplicador de tasa: graves (SeverityRateMultiplier.Major)", physics.SeverityRateMultiplier.Major);
        ParamNum("Multiplicador de tasa: críticas (SeverityRateMultiplier.Critical)", physics.SeverityRateMultiplier.Critical);
        ParamNum("Peso de agravamiento: leves (AggravationWeights.Minor)", physics.AggravationWeights.Minor, "0");
        ParamNum("Peso de agravamiento: graves (AggravationWeights.Major)", physics.AggravationWeights.Major, "0");
        ParamNum("Peso de agravamiento: críticas (AggravationWeights.Critical)", physics.AggravationWeights.Critical, "0");
        ParamNum("Agravación extra por misma ATA simultánea (SameAtaAggravationExtra)", physics.SameAtaAggravationExtra, "0");
        FormatTable(sheet, currentRow - 9, currentRow - 1, columns: 2);

        var mode = settings.RealisticMode;
        Section("Modo Realistic (RealisticMode)");
        ParamNum("Intervalo de evaluación del motor, s (EvaluationIntervalSeconds)", mode.EvaluationIntervalSeconds, "0");
        ParamNum("Límite de la cola de preparadas (PreparedQueueLimit)", mode.PreparedQueueLimit, "0");
        Param("Semilla de tierra habilitada (GroundFailureEnabled)", mode.GroundFailureEnabled ? "Sí" : "No");
        ParamNum("Tope de líneas de thinking (ThinkingLinesCap)", mode.ThinkingLinesCap, "0");
        ParamNum("Tope de delta de evaluación, s (EvaluationDeltaCapSeconds)", mode.EvaluationDeltaCapSeconds, "0");
        FormatTable(sheet, currentRow - 5, currentRow - 1, columns: 2);

        Section("Desgaste (Wear) y ventanas OFP");
        ParamNum("Umbral de aviso de sistema castigado, % (ThresholdNoticePct)", mode.Wear.ThresholdNoticePct, "0");
        ParamNum("Tope de sistemas con desgaste por sesión, 0 = sin tope (SessionSystemsCap)", mode.Wear.SessionSystemsCap, "0");
        ParamNum("Ventana previa al start, h (PreStartWindowHours)", mode.Ofp.PreStartWindowHours);
        ParamNum("Ventana posterior al arribo, h (PostArrivalWindowHours)", mode.Ofp.PostArrivalWindowHours);
        FormatTable(sheet, currentRow - 4, currentRow - 1, columns: 2);

        Section("Perfiles vigentes (defaults de código)");
        WriteHeaderRow(sheet, currentRow, "Perfil", "Multiplicador", "Desgaste máx", "Tope agravación", "Tope tierra", "Tregua crítica");
        var profileHeader = currentRow;
        currentRow++;
        foreach (var risk in Risks) {
            var profile = wearByProfile[risk][0].Expectation.Profile;
            var nameCell = sheet.Cells[currentRow, 1];
            nameCell.Value = ProfileName(risk);
            nameCell.Style.Font.Bold = true;
            nameCell.Style.Font.Color.SetColor(Accent(risk));
            sheet.Cells[currentRow, 2].Value = profile.ProbabilityMultiplier;
            sheet.Cells[currentRow, 2].Style.Numberformat.Format = "0.00";
            sheet.Cells[currentRow, 3].Value = profile.WearMaxMultiplier;
            sheet.Cells[currentRow, 3].Style.Numberformat.Format = "0.00";
            sheet.Cells[currentRow, 4].Value = profile.AggravationCap;
            sheet.Cells[currentRow, 4].Style.Numberformat.Format = "0";
            sheet.Cells[currentRow, 5].Value = profile.GroundFailuresCap;
            sheet.Cells[currentRow, 5].Style.Numberformat.Format = "0";
            sheet.Cells[currentRow, 6].Value = profile.CriticalRespiteSeconds == 0 ? "—" : $"{profile.CriticalRespiteSeconds / 60:0} min";
            currentRow++;
        }
        FormatTable(sheet, profileHeader, currentRow - 1, columns: 6);

        sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
    }

    // ------------------------------------------------------------------ markdown (diffable en git)

    private static string BuildMarkdown(Dictionary<RiskLevel, List<ProfileExpectationRow>> wearByProfile, (int Minor, int Major, int Critical) counts, FailureEngineSettings settings) {
        var report = new StringBuilder();
        report.AppendLine("# Balance del modo Realistic — generado automáticamente");
        report.AppendLine();
        report.AppendLine($"_Generado por `RealisticBalanceReportTests` con el código vigente del motor "
            + $"(FailureEngineSettings con sus perfiles; catálogo curado: {counts.Minor} leves / {counts.Major} graves / {counts.Critical} críticas). "
            + "Regenerar: `docs/balance/balance.cmd` (herramienta manual, exige `RFF_BALANCE=1`). Versión formateada: `balance-realistic.xlsx`._");
        report.AppendLine();
        report.AppendLine("## Supuestos de la estimación");
        report.AppendLine();
        report.AppendLine("- Tasa en vuelo por severidad = `FailureRatePerHourBase` × multiplicador de severidad × multiplicador del perfil × factor de desgaste. Estimación global por severidad: no pondera por fase ni por la composición de presets.");
        report.AppendLine("- Terminología: **leves** = Minor, **graves** = Major, **críticas** = Critical.");
        report.AppendLine("- La semilla de tierra es un evento por vuelo (arranque de sesión); **no escala con las horas** y el motor solo arma fallas leves ahí.");
        report.AppendLine("- Perfil Bajo: el motor descarta fallas críticas, por lo que su cifra de críticas es siempre 0.");
        report.AppendLine("- La cadencia real la decide el motor tick a tick; esto es una estimación de presentación.");
        report.AppendLine();

        var physics = settings.RealisticMode.Physics;
        var mode = settings.RealisticMode;
        report.AppendLine("## Configuración del código con la que se generó este reporte");
        report.AppendLine();
        report.AppendLine("_Valores leídos del código al generar el archivo (`FailureEngineSettings` y perfiles del catálogo): "
            + "si cambiaron parámetros, regenerar para reflejarlos._");
        report.AppendLine();
        report.AppendLine("| Parámetro | Valor |");
        report.AppendLine("|---|---:|");
        report.AppendLine($"| Tasa base por falla leve elegible en vuelo, eventos/h (`FailureRatePerHourBase`) | {Fmt(physics.FailureRatePerHourBase)} |");
        report.AppendLine($"| Probabilidad base por falla leve en la semilla de tierra (`GroundSeedBaseProbability`) | {physics.GroundSeedBaseProbability:0.000} |");
        report.AppendLine($"| Multiplicador de tasa leves / graves / críticas (`SeverityRateMultiplier`) | {Fmt(physics.SeverityRateMultiplier.Minor)} / {Fmt(physics.SeverityRateMultiplier.Major)} / {Fmt(physics.SeverityRateMultiplier.Critical)} |");
        report.AppendLine($"| Peso de agravamiento leves / graves / críticas (`AggravationWeights`) | {physics.AggravationWeights.Minor} / {physics.AggravationWeights.Major} / {physics.AggravationWeights.Critical} |");
        report.AppendLine($"| Agravación extra por misma ATA simultánea (`SameAtaAggravationExtra`) | {physics.SameAtaAggravationExtra} |");
        report.AppendLine($"| Intervalo de evaluación del motor, s (`EvaluationIntervalSeconds`) | {mode.EvaluationIntervalSeconds} |");
        report.AppendLine($"| Semilla de tierra habilitada (`GroundFailureEnabled`) | {(mode.GroundFailureEnabled ? "sí" : "no")} |");
        report.AppendLine($"| Umbral de aviso de sistema castigado, % (`Wear:ThresholdNoticePct`) | {mode.Wear.ThresholdNoticePct} |");
        report.AppendLine();
        report.AppendLine("| Perfil | Multiplicador | Desgaste máx | Tope agravación | Tope tierra | Tregua crítica |");
        report.AppendLine("|---|---:|---:|---:|---:|---:|");
        foreach (var risk in Risks) {
            var profile = wearByProfile[risk][0].Expectation.Profile;
            report.AppendLine($"| **{ProfileName(risk)}** | ×{profile.ProbabilityMultiplier:0.##} | ×{profile.WearMaxMultiplier:0.##} | {profile.AggravationCap} | {profile.GroundFailuresCap} | "
                + (profile.CriticalRespiteSeconds == 0 ? "—" : $"{profile.CriticalRespiteSeconds / 60:0} min") + " |");
        }
        report.AppendLine();

        report.AppendLine("## Experiencia por vuelo (1,5 h, desgaste 25 %)");
        report.AppendLine();
        report.AppendLine("_Lo que siente el jugador por vuelo típico (la vista de 10 h de las tablas de abajo es para comparar perfiles)._");
        report.AppendLine();
        report.AppendLine("| Perfil | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |");
        report.AppendLine("|---|---:|---:|---:|---:|---:|");
        foreach (var risk in Risks) {
            var e = wearByProfile[risk].First(r => r.WearPct == 25).Expectation;
            var total = e.TotalPerHour * 1.5;
            report.AppendLine($"| **{ProfileName(risk)}** | {Fmt(total)} | {(1 - Math.Exp(-total)) * 100:0.0} % | {(1 - Math.Exp(-e.MajorPerHour * 1.5)) * 100:0.0} % | "
                + (risk == RiskLevel.Low ? "—" : $"{(1 - Math.Exp(-e.CriticalPerHour * 1.5)) * 100:0.0} %") + " | "
                + (risk == RiskLevel.Low ? "—" : $"**{Math.Round(1 / (e.CriticalPerHour * 1.5)):0} vuelos**") + " |");
        }
        report.AppendLine();
        report.AppendLine("### Ancla temporal del desgaste");
        report.AppendLine();
        report.AppendLine("Vuelos de 1,5 h que tarda un sistema en acumular cada nivel de desgaste, según su tasa (`WearPerFlightHour`; el seed va de 0,1 a 0,6 %/h):");
        report.AppendLine();
        report.AppendLine("| Tasa del sistema | Hasta 25 % | Hasta 50 % | Hasta 80 % (aviso) |");
        report.AppendLine("|---|---:|---:|---:|");
        foreach (var rate in new[] { 0.1, 0.3, 0.6 })
            report.AppendLine($"| {rate:0.##} %/h | {Math.Ceiling(25 / (rate * 1.5)):0} | {Math.Ceiling(50 / (rate * 1.5)):0} | {Math.Ceiling(80 / (rate * 1.5)):0} |");
        report.AppendLine();

        foreach (var risk in Risks) {
            var rows = wearByProfile[risk];
            var profile = rows[0].Expectation.Profile;
            report.AppendLine($"## Perfil {ProfileName(risk)} (nivel {profile.RiskLevel})");
            report.AppendLine();
            report.AppendLine($"Multiplicador ×{profile.ProbabilityMultiplier:0.##} · desgaste máx ×{profile.WearMaxMultiplier:0.##} · "
                + $"tope agravación {profile.AggravationCap} · tope tierra {profile.GroundFailuresCap} · "
                + $"tregua crítica {(profile.CriticalRespiteSeconds == 0 ? "—" : $"{profile.CriticalRespiteSeconds / 60:0} min")}");
            report.AppendLine();
            report.AppendLine("### Fallas en vuelo por severidad");
            report.AppendLine();
            report.AppendLine("| Desgaste | Escala | Leves | Graves | Críticas | Total |");
            report.AppendLine("|---|---|---:|---:|---:|---:|");
            foreach (var row in rows) {
                var e = row.Expectation;
                foreach (var hours in HourScales)
                    report.AppendLine($"| {row.WearPct:0} % | {hours} h | {Fmt(e.MinorPerHour * hours)} | {Fmt(e.MajorPerHour * hours)} | {Fmt(e.CriticalPerHour * hours)} | {Fmt(e.TotalPerHour * hours)} |");
            }
            report.AppendLine();
            report.AppendLine("### Semilla de tierra (fallas esperadas al iniciar cada vuelo)");
            report.AppendLine();
            report.AppendLine("| Desgaste | Fallas esperadas al inicio |");
            report.AppendLine("|---|---:|");
            foreach (var row in rows)
                report.AppendLine($"| {row.WearPct:0} % | {row.Expectation.GroundSeedsPerFlight:0.00} |");
            report.AppendLine();
        }

        return report.ToString();
    }

    // ------------------------------------------------------------------ helpers

    private static void WriteTitle(ExcelWorksheet sheet, string title, int row, int lastColumn) {
        sheet.Cells[row, 1].Value = title;
        sheet.Cells[row, 1].Style.Font.Bold = true;
        sheet.Cells[row, 1].Style.Font.Size = 14;
        sheet.Cells[row, 1, row, lastColumn].Merge = true;
    }

    private static void WriteSubtitle(ExcelWorksheet sheet, string text, int row, int lastColumn) {
        sheet.Cells[row, 1].Value = text;
        sheet.Cells[row, 1].Style.Font.Italic = true;
        sheet.Cells[row, 1].Style.Font.Size = 10;
        sheet.Cells[row, 1].Style.Font.Color.SetColor(Color.FromArgb(0x6B, 0x74, 0x88));
        sheet.Cells[row, 1, row, lastColumn].Merge = true;
    }

    private static void WriteHeaderRow(ExcelWorksheet sheet, int row, params string[] headers) {
        for (var i = 0; i < headers.Length; i++) {
            var cell = sheet.Cells[row, i + 1];
            cell.Value = headers[i];
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0x23, 0x2A, 0x38));
            cell.Style.Font.Color.SetColor(Color.FromArgb(0xE7, 0xEB, 0xF3));
            cell.Style.Font.Bold = true;
        }
    }

    private static void WriteRate(ExcelRangeBase cell, double value) {
        cell.Value = value;
        cell.Style.Numberformat.Format = "0.00";
        if (value == 0) cell.Style.Font.Color.SetColor(Color.FromArgb(0x6B, 0x74, 0x88));
    }

    private static void WriteWear(ExcelRangeBase cell, double wearPct) {
        cell.Value = wearPct / 100.0;
        cell.Style.Numberformat.Format = "0 %";
    }

    private static void HighlightRow(ExcelWorksheet sheet, int row, int columns) {
        for (var c = 1; c <= columns; c++) {
            sheet.Cells[row, c].Style.Fill.PatternType = ExcelFillStyle.Solid;
            sheet.Cells[row, c].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0xEF, 0xF6, 0xFF));
        }
    }

    /// <summary>Bordes suaves para el rango completo de la tabla.</summary>
    private static void FormatTable(ExcelWorksheet sheet, int firstRow, int lastRow, int columns) {
        if (lastRow <= firstRow) return;
        var range = sheet.Cells[firstRow, 1, lastRow, columns];
        range.Style.Border.Top.Style = ExcelBorderStyle.Thin;
        range.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
        range.Style.Border.Top.Color.SetColor(Color.FromArgb(0x1A, 0x20, 0x29));
        range.Style.Border.Bottom.Color.SetColor(Color.FromArgb(0x1A, 0x20, 0x29));
    }

    private static string Fmt(double value) => value.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',');

    /// <summary>Busca la raíz del repo (directorio que contiene docs/) desde la salida del test.</summary>
    private static (string Xlsx, string Markdown) ResolveReportPaths() {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "docs")))
            directory = directory.Parent;
        if (directory is null)
            throw new InvalidOperationException("No se encontró la raíz del repo (carpeta docs/) desde " + AppContext.BaseDirectory);
        var folder = Path.Combine(directory.FullName, "docs", "balance");
        return (Path.Combine(folder, "balance-realistic.xlsx"), Path.Combine(folder, "balance-realistic.md"));
    }

    /// <summary>Rangos de la tabla de 10 h de cada perfil, para armar las series de las gráficas.</summary>
    private static readonly Dictionary<string, (ExcelRangeBase Wear, ExcelRangeBase Graves, ExcelRangeBase Criticals)> TenHoursRange = [];

    private record ProfileExpectationRow(double WearPct, ProfileExpectationDto Expectation);
}
