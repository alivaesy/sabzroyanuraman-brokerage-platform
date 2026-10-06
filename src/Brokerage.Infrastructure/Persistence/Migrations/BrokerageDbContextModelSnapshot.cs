using System;
using Brokerage.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Brokerage.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(BrokerageDbContext))]
    partial class BrokerageDbContextModelSnapshot : ModelSnapshot
    {
        /// <inheritdoc />
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "10.0.12");

            modelBuilder.Entity("Brokerage.Domain.Entities.Expert", b =>
                {
                    b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("TEXT");
                    b.Property<string>("ExpertId").IsRequired().HasColumnType("TEXT");
                    b.Property<string>("ExpertType").IsRequired().HasColumnType("TEXT");
                    b.Property<bool>("IsActive").HasColumnType("INTEGER");
                    b.HasKey("Id");
                    b.ToTable("experts", (string)null);
                });

            modelBuilder.Entity("Brokerage.Domain.Entities.IdentityVerificationState", b =>
                {
                    b.Property<string>("UserId").HasMaxLength(200).HasColumnType("TEXT");
                    b.Property<bool>("IsVerified").HasColumnType("INTEGER");
                    b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("TEXT");
                    b.Property<DateTimeOffset?>("VerifiedAt").HasColumnType("TEXT");
                    b.HasKey("UserId");
                    b.ToTable("identity_verification_states", (string)null);
                });

            modelBuilder.Entity("Brokerage.Domain.Entities.ServiceRequest", b =>
                {
                    b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("TEXT");
                    b.Property<string>("ApplicantUserId").IsRequired().HasMaxLength(200).HasColumnType("TEXT");
                    b.Property<DateTimeOffset>("CreatedAt").HasColumnType("TEXT");
                    b.Property<Guid?>("CurrentWorkflowStageId").HasColumnType("TEXT");
                    b.Property<string>("OrganizationStatus").HasMaxLength(100).HasColumnType("TEXT");
                    b.Property<string>("OrganizationTrackingId").HasMaxLength(200).HasColumnType("TEXT");
                    b.Property<string>("ServiceCode").IsRequired().HasMaxLength(50).HasColumnType("TEXT");
                    b.Property<string>("Status").IsRequired().HasMaxLength(50).HasColumnType("TEXT");
                    b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("TEXT");
                    b.HasKey("Id");
                    b.HasIndex("ApplicantUserId");
                    b.ToTable("service_requests", (string)null);
                });

            modelBuilder.Entity("Brokerage.Infrastructure.Persistence.AuditEventRecord", b =>
                {
                    b.Property<Guid>("EventId").ValueGeneratedOnAdd().HasColumnType("TEXT");
                    b.Property<string>("ActorRole").HasMaxLength(100).HasColumnType("TEXT");
                    b.Property<string>("ActorUserId").HasMaxLength(200).HasColumnType("TEXT");
                    b.Property<string>("CorrelationId").IsRequired().HasMaxLength(200).HasColumnType("TEXT");
                    b.Property<string>("EventType").IsRequired().HasMaxLength(100).HasColumnType("TEXT");
                    b.Property<string>("IpAddress").HasMaxLength(64).HasColumnType("TEXT");
                    b.Property<string>("NewState").HasMaxLength(200).HasColumnType("TEXT");
                    b.Property<string>("Outcome").IsRequired().HasMaxLength(50).HasColumnType("TEXT");
                    b.Property<string>("PreviousState").HasMaxLength(200).HasColumnType("TEXT");
                    b.Property<DateTimeOffset>("OccurredAt").HasColumnType("TEXT");
                    b.Property<Guid?>("ServiceRequestId").HasColumnType("TEXT");
                    b.Property<string>("WorkflowStage").HasMaxLength(100).HasColumnType("TEXT");
                    b.HasKey("EventId");
                    b.HasIndex("OccurredAt");
                    b.HasIndex("ServiceRequestId");
                    b.ToTable("audit_events", (string)null);
                });

            modelBuilder.Entity("Brokerage.Domain.Entities.WorkflowStage", b =>
                {
                    b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("TEXT");
                    b.Property<DateTimeOffset?>("CompletedAt").HasColumnType("TEXT");
                    b.Property<DateTimeOffset>("CreatedAt").HasColumnType("TEXT");
                    b.Property<Guid>("ServiceRequestId").HasColumnType("TEXT");
                    b.Property<string>("StageCode").IsRequired().HasMaxLength(100).HasColumnType("TEXT");
                    b.HasKey("Id");
                    b.HasIndex("ServiceRequestId");
                    b.ToTable("workflow_stages", (string)null);
                });

            modelBuilder.Entity("Brokerage.Domain.Entities.WorkflowStage", b =>
                {
                    b.HasOne("Brokerage.Domain.Entities.ServiceRequest", null)
                        .WithMany()
                        .HasForeignKey("ServiceRequestId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });
#pragma warning restore 612, 618
        }
    }
}