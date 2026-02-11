using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppTimeTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddLoggingAndHealth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create AppLogs table
            migrationBuilder.CreateTable(
                name: "AppLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    ContextData = table.Column<string>(type: "TEXT", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SourceComponent = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    StackTrace = table.Column<string>(type: "TEXT", nullable: true),
                    AdditionalMetadata = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLogs", x => x.Id);
                });

            // Create AppLogs indexes
            migrationBuilder.CreateIndex(
                name: "IX_AppLogs_CorrelationId",
                table: "AppLogs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLogs_Severity",
                table: "AppLogs",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_AppLogs_SourceComponent",
                table: "AppLogs",
                column: "SourceComponent");

            migrationBuilder.CreateIndex(
                name: "IX_AppLogs_Timestamp",
                table: "AppLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AppLogs_Timestamp_Severity",
                table: "AppLogs",
                columns: new[] { "Timestamp", "Severity" });

            // Create HealthChecks table
            migrationBuilder.CreateTable(
                name: "HealthChecks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    DatabaseConnected = table.Column<bool>(type: "INTEGER", nullable: false),
                    HookActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    MemoryUsageMB = table.Column<long>(type: "INTEGER", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Details = table.Column<string>(type: "TEXT", nullable: true),
                    PendingEventCount = table.Column<int>(type: "INTEGER", nullable: false),
                    UptimeSeconds = table.Column<long>(type: "INTEGER", nullable: false),
                    LastEventTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RecoveryTriggered = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HealthChecks", x => x.Id);
                });

            // Create HealthChecks indexes
            migrationBuilder.CreateIndex(
                name: "IX_HealthChecks_Status",
                table: "HealthChecks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_HealthChecks_Timestamp",
                table: "HealthChecks",
                column: "Timestamp",
                descending: new bool[] { true });

            migrationBuilder.CreateIndex(
                name: "IX_HealthChecks_Timestamp_Status",
                table: "HealthChecks",
                columns: new[] { "Timestamp", "Status" },
                descending: new bool[] { true, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppLogs");

            migrationBuilder.DropTable(
                name: "HealthChecks");
        }
    }
}
