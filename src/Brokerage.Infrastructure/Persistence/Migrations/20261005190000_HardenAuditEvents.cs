using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brokerage.Infrastructure.Persistence.Migrations;

[Migration("20261005190000_HardenAuditEvents")]
public partial class HardenAuditEvents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TRIGGER IF NOT EXISTS "TR_audit_events_no_update"
            BEFORE UPDATE ON "audit_events"
            BEGIN
                SELECT RAISE(ABORT, 'audit_events are immutable and cannot be updated');
            END;
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER IF NOT EXISTS "TR_audit_events_no_delete"
            BEFORE DELETE ON "audit_events"
            BEGIN
                SELECT RAISE(ABORT, 'audit_events are immutable and cannot be deleted');
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS "TR_audit_events_no_update";
            """);

        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS "TR_audit_events_no_delete";
            """);
    }
}