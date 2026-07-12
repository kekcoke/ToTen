using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToTen.Worker.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ManifestId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorId = table.Column<string>(type: "text", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Payload = table.Column<JsonDocument>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_EventType",
                table: "AuditLogEntries",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_ItemId",
                table: "AuditLogEntries",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_ManifestId",
                table: "AuditLogEntries",
                column: "ManifestId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_OccurredAt",
                table: "AuditLogEntries",
                column: "OccurredAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogEntries");
        }
    }
}
