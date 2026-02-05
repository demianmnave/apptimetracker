using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppTimeTracker.Migrations;

/// <summary>
/// Migration to add composite index on (SessionDate, ProcessName) for query optimization.
/// This index improves performance of aggregation queries used in daily report generation.
/// </summary>
public partial class AddSessionsDateProcessIndex : Migration
{
    /// <summary>
    /// Creates the composite index on AppUsageSessions table.
    /// </summary>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_Sessions_Date_Process",
            table: "AppUsageSessions",
            columns: new[] { "SessionDate", "ProcessName" });
    }

    /// <summary>
    /// Removes the composite index if migration is rolled back.
    /// </summary>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Sessions_Date_Process",
            table: "AppUsageSessions");
    }
}
