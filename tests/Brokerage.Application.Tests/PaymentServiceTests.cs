using Brokerage.Application.Contracts;
using Brokerage.Application.Models;
using Brokerage.Application.Services;
using Brokerage.Domain.Entities;
using Brokerage.Domain.Enums;

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
        var auditEvent = Assert.Single(audit.Events);
        Assert.Equal("PaymentIdempotencyConflict", auditEvent.EventType);
        Assert.Equal("Failure", auditEvent.Outcome);
        Assert.Equal(requestId, auditEvent.ServiceRequestId);
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
        Assert.Equal("Success", Assert.Single(audit.Events).Outcome);
    }

    [Fact]
    public async Task CreateAsync_AmbiguousGatewayResponseRemainsPendingAndIsNotRetriedAutomatically()
    {
        var requestId = Guid.NewGuid();
        var repository = new FakePaymentRepository();
        var gateway = new FakePaymentGateway
        {
            CreateResultOverride = new PaymentGatewayCreateResult(false, null, "HTTP_502")
        };
        var audit = new FakeAuditEventWriter();
        var service = new PaymentService(repository, gateway, audit);

        var first = await service.CreateAsync(requestId, 1000, "ambiguous-create-key", "https://example.test/return", "correlation-create-unknown");

        Assert.False(first.Succeeded);
        Assert.Equal("HTTP_502", first.ErrorCode);
        Assert.Equal(PaymentStatus.Pending, first.Transaction.Status);
        Assert.Null(first.Transaction.GatewayToken);
        Assert.Equal(1, gateway.CreateCalls);
        var auditEvent = Assert.Single(audit.Events);
        Assert.Equal("PaymentCreateOutcomeUnknown", auditEvent.EventType);
        Assert.Equal("ReviewRequired", auditEvent.Outcome);

        var second = await service.CreateAsync(requestId, 1000, "ambiguous-create-key", "https://example.test/return", "correlation-create-replay");

        Assert.False(second.Succeeded);
        Assert.True(second.AlreadyProcessed);
        Assert.Equal("PAYMENT_CREATE_OUTCOME_UNKNOWN", second.ErrorCode);
        Assert.Equal(1, gateway.CreateCalls);
        Assert.Single(repository.Items);
    }

    [Fact]
    public async Task VerifyAsync_RejectsMissingCallbackTokenWithoutCallingGateway()
    {
        var requestId = Guid.NewGuid();
        var existing = new PaymentTransaction(requestId, 1000, "IRR", "missing-callback-token-key");
        existing.MarkGatewayCreated("gateway-token");
        var repository = new FakePaymentRepository(existing);
        var gateway = new FakePaymentGateway();
        var service = new PaymentService(repository, gateway, new FakeAuditEventWriter());

        var result = await service.VerifyAsync(existing.Id, null, "correlation-missing-token");

        Assert.False(result.Succeeded);
        Assert.Equal("CALLBACK_TOKEN_MISMATCH", result.ErrorCode);
        Assert.Equal(0, gateway.VerifyCalls);
        Assert.Equal(PaymentStatus.Pending, existing.Status);
    }

    [Fact]
    public async Task VerifyAsync_RejectsMismatchedCallbackTokenWithoutCallingGateway()
    {
        var requestId = Guid.NewGuid();
        var existing = new PaymentTransaction(requestId, 1000, "IRR", "mismatch-callback-token-key");
        existing.MarkGatewayCreated("gateway-token");
        var repository = new FakePaymentRepository(existing);
        var gateway = new FakePaymentGateway();
        var service = new PaymentService(repository, gateway, new FakeAuditEventWriter());

        var result = await service.VerifyAsync(existing.Id, "different-token", "correlation-mismatch-token");

        Assert.False(result.Succeeded);
        Assert.Equal("CALLBACK_TOKEN_MISMATCH", result.ErrorCode);
        Assert.Equal(0, gateway.VerifyCalls);
        Assert.Equal(PaymentStatus.Pending, existing.Status);
    }

    [Fact]
    public async Task VerifyAsync_TransportFailureRequiresReconciliationAndBlocksAutomaticRetry()
    {
        var requestId = Guid.NewGuid();
        var existing = new PaymentTransaction(requestId, 1000, "IRR", "verify-timeout-key");
        existing.MarkGatewayCreated("gateway-token");
        var repository = new FakePaymentRepository(existing);
        var gateway = new FakePaymentGateway { ThrowOnVerify = true };
        var audit = new FakeAuditEventWriter();
        var service = new PaymentService(repository, gateway, audit);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.VerifyAsync(existing.Id, "gateway-token", "correlation-3"));

        Assert.Equal(PaymentStatus.ReconciliationRequired, existing.Status);
        Assert.Equal(1, gateway.VerifyCalls);
        var auditEvent = Assert.Single(audit.Events);
        Assert.Equal("PaymentVerifyOutcomeUnknown", auditEvent.EventType);
        Assert.Equal("ReviewRequired", auditEvent.Outcome);
        Assert.Equal(requestId, auditEvent.ServiceRequestId);

        var retry = await service.VerifyAsync(existing.Id, "gateway-token", "correlation-4");
        Assert.False(retry.Succeeded);
        Assert.Equal("PAYMENT_NOT_VERIFIABLE", retry.ErrorCode);
        Assert.Equal(1, gateway.VerifyCalls);
    }

    private sealed class FakePaymentRepository : IPaymentTransactionRepository
    {
        public List<PaymentTransaction> Items { get; } = [];

        public FakePaymentRepository(PaymentTransaction? existing = null)
        {
            if (existing is not null)
                Items.Add(existing);
        }

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

        public Task<bool> TryBeginVerificationAsync(Guid paymentId, CancellationToken cancellationToken = default)
        {
            var payment = Items.SingleOrDefault(x => x.Id == paymentId);
            if (payment is null || payment.Status != PaymentStatus.Pending)
                return Task.FromResult(false);
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<PaymentTransaction>> GetStaleVerifyingAsync(DateTimeOffset updatedBefore, int limit = 100, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PaymentTransaction>>(Array.Empty<PaymentTransaction>());

        public Task<IReadOnlyList<PaymentTransaction>> GetStaleReconciliationCandidatesAsync(DateTimeOffset updatedBefore, int limit = 100, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PaymentTransaction>>(Array.Empty<PaymentTransaction>());
    }

    private sealed class FakePaymentGateway : IPaymentGateway
    {
        public int CreateCalls { get; private set; }
        public int VerifyCalls { get; private set; }
        public bool ThrowOnVerify { get; init; }
        public PaymentGatewayCreateResult? CreateResultOverride { get; init; }

        public Task<PaymentGatewayCreateResult> CreatePaymentAsync(PaymentGatewayCreateRequest request, CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            return Task.FromResult(CreateResultOverride ?? new PaymentGatewayCreateResult(true, "new-token"));
        }

        public Task<PaymentGatewayVerifyResult> VerifyPaymentAsync(string gatewayToken, CancellationToken cancellationToken = default)
        {
            VerifyCalls++;
            if (ThrowOnVerify)
                throw new HttpRequestException("Simulated gateway timeout.");
            return Task.FromResult(new PaymentGatewayVerifyResult(false, null, null, "NOT_IMPLEMENTED"));
        }
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
