using Brokerage.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brokerage.Infrastructure.Persistence.Migrations;

[DbContext(typeof(BrokerageDbContext))]
[Migration("20261006100000_PersistOtpMfaState")]
public partial class PersistOtpMfaState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "mfa_verifications",
            columns: table => new
            {
                UserId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                VerifiedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_mfa_verifications", x => x.UserId));

        migrationBuilder.CreateTable(
            name: "otp_challenges",
            columns: table => new
            {
                ChallengeId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                UserId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                CodeHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                FailedAttempts = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_otp_challenges", x => x.ChallengeId));

        migrationBuilder.CreateIndex(
            name: "IX_otp_challenges_UserId",
            table: "otp_challenges",
            column: "UserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "otp_challenges");
        migrationBuilder.DropTable(name: "mfa_verifications");
    }
}
