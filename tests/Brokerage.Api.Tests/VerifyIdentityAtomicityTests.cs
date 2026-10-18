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
