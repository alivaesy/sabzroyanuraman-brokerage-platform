using Brokerage.Application.Contracts;
using Brokerage.Application.Models;
using Brokerage.Application.Services;
using Brokerage.Domain.Entities;
using Brokerage.Domain.Enums;

namespace Brokerage.Application.Tests;

public class PaymentServiceTests
{
    [Fact]
    public async Task CreateAsync_PersistsSadadTokenWhenCallerCancelsAfterGatewayReturns()
    {
        using var cancellation = new CancellationTokenSource();
        var repository = new CapturingPaymentRepository();
        var gateway = new CancellingSuccessfulGateway(cancellation);
        var audit = new CapturingAuditWriter();
        var service = new PaymentService(repository, gateway, audit);

        var result = await service.CreateAsync(
            Guid.NewGuid(),
            100_000,
            "create-cancellation-test",
            "https://example.test/payments/sadad/callback",
            "test-correlation",
            cancellation.Token);

        Assert.True(cancellation.IsCancellationRequested);
        Assert.True(result.Succeeded);
        Assert.Equal("sandbox-token", result.GatewayToken);
        Assert.Equal("sandbox-token", repository.Transaction?.GatewayToken);
        Assert.Contains(audit.Events, item => item.EventType == "PaymentCreated");
        Assert.All(repository.SaveTokens, token => Assert.False(token.IsCancellationRequested));
        Assert.All(audit.Tokens, token => Assert.False(token.IsCancellationRequested));
    }

    [Fact]
    public async Task VerifyAsync_PersistsVerifiedSuccessWhenCallerCancelsAfterGatewayReturns()
    {
        using var cancellation = new CancellationTokenSource();
        var payment = new PaymentTransaction(Guid.NewGuid(), 100_000, "IRR", "verify-cancellation-test");
        payment.MarkGatewayCreated("sandbox-token");
        var repository = new CapturingPaymentRepository(payment);
        var gateway = new CancellingSuccessfulVerifyGateway(cancellation, payment.Id);
        var audit = new CapturingAuditWriter();
        var service = new PaymentService(repository, gateway, audit);

        var result = await service.VerifyAsync(payment.Id, "sandbox-token", "verify-correlation", cancellation.Token);

        Assert.True(cancellation.IsCancellationRequested);
        Assert.True(result.Succeeded);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal("sandbox-reference", payment.GatewayReference);
        Assert.Contains(audit.Events, item => item.EventType == "PaymentVerified");
        Assert.All(repository.SaveTokens, token => Assert.False(token.IsCancellationRequested));
        Assert.All(audit.Tokens, token => Assert.False(token.IsCancellationRequested));
    }

    [Fact]
    public async Task CreateAsync_ReplaysExistingIdempotencyKeyWithoutCallingGateway()
    {
        var payment = new PaymentTransaction(Guid.NewGuid(), 100_000, "IRR", "replay-test");
        payment.MarkGatewayCreated("persisted-token");
        var repository = new CapturingPaymentRepository(payment);
        var gateway = new CountingGateway(new PaymentGatewayCreateResult(true, "unexpected-token"));
        var service = new PaymentService(repository, gateway, new CapturingAuditWriter());

        var result = await service.CreateAsync(
            payment.ServiceRequestId, payment.Amount, payment.IdempotencyKey,
            "https://example.test/payments/sadad/callback", "replay-correlation");

        Assert.True(result.AlreadyProcessed);
        Assert.True(result.Succeeded);
        Assert.Equal("persisted-token", result.GatewayToken);
        Assert.Equal(0, gateway.CreateCalls);
    }

