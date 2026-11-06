using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealFenixFailures.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RebalanceWearRatesLean : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AircraftWearableSystems",
                keyColumn: "Id",
                keyValue: 3,
                column: "WearPerFlightHour",
                value: 0.69999999999999996);

            migrationBuilder.UpdateData(
                table: "AircraftWearableSystems",
                keyColumn: "Id",
                keyValue: 6,
                column: "WearPerFlightHour",
                value: 0.55000000000000004);

            migrationBuilder.UpdateData(
                table: "AircraftWearableSystems",
                keyColumn: "Id",
                keyValue: 8,
                column: "WearPerFlightHour",
                value: 0.875);

            migrationBuilder.UpdateData(
                table: "AircraftWearableSystems",
                keyColumn: "Id",
                keyValue: 14,
                column: "WearPerFlightHour",
                value: 0.875);

            migrationBuilder.UpdateData(
                table: "AircraftWearableSystems",
                keyColumn: "Id",
                keyValue: 16,
                column: "WearPerFlightHour",
                value: 0.94999999999999996);

            migrationBuilder.UpdateData(
                table: "AircraftWearableSystems",
                keyColumn: "Id",
                keyValue: 18,
                column: "WearPerFlightHour",
                value: 0.75);

            migrationBuilder.UpdateData(
                table: "AircraftWearableSystems",
                keyColumn: "Id",
                keyValue: 19,
                column: "WearPerFlightHour",
                value: 0.5);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AircraftWearableSystems",
                keyColumn: "Id",
                keyValue: 3,
                column: "WearPerFlightHour",
                value: 1.0);

            migrationBuilder.UpdateData(
                table: "AircraftWearableSystems",
                keyColumn: "Id",
                keyValue: 6,
                column: "WearPerFlightHour",
                value: 1.25);

            migrationBuilder.UpdateData(
                table: "AircraftWearableSystems",
                keyColumn: "Id",
                keyValue: 8,
                column: "WearPerFlightHour",
                value: 1.875);

            migrationBuilder.UpdateData(
                table: "AircraftWearableSystems",
                keyColumn: "Id",
                keyValue: 14,
                column: "WearPerFlightHour",
                value: 1.875);

            migrationBuilder.UpdateData(
                table: "AircraftWearableSystems",
                keyColumn: "Id",
                keyValue: 16,
                column: "WearPerFlightHour",
                value: 1.25);

            migrationBuilder.UpdateData(
                table: "AircraftWearableSystems",
                keyColumn: "Id",
                keyValue: 18,
                column: "WearPerFlightHour",
                value: 1.75);

            migrationBuilder.UpdateData(
                table: "AircraftWearableSystems",
                keyColumn: "Id",
                keyValue: 19,
                column: "WearPerFlightHour",
                value: 2.5);
        }
    }
}
