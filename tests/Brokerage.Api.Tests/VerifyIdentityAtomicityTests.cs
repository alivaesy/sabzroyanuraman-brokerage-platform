using Brokerage.Application.Contracts;
using Brokerage.Application.Integration;
using Brokerage.Application.Models;
using Brokerage.Application.UseCases;
using Brokerage.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brokerage.Api.Tests;

public sealed class VerifyIdentityAtomicityTests
{
    [Fact]
    public async Task VerifyIdentity_AuditFailure_RollsBackIdentityState()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<BrokerageDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setup = new BrokerageDbContext(options))
            await setup.Database.MigrateAsync();

        await using var db = new BrokerageDbContext(options);
        var useCase = new VerifyIdentity(
            new AcceptedIdentityVerificationService(),
            new IdentityVerificationStateRepository(db),
            new EfUnitOfWork(db),
            new FailingAuditEventWriter());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            useCase.ExecuteAsync("atomic-user", "test-identifier"));

        Assert.False(await db.IdentityVerificationStates
            .AsNoTracking()
            .AnyAsync(x => x.UserId == "atomic-user"));
    }

    [Fact]
    public async Task VerifyIdentity_Success_CommitsStateAndAuditTogether()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<BrokerageDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setup = new BrokerageDbContext(options))
            await setup.Database.MigrateAsync();

        await using var db = new BrokerageDbContext(options);
        var useCase = new VerifyIdentity(
            new AcceptedIdentityVerificationService(),
            new IdentityVerificationStateRepository(db),
            new EfUnitOfWork(db),
            new AuditEventWriter(db, Microsoft.Extensions.Logging.Abstractions.NullLogger<AuditEventWriter>.Instance));

        var result = await useCase.ExecuteAsync("atomic-success-user", "test-identifier", "test-correlation");

        Assert.True(result.Verified);
        Assert.True(await db.IdentityVerificationStates.AsNoTracking()
            .AnyAsync(x => x.UserId == "atomic-success-user" && x.IsVerified));
        Assert.True(await db.AuditEvents.AsNoTracking()
            .AnyAsync(x => x.ActorUserId == "atomic-success-user" && x.EventType == "IdentityVerification"
                && x.CorrelationId == "test-correlation" && x.Outcome == "Success"));
    }

    [Fact]
    public async Task VerifyIdentity_RejectedIdentity_CommitsFailureStateAndAuditWithoutIdentifier()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<BrokerageDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setup = new BrokerageDbContext(options))
            await setup.Database.MigrateAsync();

        await using var db = new BrokerageDbContext(options);
        var useCase = new VerifyIdentity(
            new RejectedIdentityVerificationService(),
            new IdentityVerificationStateRepository(db),
            new EfUnitOfWork(db),
            new AuditEventWriter(db, Microsoft.Extensions.Logging.Abstractions.NullLogger<AuditEventWriter>.Instance));

        const string nationalIdentifier = "TEST-IDENTIFIER-MUST-NOT-PERSIST";
        var result = await useCase.ExecuteAsync("rejected-user", nationalIdentifier, "rejected-correlation");

        Assert.False(result.Verified);
        Assert.True(await db.IdentityVerificationStates.AsNoTracking()
            .AnyAsync(x => x.UserId == "rejected-user" && !x.IsVerified));
        var audit = await db.AuditEvents.AsNoTracking()
            .SingleAsync(x => x.ActorUserId == "rejected-user" && x.EventType == "IdentityVerification");
        Assert.Equal("Failure", audit.Outcome);
        Assert.DoesNotContain(nationalIdentifier, audit.NewState ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(nationalIdentifier, audit.PreviousState ?? string.Empty, StringComparison.Ordinal);
    }

    private sealed class RejectedIdentityVerificationService : IIdentityVerificationService
    {
        public Task<bool> VerifyAsync(string nationalIdentifier, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class AcceptedIdentityVerificationService : IIdentityVerificationService
    {
        public Task<bool> VerifyAsync(string nationalIdentifier, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class FailingAuditEventWriter : IAuditEventWriter
    {
        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated audit persistence failure.");
    }
}