    [Fact]
    public async Task CreateAsync_RejectsIdempotencyKeyReusedWithDifferentAmount()
    {
        var payment = new PaymentTransaction(Guid.NewGuid(), 100_000, "IRR", "conflict-test");
        var repository = new CapturingPaymentRepository(payment);
        var gateway = new CountingGateway(new PaymentGatewayCreateResult(true, "unexpected-token"));
        var service = new PaymentService(repository, gateway, new CapturingAuditWriter());

        var result = await service.CreateAsync(
            payment.ServiceRequestId, 200_000, payment.IdempotencyKey,
            "https://example.test/payments/sadad/callback", "conflict-correlation");

        Assert.False(result.Succeeded);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_REQUEST", result.ErrorCode);
        Assert.Equal(0, gateway.CreateCalls);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    [Fact]
    public async Task CreateAsync_DoesNotRepeatGatewayCreateAfterAmbiguousOutcome()
    {
        var repository = new CapturingPaymentRepository();
        var gateway = new CountingGateway(new PaymentGatewayCreateResult(false, null, "HTTP_504"));
        var service = new PaymentService(repository, gateway, new CapturingAuditWriter());
        var serviceRequestId = Guid.NewGuid();

        var first = await service.CreateAsync(
            serviceRequestId, 100_000, "ambiguous-create-test",
            "https://example.test/payments/sadad/callback", "first-correlation");
        var replay = await service.CreateAsync(
            serviceRequestId, 100_000, "ambiguous-create-test",
            "https://example.test/payments/sadad/callback", "replay-correlation");

        Assert.False(first.Succeeded);
        Assert.Equal("HTTP_504", first.ErrorCode);
        Assert.Equal(PaymentStatus.Pending, first.Transaction.Status);
        Assert.False(replay.Succeeded);
        Assert.True(replay.AlreadyProcessed);
        Assert.Equal("PAYMENT_CREATE_OUTCOME_UNKNOWN", replay.ErrorCode);
        Assert.Equal(1, gateway.CreateCalls);
    }


    [Fact]
    public async Task VerifyAsync_PersistsReconciliationWhenCallerCancelsAfterMismatchedGatewayResult()
    {
        using var cancellation = new CancellationTokenSource();
        var payment = new PaymentTransaction(Guid.NewGuid(), 100_000, "IRR", "verify-mismatch-cancel-test");
        payment.MarkGatewayCreated("sandbox-token");
        var repository = new CapturingPaymentRepository(payment);
        var gateway = new CancellingMismatchedVerifyGateway(cancellation, payment.Id);
        var audit = new CapturingAuditWriter();
        var service = new PaymentService(repository, gateway, audit);

        var result = await service.VerifyAsync(payment.Id, "sandbox-token", "verify-mismatch-correlation", cancellation.Token);

        Assert.True(cancellation.IsCancellationRequested);
        Assert.False(result.Succeeded);
        Assert.Equal("VERIFY_AMOUNT_MISMATCH", result.ErrorCode);
        Assert.Equal(PaymentStatus.ReconciliationRequired, payment.Status);
        Assert.Contains(audit.Events, item => item.EventType == "PaymentReconciliationRequired");
        Assert.All(repository.SaveTokens, token => Assert.False(token.IsCancellationRequested));
        Assert.All(audit.Tokens, token => Assert.False(token.IsCancellationRequested));
    }

    [Fact]
    public async Task VerifyAsync_PersistsUnknownOutcomeWhenGatewayThrowsAfterCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var payment = new PaymentTransaction(Guid.NewGuid(), 100_000, "IRR", "verify-unknown-cancel-test");
        payment.MarkGatewayCreated("sandbox-token");
        var repository = new CapturingPaymentRepository(payment);
        var gateway = new CancellingThrowingVerifyGateway(cancellation);
        var audit = new CapturingAuditWriter();
        var service = new PaymentService(repository, gateway, audit);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.VerifyAsync(payment.Id, "sandbox-token", "verify-unknown-correlation", cancellation.Token));

