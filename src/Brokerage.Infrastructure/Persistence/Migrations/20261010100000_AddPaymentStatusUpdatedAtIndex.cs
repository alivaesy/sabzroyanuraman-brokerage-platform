using Brokerage.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brokerage.Infrastructure.Persistence.Migrations;

[DbContext(typeof(BrokerageDbContext))]
[Migration("20261010100000_AddPaymentStatusUpdatedAtIndex")]
public partial class AddPaymentStatusUpdatedAtIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateIndex(
            name: "IX_payment_transactions_Status_UpdatedAt",
            table: "payment_transactions",
            columns: new[] { "Status", "UpdatedAt" });

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropIndex(
            name: "IX_payment_transactions_Status_UpdatedAt",
            table: "payment_transactions");
}
