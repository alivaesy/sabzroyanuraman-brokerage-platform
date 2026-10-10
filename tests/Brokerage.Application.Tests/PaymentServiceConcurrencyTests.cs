using Brokerage.Application.Contracts;
using Brokerage.Application.Models;
using Brokerage.Application.Services;
using Brokerage.Domain.Entities;
using Brokerage.Domain.Enums;

namespace Brokerage.Application.Tests;

public class PaymentServiceConcurrencyTests
{
    [Fact]
    public async Task VerifyAsync_ConcurrentCallbackDoesNotStartSecondGatewayVerification()
    {
        var serviceRequestId = Guid.NewGuid();
        var payment = new PaymentTransaction(serviceRequestId, 1000, "IRR", "concurrent-verify-key");
        payment.MarkGatewayCreated("gateway-token");

        var repository = new InMemoryPaymentRepository(payment);
        var gateway = new BlockingPaymentGateway();
        var service = new PaymentService(repository, gateway, new InMemoryAuditWriter());

        var firstAttempt = service.VerifyAsync(payment.Id, "gateway-token", "correlation-first");
        await gateway.VerifyStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var secondAttempt = await service.VerifyAsync(payment.Id, "gateway-token", "correlation-second");

        Assert.False(secondAttempt.Succeeded);
        Assert.Equal("PAYMENT_NOT_VERIFIABLE", secondAttempt.ErrorCode);
        Assert.Equal(1, gateway.VerifyCalls);

        gateway.Complete(new PaymentGatewayVerifyResult(true, 1000, "trace-concurrent-success"));
        var firstResult = await firstAttempt;

        Assert.True(firstResult.Succeeded);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal(1, gateway.VerifyCalls);
    }

    private sealed class InMemoryPaymentRepository(PaymentTransaction payment) : IPaymentTransactionRepository
    {
        public Task<PaymentTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<PaymentTransaction?>(id == payment.Id ? payment : null);

        public Task<PaymentTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<PaymentTransaction?>(payment.IdempotencyKey == idempotencyKey ? payment : null);

        public Task AddAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<PaymentTransaction> CreateOrGetByIdempotencyKeyAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default) =>
            Task.FromResult(transaction);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> TryBeginVerificationAsync(Guid paymentId, CancellationToken cancellationToken = default) =>
            Task.FromResult(paymentId == payment.Id && payment.Status == PaymentStatus.Pending);

        public Task<IReadOnlyList<PaymentTransaction>> GetStaleVerifyingAsync(
            DateTimeOffset updatedBefore, int limit = 100, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PaymentTransaction>>(Array.Empty<PaymentTransaction>());

        public Task<IReadOnlyList<PaymentTransaction>> GetStaleReconciliationCandidatesAsync(
            DateTimeOffset updatedBefore, int limit = 100, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PaymentTransaction>>(Array.Empty<PaymentTransaction>());
    }

    private sealed class BlockingPaymentGateway : IPaymentGateway
    {
        private readonly TaskCompletionSource<PaymentGatewayVerifyResult> _verifyResult =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _verifyCalls;

        public TaskCompletionSource<bool> VerifyStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int VerifyCalls => Volatile.Read(ref _verifyCalls);

        public Task<PaymentGatewayCreateResult> CreatePaymentAsync(
            PaymentGatewayCreateRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaymentGatewayCreateResult(true, "gateway-token"));

        public Task<PaymentGatewayVerifyResult> VerifyPaymentAsync(
            string gatewayToken, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _verifyCalls);
            VerifyStarted.TrySetResult(true);
            return _verifyResult.Task;
        }

        public void Complete(PaymentGatewayVerifyResult result) => _verifyResult.TrySetResult(result);
    }

    private sealed class InMemoryAuditWriter : IAuditEventWriter
    {
        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
