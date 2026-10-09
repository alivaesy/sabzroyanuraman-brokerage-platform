using Brokerage.Application.Contracts;
using Brokerage.Application.Models;
using Brokerage.Application.Services;
using Brokerage.Domain.Entities;

namespace Brokerage.Application.Tests;

public class PaymentServiceTests
{
    [Fact]
    public async Task CreateAsync_RejectsReusedIdempotencyKeyWithDifferentAmount()
    {
        var requestId = Guid.NewGuid();
        var existing = new PaymentTransaction(requestId, 1000, "IRR", "stable-key");
        existing.MarkGatewayCreated("gateway-token");
        var repository = new FakePaymentRepository(existing);
        var gateway = new FakePaymentGateway();
        var audit = new FakeAuditEventWriter();
        var service = new PaymentService(repository, gateway, audit);

        var result = await service.CreateAsync(requestId, 2000, "stable-key", "https://example.test/return", "correlation-1");

        Assert.False(result.Succeeded);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_REQUEST", result.ErrorCode);
        Assert.Equal(0, gateway.CreateCalls);
        Assert.Contains(audit.Events, item => item.EventType == "PaymentIdempotencyConflict");
        Assert.Equal(1, repository.Items.Count);
    }

    [Fact]
    public async Task CreateAsync_ReusesExistingPaymentWhenIdempotencyPayloadMatches()
    {
        var requestId = Guid.NewGuid();
        var existing = new PaymentTransaction(requestId, 1000, "IRR", "stable-key");
        existing.MarkGatewayCreated("gateway-token");
        var repository = new FakePaymentRepository(existing);
        var gateway = new FakePaymentGateway();
        var audit = new FakeAuditEventWriter();
        var service = new PaymentService(repository, gateway, audit);

        var result = await service.CreateAsync(requestId, 1000, "stable-key", "https://example.test/return", "correlation-2");

        Assert.True(result.AlreadyProcessed);
        Assert.Equal(existing.Id, result.Transaction.Id);
        Assert.Equal("gateway-token", result.GatewayToken);
        Assert.Equal(0, gateway.CreateCalls);
    }

    private sealed class FakePaymentRepository(PaymentTransaction existing) : IPaymentTransactionRepository
    {
        public List<PaymentTransaction> Items { get; } = [existing];

        public Task<PaymentTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.SingleOrDefault(x => x.Id == id));

        public Task<PaymentTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.SingleOrDefault(x => x.IdempotencyKey == idempotencyKey));

        public Task AddAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default)
        {
            Items.Add(transaction);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> TryBeginVerificationAsync(Guid paymentId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<PaymentTransaction>> GetStaleVerifyingAsync(DateTimeOffset updatedBefore, int limit = 100, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PaymentTransaction>>(Array.Empty<PaymentTransaction>());

        public Task<IReadOnlyList<PaymentTransaction>> GetStaleReconciliationCandidatesAsync(DateTimeOffset updatedBefore, int limit = 100, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PaymentTransaction>>(Array.Empty<PaymentTransaction>());
    }

    private sealed class FakePaymentGateway : IPaymentGateway
    {
        public int CreateCalls { get; private set; }

        public Task<PaymentGatewayCreateResult> CreatePaymentAsync(PaymentGatewayCreateRequest request, CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            return Task.FromResult(new PaymentGatewayCreateResult(true, "new-token"));
        }

        public Task<PaymentGatewayVerifyResult> VerifyPaymentAsync(string gatewayToken, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaymentGatewayVerifyResult(false, null, null, "NOT_IMPLEMENTED"));
    }

    private sealed class FakeAuditEventWriter : IAuditEventWriter
    {
        public List<AuditEvent> Events { get; } = [];

        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }
}
