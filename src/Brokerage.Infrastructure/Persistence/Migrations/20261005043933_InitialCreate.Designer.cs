using System;
using Brokerage.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Brokerage.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(BrokerageDbContext))]
    partial class InitialCreate
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
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