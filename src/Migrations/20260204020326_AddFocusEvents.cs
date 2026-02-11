using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppTimeTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddFocusEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FocusEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProcessName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    WindowTitle = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    StartTimeUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTimeUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    EventDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    PersistedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FocusEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FocusEvents_EventDate",
                table: "FocusEvents",
                column: "EventDate");

            migrationBuilder.CreateIndex(
                name: "IX_FocusEvents_EventDate_UserId",
                table: "FocusEvents",
                columns: new[] { "EventDate", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_FocusEvents_ProcessName",
                table: "FocusEvents",
                column: "ProcessName");

            migrationBuilder.CreateIndex(
                name: "IX_FocusEvents_UserId",
                table: "FocusEvents",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FocusEvents");
        }
    }
}