        Assert.True(cancellation.IsCancellationRequested);
        Assert.Equal(PaymentStatus.ReconciliationRequired, payment.Status);
        Assert.Contains(audit.Events, item => item.EventType == "PaymentVerifyOutcomeUnknown");
        Assert.All(repository.SaveTokens, token => Assert.False(token.IsCancellationRequested));
        Assert.All(audit.Tokens, token => Assert.False(token.IsCancellationRequested));
    }

    private sealed class CapturingPaymentRepository : IPaymentTransactionRepository
    {
        public CapturingPaymentRepository(PaymentTransaction? transaction = null) => Transaction = transaction;

        public PaymentTransaction? Transaction { get; private set; }
        public List<CancellationToken> SaveTokens { get; } = new();

        public Task<PaymentTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Transaction?.Id == id ? Transaction : null);

        public Task<PaymentTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(Transaction?.IdempotencyKey == idempotencyKey ? Transaction : null);

        public Task AddAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default)
        {
            Transaction = transaction;
            return Task.CompletedTask;
        }

        public Task<PaymentTransaction> CreateOrGetByIdempotencyKeyAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default)
        {
            Transaction = transaction;
            return Task.FromResult(transaction);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveTokens.Add(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task<bool> TryBeginVerificationAsync(Guid paymentId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Transaction is { } current && current.Id == paymentId && current.Status == PaymentStatus.Pending);

        public Task<IReadOnlyList<PaymentTransaction>> GetStaleVerifyingAsync(
            DateTimeOffset updatedBefore, int limit = 100, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PaymentTransaction>>(Array.Empty<PaymentTransaction>());

        public Task<IReadOnlyList<PaymentTransaction>> GetStaleReconciliationCandidatesAsync(
            DateTimeOffset updatedBefore, int limit = 100, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PaymentTransaction>>(Array.Empty<PaymentTransaction>());
    }

    private sealed class CountingGateway(PaymentGatewayCreateResult createResult) : IPaymentGateway
    {
        public int CreateCalls { get; private set; }

        public Task<PaymentGatewayCreateResult> CreatePaymentAsync(
            PaymentGatewayCreateRequest request, CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            return Task.FromResult(createResult);
        }

        public Task<PaymentGatewayVerifyResult> VerifyPaymentAsync(
            string gatewayToken, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaymentGatewayVerifyResult(false, null, null, "NOT_USED"));
    }

    private sealed class CancellingSuccessfulGateway(CancellationTokenSource cancellation) : IPaymentGateway
    {
        public Task<PaymentGatewayCreateResult> CreatePaymentAsync(
            PaymentGatewayCreateRequest request, CancellationToken cancellationToken = default)
        {
            cancellation.Cancel();
            return Task.FromResult(new PaymentGatewayCreateResult(true, "sandbox-token"));
        }

        public Task<PaymentGatewayVerifyResult> VerifyPaymentAsync(
            string gatewayToken, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaymentGatewayVerifyResult(false, null, null, "NOT_USED"));
    }

    private sealed class CancellingSuccessfulVerifyGateway(CancellationTokenSource cancellation, Guid paymentId) : IPaymentGateway
    {
        public Task<PaymentGatewayCreateResult> CreatePaymentAsync(
            PaymentGatewayCreateRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaymentGatewayCreateResult(true, "unused"));

        public Task<PaymentGatewayVerifyResult> VerifyPaymentAsync(
            string gatewayToken, CancellationToken cancellationToken = default)
        {
            cancellation.Cancel();
            return Task.FromResult(new PaymentGatewayVerifyResult(
                true, 100_000, "sandbox-reference", null, paymentId.ToString("N")));
        }
    }


    private sealed class CancellingMismatchedVerifyGateway(CancellationTokenSource cancellation, Guid paymentId) : IPaymentGateway
    {
        public Task<PaymentGatewayCreateResult> CreatePaymentAsync(
            PaymentGatewayCreateRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaymentGatewayCreateResult(true, "unused"));

        public Task<PaymentGatewayVerifyResult> VerifyPaymentAsync(
            string gatewayToken, CancellationToken cancellationToken = default)
        {
            cancellation.Cancel();
            return Task.FromResult(new PaymentGatewayVerifyResult(
                true, 99_000, "sandbox-reference", null, paymentId.ToString("N")));
        }
    }

    private sealed class CancellingThrowingVerifyGateway(CancellationTokenSource cancellation) : IPaymentGateway
    {
        public Task<PaymentGatewayCreateResult> CreatePaymentAsync(
            PaymentGatewayCreateRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaymentGatewayCreateResult(true, "unused"));

        public Task<PaymentGatewayVerifyResult> VerifyPaymentAsync(
            string gatewayToken, CancellationToken cancellationToken = default)
        {
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        }
    }

    private sealed class CapturingAuditWriter : IAuditEventWriter
    {
        public List<AuditEvent> Events { get; } = new();
        public List<CancellationToken> Tokens { get; } = new();

        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Tokens.Add(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }
}
