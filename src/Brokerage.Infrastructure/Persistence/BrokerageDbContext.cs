using Brokerage.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Brokerage.Infrastructure.Persistence;

public sealed class BrokerageDbContext(DbContextOptions<BrokerageDbContext> options) : DbContext(options)
{
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<WorkflowStage> WorkflowStages => Set<WorkflowStage>();
    public DbSet<Expert> Experts => Set<Expert>();
    public DbSet<IdentityVerificationState> IdentityVerificationStates => Set<IdentityVerificationState>();

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
    }
}
