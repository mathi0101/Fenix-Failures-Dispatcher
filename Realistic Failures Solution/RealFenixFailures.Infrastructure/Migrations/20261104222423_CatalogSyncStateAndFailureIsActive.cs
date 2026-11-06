using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealFenixFailures.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CatalogSyncStateAndFailureIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "FenixFailureDefinitions",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "CatalogSyncStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CatalogsHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SyncedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogSyncStates", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogSyncStates");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "FenixFailureDefinitions");
        }
    }
}
