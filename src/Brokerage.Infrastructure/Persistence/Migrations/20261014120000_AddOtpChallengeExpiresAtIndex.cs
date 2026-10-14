using Brokerage.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brokerage.Infrastructure.Persistence.Migrations;

[DbContext(typeof(BrokerageDbContext))]
[Migration("20261014120000_AddOtpChallengeExpiresAtIndex")]
public partial class AddOtpChallengeExpiresAtIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateIndex(
            name: "IX_otp_challenges_ExpiresAt",
            table: "otp_challenges",
            column: "ExpiresAt");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropIndex(
            name: "IX_otp_challenges_ExpiresAt",
            table: "otp_challenges");
}
