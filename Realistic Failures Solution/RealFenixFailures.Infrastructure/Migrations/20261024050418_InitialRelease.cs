using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RealFenixFailures.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialRelease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AircraftWearableSystems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AtaId = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ShortName = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    WearPerFlightHour = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AircraftWearableSystems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FailurePresetTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FailurePresetTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FenixFailureSystems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ShortName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AtaId = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FenixFailureSystems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FlightPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OriginIcao = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    DestinationIcao = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    AircraftIcaoType = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    FlightNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Callsign = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    OffBlockUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EstimatedArrivalUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EstimatedDurationMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    RouteText = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    RouteDistanceNM = table.Column<int>(type: "INTEGER", nullable: false),
                    RawData = table.Column<string>(type: "TEXT", nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DownloadedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ValidFromUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ValidUntilUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ValidationState = table.Column<int>(type: "INTEGER", nullable: false),
                    ValidationMessage = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlightPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserAircrafts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Registration = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    IcaoTypeCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAircrafts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FailurePresets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 600, nullable: false),
                    TriggerDescription = table.Column<string>(type: "TEXT", maxLength: 600, nullable: false),
                    PresetType = table.Column<int>(type: "INTEGER", nullable: false),
                    FlightPhase = table.Column<int>(type: "INTEGER", nullable: false),
                    Difficulty = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FailurePresets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FailurePresets_FailurePresetTypes_PresetType",
                        column: x => x.PresetType,
                        principalTable: "FailurePresetTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FenixFailureGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SystemId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FenixFailureGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FenixFailureGroups_FenixFailureSystems_SystemId",
                        column: x => x.SystemId,
                        principalTable: "FenixFailureSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AircraftSystemWears",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserAircraftId = table.Column<int>(type: "INTEGER", nullable: false),
                    WearableSystemId = table.Column<int>(type: "INTEGER", nullable: false),
                    WearPercentage = table.Column<double>(type: "REAL", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AircraftSystemWears", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AircraftSystemWears_AircraftWearableSystems_WearableSystemId",
                        column: x => x.WearableSystemId,
                        principalTable: "AircraftWearableSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AircraftSystemWears_UserAircrafts_UserAircraftId",
                        column: x => x.UserAircraftId,
                        principalTable: "UserAircrafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FlightSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FlightPlanId = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RiskLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    UserAircraftId = table.Column<int>(type: "INTEGER", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DiscardedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlightSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlightSessions_FlightPlans_FlightPlanId",
                        column: x => x.FlightPlanId,
                        principalTable: "FlightPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FlightSessions_UserAircrafts_UserAircraftId",
                        column: x => x.UserAircraftId,
                        principalTable: "UserAircrafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FenixFailureDefinitions",
                columns: table => new
                {
                    FenixFailureId = table.Column<string>(type: "TEXT", maxLength: 180, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    GroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    CanBeTemporary = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    CanBeRepaired = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FenixFailureDefinitions", x => x.FenixFailureId);
                    table.ForeignKey(
                        name: "FK_FenixFailureDefinitions_FenixFailureGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "FenixFailureGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionSystemWearDeltas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FlightSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    AtaId = table.Column<int>(type: "INTEGER", nullable: false),
                    SystemName = table.Column<string>(type: "TEXT", nullable: false),
                    DeltaPercentage = table.Column<double>(type: "REAL", nullable: false),
                    TotalPercentage = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionSystemWearDeltas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionSystemWearDeltas_FlightSessions_FlightSessionId",
                        column: x => x.FlightSessionId,
                        principalTable: "FlightSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PresetFailureDefinitions",
                columns: table => new
                {
                    PresetId = table.Column<int>(type: "INTEGER", nullable: false),
                    FenixFailureId = table.Column<string>(type: "TEXT", maxLength: 180, nullable: false),
                    ProbabilityGroup = table.Column<int>(type: "INTEGER", nullable: true),
                    Probability = table.Column<double>(type: "REAL", nullable: false),
                    Ias = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Above_Altitude = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Below_Altitude = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Time = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    AfterEvent = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    AfterEventSeconds = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PresetFailureDefinitions", x => new { x.PresetId, x.FenixFailureId });
                    table.ForeignKey(
                        name: "FK_PresetFailureDefinitions_FailurePresets_PresetId",
                        column: x => x.PresetId,
                        principalTable: "FailurePresets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PresetFailureDefinitions_FenixFailureDefinitions_FenixFailureId",
                        column: x => x.FenixFailureId,
                        principalTable: "FenixFailureDefinitions",
                        principalColumn: "FenixFailureId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TriggeredFailures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FlightSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    FenixFailureId = table.Column<string>(type: "TEXT", nullable: false),
                    TriggeredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FlightPhase = table.Column<int>(type: "INTEGER", nullable: false),
                    FailureName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Severity = table.Column<int>(type: "INTEGER", nullable: true),
                    PresetId = table.Column<int>(type: "INTEGER", nullable: true),
                    PresetName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Probability = table.Column<double>(type: "REAL", nullable: true),
                    ProbabilityGroup = table.Column<int>(type: "INTEGER", nullable: true),
                    Ias = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    AboveAltitude = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    BelowAltitude = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Time = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    AfterEvent = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    AfterEventSeconds = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    State = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 2),
                    FenixConfirmedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ResolvedPhase = table.Column<int>(type: "INTEGER", nullable: true),
                    ResolutionSource = table.Column<int>(type: "INTEGER", nullable: true),
                    CanBeTemporary = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    PreparedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriggeredFailures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TriggeredFailures_FenixFailureDefinitions_FenixFailureId",
                        column: x => x.FenixFailureId,
                        principalTable: "FenixFailureDefinitions",
                        principalColumn: "FenixFailureId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TriggeredFailures_FlightSessions_FlightSessionId",
                        column: x => x.FlightSessionId,
                        principalTable: "FlightSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessionEventLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FlightSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EventCode = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    FlightPhase = table.Column<int>(type: "INTEGER", nullable: false),
                    TriggeredFailureId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionEventLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionEventLogs_FlightSessions_FlightSessionId",
                        column: x => x.FlightSessionId,
                        principalTable: "FlightSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SessionEventLogs_TriggeredFailures_TriggeredFailureId",
                        column: x => x.TriggeredFailureId,
                        principalTable: "TriggeredFailures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "AircraftWearableSystems",
                columns: new[] { "Id", "AtaId", "DisplayOrder", "Name", "ShortName", "WearPerFlightHour" },
                values: new object[,]
                {
                    { 1, 21, 1, "Air Conditioning", "Air", 0.20000000000000001 },
                    { 2, 22, 2, "AutoFlight", "AutoFlight", 0.10000000000000001 },
                    { 3, 23, 3, "Communications", "Communications", 0.29999999999999999 },
                    { 4, 24, 4, "Electrical power", "Elec", 0.29999999999999999 },
                    { 5, 26, 5, "Fire protection", "Fire", 0.20000000000000001 },
                    { 6, 27, 6, "Flight controls", "Flight controls", 0.40000000000000002 },
                    { 7, 28, 7, "Fuel", "Fuel", 0.59999999999999998 },
                    { 8, 29, 8, "Hydraulic power", "Hydraulic", 0.59999999999999998 },
                    { 9, 30, 9, "Ice and rain protection", "Ice/Rain", 0.10000000000000001 },
                    { 10, 31, 10, "Indicating/Recording system", "Indicating/Recording", 0.29999999999999999 },
                    { 11, 32, 11, "Landing gear", "Gear", 0.20000000000000001 },
                    { 12, 34, 12, "Navigation", "Navigation", 0.29999999999999999 },
                    { 13, 35, 13, "Oxygen", "Oxygen", 0.10000000000000001 },
                    { 14, 36, 14, "Pneumatic", "Pneumatic", 0.59999999999999998 },
                    { 15, 46, 15, "Information systems", "Information", 0.29999999999999999 },
                    { 16, 49, 16, "APU", "APU", 0.40000000000000002 },
                    { 17, 52, 17, "Doors", "Doors", 0.10000000000000001 },
                    { 18, 70, 18, "Power plant", "Engines", 0.5 },
                    { 19, 80, 19, "Start faults", "Start Faults", 0.80000000000000004 }
                });

            migrationBuilder.InsertData(
                table: "FailurePresetTypes",
                columns: new[] { "Id", "Description" },
                values: new object[,]
                {
                    { 1, "RealisticMode" },
                    { 2, "TrainingMode" },
                    { 3, "Custom" },
                    { 4, "UserPreset" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AircraftSystemWears_UserAircraftId_WearableSystemId",
                table: "AircraftSystemWears",
                columns: new[] { "UserAircraftId", "WearableSystemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AircraftSystemWears_WearableSystemId",
                table: "AircraftSystemWears",
                column: "WearableSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_FailurePresets_PresetType",
                table: "FailurePresets",
                column: "PresetType");

            migrationBuilder.CreateIndex(
                name: "IX_FenixFailureDefinitions_GroupId",
                table: "FenixFailureDefinitions",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_FenixFailureGroups_SystemId",
                table: "FenixFailureGroups",
                column: "SystemId");

            migrationBuilder.CreateIndex(
                name: "IX_FlightSessions_FlightPlanId",
                table: "FlightSessions",
                column: "FlightPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_FlightSessions_UserAircraftId",
                table: "FlightSessions",
                column: "UserAircraftId");

            migrationBuilder.CreateIndex(
                name: "IX_PresetFailureDefinitions_FenixFailureId",
                table: "PresetFailureDefinitions",
                column: "FenixFailureId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionEventLogs_FlightSessionId_OccurredAt",
                table: "SessionEventLogs",
                columns: new[] { "FlightSessionId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionEventLogs_TriggeredFailureId",
                table: "SessionEventLogs",
                column: "TriggeredFailureId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionSystemWearDeltas_FlightSessionId",
                table: "SessionSystemWearDeltas",
                column: "FlightSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_TriggeredFailures_FenixFailureId",
                table: "TriggeredFailures",
                column: "FenixFailureId");

            migrationBuilder.CreateIndex(
                name: "IX_TriggeredFailures_FlightSessionId",
                table: "TriggeredFailures",
                column: "FlightSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAircrafts_Registration",
                table: "UserAircrafts",
                column: "Registration",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AircraftSystemWears");

            migrationBuilder.DropTable(
                name: "PresetFailureDefinitions");

            migrationBuilder.DropTable(
                name: "SessionEventLogs");

            migrationBuilder.DropTable(
                name: "SessionSystemWearDeltas");

            migrationBuilder.DropTable(
                name: "AircraftWearableSystems");

            migrationBuilder.DropTable(
                name: "FailurePresets");

            migrationBuilder.DropTable(
                name: "TriggeredFailures");

            migrationBuilder.DropTable(
                name: "FailurePresetTypes");

            migrationBuilder.DropTable(
                name: "FenixFailureDefinitions");

            migrationBuilder.DropTable(
                name: "FlightSessions");

            migrationBuilder.DropTable(
                name: "FenixFailureGroups");

            migrationBuilder.DropTable(
                name: "FlightPlans");

            migrationBuilder.DropTable(
                name: "UserAircrafts");

            migrationBuilder.DropTable(
                name: "FenixFailureSystems");
        }
    }
}
