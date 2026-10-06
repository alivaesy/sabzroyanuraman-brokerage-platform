using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brokerage.Infrastructure.Persistence.Migrations
{
    [Migration("20261005093520_AddAuditEvents")]
    public partial class AddAuditEvents : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_events",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ServiceRequestId = table.Column<Guid>(type: "TEXT", nullable: true),
                    WorkflowStage = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Outcome = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    PreviousState = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    NewState = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ActorUserId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ActorRole = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_events", x => x.EventId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_OccurredAt",
                table: "audit_events",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_ServiceRequestId",
                table: "audit_events",
                column: "ServiceRequestId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "audit_events");
        }
    }
}
