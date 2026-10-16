using Brokerage.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brokerage.Infrastructure.Persistence.Migrations;

[DbContext(typeof(BrokerageDbContext))]
[Migration("20261008170000_AddPaymentTransactions")]
public partial class AddPaymentTransactions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "payment_transactions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ServiceRequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                Amount = table.Column<long>(type: "INTEGER", nullable: false),
                Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                IdempotencyKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                GatewayToken = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                GatewayReference = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                VerifiedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_payment_transactions", x => x.Id);
                table.ForeignKey(
                    name: "FK_payment_transactions_service_requests_ServiceRequestId",
                    column: x => x.ServiceRequestId,
                    principalTable: "service_requests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_payment_transactions_IdempotencyKey",
            table: "payment_transactions",
            column: "IdempotencyKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_payment_transactions_GatewayReference",
            table: "payment_transactions",
            column: "GatewayReference");

        migrationBuilder.CreateIndex(
            name: "IX_payment_transactions_ServiceRequestId",
            table: "payment_transactions",
            column: "ServiceRequestId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "payment_transactions");
    }
}
