using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealFenixFailures.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionEventHistoryAndDerivedAircraftTotals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalFlightHours",
                table: "UserAircrafts");

            migrationBuilder.DropColumn(
                name: "TotalFlights",
                table: "UserAircrafts");

            migrationBuilder.AddColumn<string>(
                name: "AboveAltitude",
                table: "TriggeredFailures",
                type: "TEXT",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AfterEvent",
                table: "TriggeredFailures",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AfterEventSeconds",
                table: "TriggeredFailures",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BelowAltitude",
                table: "TriggeredFailures",
                type: "TEXT",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureName",
                table: "TriggeredFailures",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ias",
                table: "TriggeredFailures",
                type: "TEXT",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PresetId",
                table: "TriggeredFailures",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PresetName",
                table: "TriggeredFailures",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Probability",
                table: "TriggeredFailures",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProbabilityGroup",
                table: "TriggeredFailures",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Severity",
                table: "TriggeredFailures",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Time",
                table: "TriggeredFailures",
                type: "TEXT",
                maxLength: 10,
                nullable: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_SessionEventLogs_FlightSessionId_OccurredAt",
                table: "SessionEventLogs",
                columns: new[] { "FlightSessionId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionEventLogs_TriggeredFailureId",
                table: "SessionEventLogs",
                column: "TriggeredFailureId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SessionEventLogs");

            migrationBuilder.DropColumn(
                name: "AboveAltitude",
                table: "TriggeredFailures");

            migrationBuilder.DropColumn(
                name: "AfterEvent",
                table: "TriggeredFailures");

            migrationBuilder.DropColumn(
                name: "AfterEventSeconds",
                table: "TriggeredFailures");

            migrationBuilder.DropColumn(
                name: "BelowAltitude",
                table: "TriggeredFailures");

            migrationBuilder.DropColumn(
                name: "FailureName",
                table: "TriggeredFailures");

            migrationBuilder.DropColumn(
                name: "Ias",
                table: "TriggeredFailures");

            migrationBuilder.DropColumn(
                name: "PresetId",
                table: "TriggeredFailures");

            migrationBuilder.DropColumn(
                name: "PresetName",
                table: "TriggeredFailures");

            migrationBuilder.DropColumn(
                name: "Probability",
                table: "TriggeredFailures");

            migrationBuilder.DropColumn(
                name: "ProbabilityGroup",
                table: "TriggeredFailures");

            migrationBuilder.DropColumn(
                name: "Severity",
                table: "TriggeredFailures");

            migrationBuilder.DropColumn(
                name: "Time",
                table: "TriggeredFailures");

            migrationBuilder.AddColumn<double>(
                name: "TotalFlightHours",
                table: "UserAircrafts",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "TotalFlights",
                table: "UserAircrafts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
