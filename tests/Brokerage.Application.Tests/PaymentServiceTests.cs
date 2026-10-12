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
            Task.FromResult(Transaction?.Id == paymentId && Transaction.Status == PaymentStatus.Pending);

        public Task<IReadOnlyList<PaymentTransaction>> GetStaleVerifyingAsync(
            DateTimeOffset updatedBefore, int limit = 100, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PaymentTransaction>>(Array.Empty<PaymentTransaction>());

        public Task<IReadOnlyList<PaymentTransaction>> GetStaleReconciliationCandidatesAsync(
            DateTimeOffset updatedBefore, int limit = 100, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PaymentTransaction>>(Array.Empty<PaymentTransaction>());
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
