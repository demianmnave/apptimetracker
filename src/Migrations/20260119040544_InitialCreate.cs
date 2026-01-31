using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppTimeTracker.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppUsageSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProcessName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ExecutablePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    WindowTitle = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    StartTimeUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTimeUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DurationSeconds = table.Column<long>(type: "INTEGER", nullable: false),
                    SessionDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUsageSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_Date",
                table: "AppUsageSessions",
                column: "SessionDate");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_Date_User",
                table: "AppUsageSessions",
                columns: new[] { "SessionDate", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_Process",
                table: "AppUsageSessions",
                column: "ProcessName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppUsageSessions");
        }
    }
}
