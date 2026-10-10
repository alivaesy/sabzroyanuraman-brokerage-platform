using Brokerage.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Brokerage.Infrastructure.Persistence;

public sealed class BrokerageDbContext(DbContextOptions<BrokerageDbContext> options) : DbContext(options)
{
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<WorkflowStage> WorkflowStages => Set<WorkflowStage>();
    public DbSet<Expert> Experts => Set<Expert>();
    public DbSet<IdentityVerificationState> IdentityVerificationStates => Set<IdentityVerificationState>();
    public DbSet<AuditEventRecord> AuditEvents => Set<AuditEventRecord>();
    public DbSet<OtpChallengeRecord> OtpChallenges => Set<OtpChallengeRecord>();
    public DbSet<MfaVerificationRecord> MfaVerifications => Set<MfaVerificationRecord>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ServiceRequest>(entity =>
        {
            entity.ToTable("service_requests");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ApplicantUserId).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => x.ApplicantUserId);
            entity.Property(x => x.ServiceCode).HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.Property(x => x.OrganizationTrackingId).HasMaxLength(200);
            entity.Property(x => x.OrganizationStatus).HasMaxLength(100);
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.Property(x => x.UpdatedAt).IsRequired();
            entity.HasMany<WorkflowStage>().WithOne().HasForeignKey(x => x.ServiceRequestId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkflowStage>(entity =>
        {
            entity.ToTable("workflow_stages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.StageCode).HasMaxLength(100).IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<Expert>(entity =>
        {
            entity.ToTable("experts");
            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<IdentityVerificationState>(entity =>
        {
            entity.ToTable("identity_verification_states");
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.UserId).HasMaxLength(200).IsRequired();
            entity.Property(x => x.IsVerified).IsRequired();
            entity.Property(x => x.UpdatedAt).IsRequired();
        });

        modelBuilder.Entity<OtpChallengeRecord>(entity =>
        {
            entity.ToTable("otp_challenges");
            entity.HasKey(x => x.ChallengeId);
            entity.Property(x => x.ChallengeId).HasMaxLength(64).IsRequired();
            entity.Property(x => x.UserId).HasMaxLength(200).IsRequired();
            entity.Property(x => x.CodeHash).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ExpiresAt).IsRequired();
            entity.Property(x => x.FailedAttempts).IsRequired();
            entity.HasIndex(x => x.UserId);
        });

        modelBuilder.Entity<MfaVerificationRecord>(entity =>
        {
            entity.ToTable("mfa_verifications");
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.UserId).HasMaxLength(200).IsRequired();
            entity.Property(x => x.VerifiedAt).IsRequired();
        });

        modelBuilder.Entity<AuditEventRecord>(entity =>
        {
            entity.ToTable("audit_events");
            entity.HasKey(x => x.EventId);
            entity.Property(x => x.EventType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.CorrelationId).HasMaxLength(200).IsRequired();
            entity.Property(x => x.WorkflowStage).HasMaxLength(100);
            entity.Property(x => x.Outcome).HasMaxLength(50).IsRequired();
            entity.Property(x => x.PreviousState).HasMaxLength(200);
            entity.Property(x => x.NewState).HasMaxLength(200);
            entity.Property(x => x.ActorUserId).HasMaxLength(200);
            entity.Property(x => x.ActorRole).HasMaxLength(100);
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.Property(x => x.OccurredAt).IsRequired();
            entity.HasIndex(x => x.OccurredAt);
            entity.HasIndex(x => x.ServiceRequestId);
        });

        modelBuilder.Entity<PaymentTransaction>(entity =>
        {
            entity.ToTable("payment_transactions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).IsRequired();
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.Property(x => x.IdempotencyKey).HasMaxLength(200).IsRequired();
            entity.Property(x => x.GatewayToken).HasMaxLength(500);
            entity.Property(x => x.GatewayReference).HasMaxLength(200);
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.Property(x => x.UpdatedAt).IsRequired();
            entity.Property(x => x.VerifiedAt);
            entity.HasIndex(x => x.IdempotencyKey).IsUnique();
            entity.HasIndex(x => x.GatewayReference);
            entity.HasIndex(x => x.ServiceRequestId);
            entity.HasIndex(x => new { x.Status, x.UpdatedAt });
            entity.HasOne<ServiceRequest>()
                .WithMany()
                .HasForeignKey(x => x.ServiceRequestId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
