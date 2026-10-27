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
/// docs/balance/balance-realistic.xlsx con hojas Resumen (KPIs, estadísticas, experiencia por vuelo y
/// ancla de desgaste), Gráficas (grilla 2×2: tendencias por desgaste, comparativa y experiencia), Configuración
/// (parámetros del código con los que se generó el archivo) y una hoja por perfil (1/10/100 h × desgaste
/// 0–100 %, semilla de tierra, experiencia por vuelo de 3 h y gráfica propia), más su versión .md diffable.
/// Estimación global por severidad con el código real del motor (física, perfiles, conteos del catálogo
/// curado); sin presets ni fases de vuelo.
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
    private const int ChartColStride = 10;   // columnas entre gráficas del layout en grilla
    private const int ChartRowStride = 22;   // filas entre filas de gráficas (~330 px por gráfica de 320)
    /// <summary>Vuelo típico de referencia para las secciones de experiencia (sincronizado con el default de la app).</summary>
    private static readonly double FlightHours = RealisticExpectationService.DefaultFlightHours;

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

    /// <summary>Color de serie por severidad: 0 = leves, 1 = graves, 2 = críticas.</summary>
    private static Color SeverityColor(int severity) => severity switch {
        0 => Color.FromArgb(0x4C, 0xAF, 0x50),
        1 => Color.FromArgb(0xF5, 0x9E, 0x0B),
        _ => Color.FromArgb(0xEF, 0x44, 0x44),
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
        BuildChartsSheet(package, wearByProfile);
        BuildConfigSheet(package, settings, wearByProfile);
    }

    private static void BuildSummaryContent(ExcelWorksheet sheet, Dictionary<RiskLevel, List<ProfileExpectationRow>> wearByProfile, (int Minor, int Major, int Critical) counts) {
        WriteTitle(sheet, "Balance del modo Realistic — generado automáticamente con el motor vigente", 1, 16);
        WriteSubtitle(sheet, $"Fallas en vuelo por perfil y severidad; semilla de tierra (arranque de sesión) aparte. "
            + $"catálogo curado: {counts.Minor} leves / {counts.Major} graves / {counts.Critical} críticas. Estimación global por severidad (sin presets ni fases). Regenerar: docs/balance/balance.cmd", 2, 16);

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

        // Experiencia por vuelo (centro, col I): lo que siente el jugador, a 3 h con desgaste 0/25/100 %.
        const int expCol = 9;
        sheet.Cells[4, expCol].Value = $"Experiencia por vuelo ({FlightHours:0.#} h; desgaste 0 / 25 / 100 %)";
        sheet.Cells[4, expCol].Style.Font.Bold = true;
        WriteHeaderRow(sheet, 5, expCol, "Perfil", "Desgaste", "E[fallas]", "P(≥1 falla)", "P(≥1 grave)", "P(≥1 crítica)", "1 crítica cada");
        var expRow = 6;
        foreach (var risk in Risks)
            foreach (var wear in new[] { 0.0, 25.0, 100.0 }) {
                var e = wearByProfile[risk].First(r => r.WearPct == wear).Expectation;
                var total = e.TotalPerHour * FlightHours;
                var nameCell = sheet.Cells[expRow, expCol];
                nameCell.Value = ProfileName(risk);
                nameCell.Style.Font.Bold = true;
                nameCell.Style.Font.Color.SetColor(Accent(risk));
                WriteWear(sheet.Cells[expRow, expCol + 1], wear);
                WriteRate(sheet.Cells[expRow, expCol + 2], total);
                var anyCell = sheet.Cells[expRow, expCol + 3];
                anyCell.Value = (1 - Math.Exp(-total)) * 100;
                anyCell.Style.Numberformat.Format = "0.0 \" %\"";
                var majorCell = sheet.Cells[expRow, expCol + 4];
                majorCell.Value = (1 - Math.Exp(-e.MajorPerHour * FlightHours)) * 100;
                majorCell.Style.Numberformat.Format = "0.0 \" %\"";
                if (risk == RiskLevel.Low) {
                    sheet.Cells[expRow, expCol + 5].Value = "—";
                    sheet.Cells[expRow, expCol + 6].Value = "—";
                } else {
                    var criticalCell = sheet.Cells[expRow, expCol + 5];
                    criticalCell.Value = (1 - Math.Exp(-e.CriticalPerHour * FlightHours)) * 100;
                    criticalCell.Style.Numberformat.Format = "0.0 \" %\"";
                    sheet.Cells[expRow, expCol + 6].Value = Math.Round(1 / (e.CriticalPerHour * FlightHours));
                    sheet.Cells[expRow, expCol + 6].Style.Numberformat.Format = "0 \"vuelos\"";
                }
                if (wear == 25) HighlightRow(sheet, expRow, 7, expCol);
                expRow++;
            }
        FormatTable(sheet, 5, expRow - 1, 7, expCol);

        // Ancla temporal (columna izquierda, bajo las estadísticas): vuelos hasta cada nivel de desgaste.
        row += 2;
        sheet.Cells[row, 1].Value = $"Ancla temporal del desgaste (vuelos de {FlightHours:0.#} h; el seed va de 0,1 a 0,6 %/h)";
        sheet.Cells[row, 1].Style.Font.Bold = true;
        row++;
        var anchorHeader = row;
        WriteHeaderRow(sheet, row, "Tasa del sistema", "Hasta 25 %", "Hasta 50 %", "Hasta 80 % (aviso)");
        row++;
        foreach (var rate in new[] { 0.1, 0.3, 0.6 }) {
            sheet.Cells[row, 1].Value = $"{rate:0.##} %/h";
            for (var i = 0; i < 3; i++) {
                var cell = sheet.Cells[row, 2 + i];
                cell.Value = Math.Ceiling(new[] { 25.0, 50.0, 80.0 }[i] / (rate * FlightHours));
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
        WriteTitle(sheet, $"Perfil {ProfileName(risk)} (nivel {profile.RiskLevel})", 1, 16);
        WriteSubtitle(sheet, $"Multiplicador ×{profile.ProbabilityMultiplier:0.##} · desgaste máx ×{profile.WearMaxMultiplier:0.##} · "
            + $"tope agravación {profile.AggravationCap} · tope tierra {profile.GroundFailuresCap} · tregua crítica "
            + (profile.CriticalRespiteSeconds == 0 ? "—" : $"{profile.CriticalRespiteSeconds / 60:0} min"), 2, 16);

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

        // Columna derecha (I): semilla de tierra y experiencia por vuelo, al costado de las tablas de escalas.
        const int sideCol = 9;
        sheet.Cells[4, sideCol].Value = "Semilla de tierra (fallas esperadas al iniciar cada vuelo)";
        sheet.Cells[4, sideCol].Style.Font.Bold = true;
        WriteHeaderRow(sheet, 5, sideCol, "Desgaste", "Fallas al inicio");
        var seedStart = 6;
        var seedRow = seedStart;
        foreach (var row in rows) {
            WriteWear(sheet.Cells[seedRow, sideCol], row.WearPct);
            var cell = sheet.Cells[seedRow, sideCol + 1];
            cell.Value = row.Expectation.GroundSeedsPerFlight;
            cell.Style.Numberformat.Format = "0.00";
            seedRow++;
        }
        FormatTable(sheet, seedStart, seedRow - 1, 2, sideCol);

        // Experiencia por vuelo (3 h de referencia): desgaste 0–100 % en pasos de 25.
        const int expCol = 9;
        var expTitleRow = seedRow + 1;
        sheet.Cells[expTitleRow, expCol].Value = $"Experiencia por vuelo ({FlightHours:0.#} h)";
        sheet.Cells[expTitleRow, expCol].Style.Font.Bold = true;
        var expHeader = expTitleRow + 1;
        WriteHeaderRow(sheet, expHeader, expCol, "Desgaste", "E[fallas]", "P(≥1 falla)", "P(≥1 grave)", "P(≥1 crítica)", "1 crítica cada");
        var expStart = expHeader + 1;
        var expRow = expStart;
        foreach (var row in rows) {
            var e = row.Expectation;
            var total = e.TotalPerHour * FlightHours;
            WriteWear(sheet.Cells[expRow, expCol], row.WearPct);
            WriteRate(sheet.Cells[expRow, expCol + 1], total);
            var anyCell = sheet.Cells[expRow, expCol + 2];
            anyCell.Value = (1 - Math.Exp(-total)) * 100;
            anyCell.Style.Numberformat.Format = "0.0 \" %\"";
            var majorCell = sheet.Cells[expRow, expCol + 3];
            majorCell.Value = (1 - Math.Exp(-e.MajorPerHour * FlightHours)) * 100;
            majorCell.Style.Numberformat.Format = "0.0 \" %\"";
            if (risk == RiskLevel.Low) {
                sheet.Cells[expRow, expCol + 4].Value = "—";
                sheet.Cells[expRow, expCol + 5].Value = "—";
            } else {
                var criticalCell = sheet.Cells[expRow, expCol + 4];
                criticalCell.Value = (1 - Math.Exp(-e.CriticalPerHour * FlightHours)) * 100;
                criticalCell.Style.Numberformat.Format = "0.0 \" %\"";
                sheet.Cells[expRow, expCol + 5].Value = Math.Round(1 / (e.CriticalPerHour * FlightHours));
                sheet.Cells[expRow, expCol + 5].Style.Numberformat.Format = "0 \"vuelos\"";
            }
            if (row.WearPct == 25) HighlightRow(sheet, expRow, 6, expCol);
            expRow++;
        }
        FormatTable(sheet, expHeader, expRow - 1, 6, expCol);

        // Datos de la gráfica y gráfica del perfil: bajo la experiencia, en la misma columna derecha.
        sheet.Cells[expRow + 1, sideCol].Value = $"Datos de la gráfica: E[fallas por vuelo de {FlightHours:0.#} h] por severidad";
        sheet.Cells[expRow + 1, sideCol].Style.Font.Bold = true;
        WriteHeaderRow(sheet, expRow + 2, sideCol, "Desgaste", "Leves", "Graves", "Críticas");
        var chartDataStart = expRow + 3;
        var chartDataRow = chartDataStart;
        foreach (var row in rows) {
            var e = row.Expectation;
            WriteWear(sheet.Cells[chartDataRow, sideCol], row.WearPct);
            WriteRate(sheet.Cells[chartDataRow, sideCol + 1], e.MinorPerHour * FlightHours);
            WriteRate(sheet.Cells[chartDataRow, sideCol + 2], e.MajorPerHour * FlightHours);
            WriteRate(sheet.Cells[chartDataRow, sideCol + 3], e.CriticalPerHour * FlightHours);
            chartDataRow++;
        }
        FormatTable(sheet, expRow + 2, chartDataRow - 1, 4, sideCol);

        // Gráfica del perfil: fallas esperadas por vuelo (3 h) según desgaste, una serie por severidad.
        var wearRange = sheet.Cells[chartDataStart, sideCol, chartDataRow - 1, sideCol];
        var profileChart = (ExcelLineChart)sheet.Drawings.AddChart($"Experiencia_{ProfileName(risk)}", eChartType.LineMarkers);
        profileChart.Title.Text = $"Perfil {ProfileName(risk)}: fallas esperadas por vuelo de {FlightHours:0.#} h, según desgaste";
        profileChart.Legend.Position = eLegendPosition.Bottom;
        profileChart.SetPosition(chartDataRow + 1, 0, sideCol - 1, 0);
        profileChart.SetSize(ChartWidth, ChartHeight);
        for (var s = 0; s < 3; s++) {
            var serie = profileChart.Series.Add(sheet.Cells[chartDataStart, sideCol + 1 + s, chartDataRow - 1, sideCol + 1 + s], wearRange);
            serie.Header = new[] { "Leves", "Graves", "Críticas" }[s];
            serie.Border.LineStyle = OfficeOpenXml.Drawing.eLineStyle.Solid;
            serie.Border.Fill.Color = SeverityColor(s);
        }

        sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
        // Rango de la tabla de 10 h, lo usan las gráficas (A = desgaste, C = graves, D = críticas).
        TenHoursRange[ProfileName(risk)] = (
            Wear: sheet.Cells[tenHoursHeader + 1, 1, tenHoursHeader + rows.Count, 1],
            Graves: sheet.Cells[tenHoursHeader + 1, 3, tenHoursHeader + rows.Count, 3],
            Criticals: sheet.Cells[tenHoursHeader + 1, 4, tenHoursHeader + rows.Count, 4]);
    }

    /// <summary>Hoja Gráficas: grilla 2×2 — tendencias por desgaste, comparativa por perfil y experiencia de vuelo.</summary>
    private static void BuildChartsSheet(ExcelPackage package, Dictionary<RiskLevel, List<ProfileExpectationRow>> wearByProfile) {
        var sheet = package.Workbook.Worksheets.Add("Gráficas");
        WriteTitle(sheet, "Tendencias del balance", 1, 10);

        // Bloque de datos para la gráfica de experiencia general (bajo la grilla 2×2, col A).
        var dataHeader = 2 + 2 * ChartRowStride + 2;
        sheet.Cells[dataHeader - 1, 1].Value = $"Datos: E[fallas por vuelo de {FlightHours:0.#} h] según desgaste (fuente de la gráfica de experiencia)";
        sheet.Cells[dataHeader - 1, 1].Style.Font.Bold = true;
        WriteHeaderRow(sheet, dataHeader, "Desgaste", "Bajo", "Moderado", "Alto", "7700");
        var dataStart = dataHeader + 1;
        var dataRow = dataStart;
        foreach (var wear in WearLevels) {
            WriteWear(sheet.Cells[dataRow, 1], wear);
            for (var i = 0; i < Risks.Length; i++) {
                var e = wearByProfile[Risks[i]].First(r => r.WearPct == wear).Expectation;
                WriteRate(sheet.Cells[dataRow, 2 + i], e.TotalPerHour * FlightHours);
            }
            dataRow++;
        }
        FormatTable(sheet, dataHeader, dataRow - 1, columns: 5);

        // Grilla 2×2: (arriba-izq) graves por desgaste · (arriba-der) críticas por desgaste
        //             (abajo-izq) barras graves por perfil · (abajo-der) experiencia por vuelo por perfil.

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
        criticals.SetPosition(2, 0, ChartColStride, 0);
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
        bars.SetPosition(2 + ChartRowStride, 0, 0, 0);
        bars.SetSize(ChartWidth, ChartHeight);
        var barSerie = bars.Series.Add(summary.Cells[6, 3, 9, 3], summary.Cells[6, 1, 9, 1]);
        barSerie.Header = "Graves / 10 h";
        barSerie.Fill.Color = Color.FromArgb(0x3B, 0x82, 0xF6);

        // Experiencia general: E[fallas por vuelo (3 h)] según desgaste, una serie por perfil con su color.
        var experience = (ExcelLineChart)sheet.Drawings.AddChart("ExperienciaPorDesgaste", eChartType.LineMarkers);
        experience.Title.Text = $"Experiencia por vuelo: fallas esperadas en {FlightHours:0.#} h, según desgaste";
        experience.Legend.Position = eLegendPosition.Bottom;
        experience.SetPosition(2 + ChartRowStride, 0, ChartColStride, 0);
        experience.SetSize(ChartWidth, ChartHeight);
        var wearColumn = sheet.Cells[dataStart, 1, dataRow - 1, 1];
        for (var i = 0; i < Risks.Length; i++) {
            var serie = experience.Series.Add(sheet.Cells[dataStart, 2 + i, dataRow - 1, 2 + i], wearColumn);
            serie.Header = ProfileName(Risks[i]);
            serie.Border.LineStyle = OfficeOpenXml.Drawing.eLineStyle.Solid;
            serie.Border.Fill.Color = Accent(Risks[i]);
        }
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

        // Tabla de perfiles al costado (col D), a la derecha de las secciones de parámetros.
        const int profCol = 4;
        sheet.Cells[4, profCol].Value = "Perfiles vigentes (defaults de código)";
        sheet.Cells[4, profCol].Style.Font.Bold = true;
        WriteHeaderRow(sheet, 5, profCol, "Perfil", "Multiplicador", "Desgaste máx", "Tope agravación", "Tope tierra", "Tregua crítica");
        var profileRow = 6;
        foreach (var risk in Risks) {
            var profile = wearByProfile[risk][0].Expectation.Profile;
            var nameCell = sheet.Cells[profileRow, profCol];
            nameCell.Value = ProfileName(risk);
            nameCell.Style.Font.Bold = true;
            nameCell.Style.Font.Color.SetColor(Accent(risk));
            var multCell = sheet.Cells[profileRow, profCol + 1];
            multCell.Value = profile.ProbabilityMultiplier;
            multCell.Style.Numberformat.Format = "0.00";
            var wearCell = sheet.Cells[profileRow, profCol + 2];
            wearCell.Value = profile.WearMaxMultiplier;
            wearCell.Style.Numberformat.Format = "0.00";
            var aggrCell = sheet.Cells[profileRow, profCol + 3];
            aggrCell.Value = profile.AggravationCap;
            aggrCell.Style.Numberformat.Format = "0";
            var groundCell = sheet.Cells[profileRow, profCol + 4];
            groundCell.Value = profile.GroundFailuresCap;
            groundCell.Style.Numberformat.Format = "0";
            sheet.Cells[profileRow, profCol + 5].Value = profile.CriticalRespiteSeconds == 0 ? "—" : $"{profile.CriticalRespiteSeconds / 60:0} min";
            profileRow++;
        }
        FormatTable(sheet, 5, profileRow - 1, 6, profCol);

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

        report.AppendLine($"## Experiencia por vuelo ({FlightHours:0.#} h; desgaste 0 / 25 / 100 %)");
        report.AppendLine();
        report.AppendLine("_Lo que siente el jugador por vuelo típico —la vista de 10 h de las tablas de abajo es para comparar perfiles—. Tres niveles de desgaste para comparar cómo empeora con el uso._");
        report.AppendLine();
        report.AppendLine("| Perfil | Desgaste | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |");
        report.AppendLine("|---|---|---:|---:|---:|---:|---:|");
        foreach (var risk in Risks)
            foreach (var wear in new[] { 0.0, 25.0, 100.0 }) {
                var e = wearByProfile[risk].First(r => r.WearPct == wear).Expectation;
                var total = e.TotalPerHour * FlightHours;
                report.AppendLine($"| **{ProfileName(risk)}** | {wear:0} % | {Fmt(total)} | {(1 - Math.Exp(-total)) * 100:0.0} % | {(1 - Math.Exp(-e.MajorPerHour * FlightHours)) * 100:0.0} % | "
                    + (risk == RiskLevel.Low ? "—" : $"{(1 - Math.Exp(-e.CriticalPerHour * FlightHours)) * 100:0.0} %") + " | "
                    + (risk == RiskLevel.Low ? "—" : $"**{Math.Round(1 / (e.CriticalPerHour * FlightHours)):0} vuelos**") + " |");
            }
        report.AppendLine();
        report.AppendLine("### Ancla temporal del desgaste");
        report.AppendLine();
        report.AppendLine($"Vuelos de {FlightHours:0.#} h que tarda un sistema en acumular cada nivel de desgaste, según su tasa (`WearPerFlightHour`; el seed va de 0,1 a 0,6 %/h):");
        report.AppendLine();
        report.AppendLine("| Tasa del sistema | Hasta 25 % | Hasta 50 % | Hasta 80 % (aviso) |");
        report.AppendLine("|---|---:|---:|---:|");
        foreach (var rate in new[] { 0.1, 0.3, 0.6 })
            report.AppendLine($"| {rate:0.##} %/h | {Math.Ceiling(25 / (rate * FlightHours)):0} | {Math.Ceiling(50 / (rate * FlightHours)):0} | {Math.Ceiling(80 / (rate * FlightHours)):0} |");
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
            report.AppendLine($"### Experiencia por vuelo ({FlightHours:0.#} h)");
            report.AppendLine();
            report.AppendLine("| Desgaste | E[fallas] | P(≥1 falla) | P(≥1 grave) | P(≥1 crítica) | 1 crítica cada |");
            report.AppendLine("|---|---:|---:|---:|---:|---:|");
            foreach (var row in rows) {
                var e = row.Expectation;
                var total = e.TotalPerHour * FlightHours;
                report.AppendLine($"| {row.WearPct:0} % | {Fmt(total)} | {(1 - Math.Exp(-total)) * 100:0.0} % | {(1 - Math.Exp(-e.MajorPerHour * FlightHours)) * 100:0.0} % | "
                    + (risk == RiskLevel.Low ? "—" : $"{(1 - Math.Exp(-e.CriticalPerHour * FlightHours)) * 100:0.0} %") + " | "
                    + (risk == RiskLevel.Low ? "—" : $"**{Math.Round(1 / (e.CriticalPerHour * FlightHours)):0} vuelos**") + " |");
            }
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

    private static void WriteHeaderRow(ExcelWorksheet sheet, int row, int startCol, params string[] headers) {
        for (var i = 0; i < headers.Length; i++) {
            var cell = sheet.Cells[row, startCol + i];
            cell.Value = headers[i];
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0x23, 0x2A, 0x38));
            cell.Style.Font.Color.SetColor(Color.FromArgb(0xE7, 0xEB, 0xF3));
            cell.Style.Font.Bold = true;
        }
    }

    private static void WriteHeaderRow(ExcelWorksheet sheet, int row, params string[] headers) => WriteHeaderRow(sheet, row, 1, headers);

    private static void WriteRate(ExcelRangeBase cell, double value) {
        cell.Value = value;
        cell.Style.Numberformat.Format = "0.00";
        if (value == 0) cell.Style.Font.Color.SetColor(Color.FromArgb(0x6B, 0x74, 0x88));
    }

    private static void WriteWear(ExcelRangeBase cell, double wearPct) {
        cell.Value = wearPct / 100.0;
        cell.Style.Numberformat.Format = "0 %";
    }

    private static void HighlightRow(ExcelWorksheet sheet, int row, int columns, int startCol = 1) {
        for (var c = startCol; c < startCol + columns; c++) {
            sheet.Cells[row, c].Style.Fill.PatternType = ExcelFillStyle.Solid;
            sheet.Cells[row, c].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0xEF, 0xF6, 0xFF));
        }
    }

    /// <summary>Bordes suaves para el rango completo de la tabla.</summary>
    private static void FormatTable(ExcelWorksheet sheet, int firstRow, int lastRow, int columns, int startCol = 1) {
        if (lastRow <= firstRow) return;
        var range = sheet.Cells[firstRow, startCol, lastRow, startCol + columns - 1];
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
