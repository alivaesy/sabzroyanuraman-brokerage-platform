using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brokerage.Infrastructure.Persistence.Migrations
{
    [Migration("20261005043933_InitialCreate")]
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "experts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExpertId = table.Column<string>(type: "TEXT", nullable: false),
                    ExpertType = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_experts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "identity_verification_states",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IsVerified = table.Column<bool>(type: "INTEGER", nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_verification_states", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "service_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApplicantUserId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ServiceCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CurrentWorkflowStageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    OrganizationTrackingId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    OrganizationStatus = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_stages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ServiceRequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StageCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_stages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workflow_stages_service_requests_ServiceRequestId",
                        column: x => x.ServiceRequestId,
                        principalTable: "service_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_service_requests_ApplicantUserId",
                table: "service_requests",
                column: "ApplicantUserId");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_stages_ServiceRequestId",
                table: "workflow_stages",
                column: "ServiceRequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "experts");
            migrationBuilder.DropTable(name: "identity_verification_states");
            migrationBuilder.DropTable(name: "workflow_stages");
            migrationBuilder.DropTable(name: "service_requests");
        }
    }
}