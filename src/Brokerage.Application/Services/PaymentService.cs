using System.Security.Cryptography;
using System.Text;
using Brokerage.Application.Contracts;
using Brokerage.Application.Models;
using Brokerage.Domain.Entities;
using Brokerage.Domain.Enums;

namespace Brokerage.Application.Services;

public sealed class PaymentService(
    IPaymentTransactionRepository repository,
    IPaymentGateway gateway,
    IAuditEventWriter auditEventWriter)
{
    public async Task<PaymentCreateResult> CreateAsync(Guid serviceRequestId, long amount, string idempotencyKey, string returnUrl, string correlationId, CancellationToken cancellationToken = default)
    {
        var existing = await repository.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.ServiceRequestId != serviceRequestId ||
                existing.Amount != amount ||
                !string.Equals(existing.Currency, "IRR", StringComparison.Ordinal))
            {
                const string conflictCode = "IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_REQUEST";
                await WriteAuditAsync("PaymentIdempotencyConflict", correlationId, existing, existing.Status.ToString(), conflictCode, "Failure", cancellationToken);
                return PaymentCreateResult.Failed(existing, conflictCode);
            }

            await WriteAuditAsync("PaymentAlreadyProcessed", correlationId, existing, existing.Status.ToString(), "IdempotencyKeyReused", "Success", cancellationToken);
            return PaymentCreateResult.FromExisting(existing);
        }

        var transaction = new PaymentTransaction(serviceRequestId, amount, "IRR", idempotencyKey);
        await repository.AddAsync(transaction, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        PaymentGatewayCreateResult gatewayResult;
        try
        {
            gatewayResult = await gateway.CreatePaymentAsync(
                new PaymentGatewayCreateRequest(transaction.Id.ToString("N"), transaction.Amount, transaction.Currency, returnUrl),
                cancellationToken);
        }
        catch
        {
            await WriteAuditAsync("PaymentGatewayUnavailable", correlationId, transaction, PaymentStatus.Pending.ToString(), "CreateOutcomeUnknown", "ReviewRequired", cancellationToken);
            throw;
        }

        if (!gatewayResult.Succeeded || string.IsNullOrWhiteSpace(gatewayResult.GatewayToken))
        {
            transaction.MarkFailed();
            await repository.SaveChangesAsync(cancellationToken);
            await WriteAuditAsync("PaymentFailed", correlationId, transaction, PaymentStatus.Pending.ToString(), gatewayResult.ErrorCode ?? PaymentStatus.Failed.ToString(), "Failure", cancellationToken);
            return PaymentCreateResult.Failed(transaction, gatewayResult.ErrorCode);
        }

        transaction.MarkGatewayCreated(gatewayResult.GatewayToken);
        await repository.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync("PaymentCreated", correlationId, transaction, null, PaymentStatus.Pending.ToString(), "Success", cancellationToken);
        return PaymentCreateResult.Created(transaction, gatewayResult.GatewayToken);
    }

    public async Task<PaymentVerifyResult> VerifyAsync(Guid paymentId, string? callbackToken, string correlationId, CancellationToken cancellationToken = default)
    {
        var transaction = await repository.GetByIdAsync(paymentId, cancellationToken)
            ?? throw new InvalidOperationException("Payment transaction was not found.");

        if (transaction.Status == PaymentStatus.Succeeded)
            return PaymentVerifyResult.FromAlreadySucceeded(transaction);

        if (transaction.Status != PaymentStatus.Pending || string.IsNullOrWhiteSpace(transaction.GatewayToken))
            return PaymentVerifyResult.Failure(transaction, "PAYMENT_NOT_VERIFIABLE");

        // Callback verification must prove possession of the exact issued token. A missing
        // token is not equivalent to a trusted callback and must never reach the gateway.
        if (string.IsNullOrWhiteSpace(callbackToken) || !CryptographicEquals(callbackToken, transaction.GatewayToken))
            return PaymentVerifyResult.Failure(transaction, "CALLBACK_TOKEN_MISMATCH");

        if (!await repository.TryBeginVerificationAsync(paymentId, cancellationToken))
            return PaymentVerifyResult.Failure(transaction, "PAYMENT_VERIFICATION_IN_PROGRESS");

        transaction.MarkVerifying();
        await repository.SaveChangesAsync(cancellationToken);

        PaymentGatewayVerifyResult gatewayResult;
        try
        {
            gatewayResult = await gateway.VerifyPaymentAsync(transaction.GatewayToken, cancellationToken);
        }
        catch
        {
            // The provider may have processed Verify despite a timeout. Do not return to Pending,
            // because a later callback could repeat the external operation without reconciliation.
            transaction.MarkVerificationOutcomeUnknown();
            await repository.SaveChangesAsync(cancellationToken);
            await WriteAuditAsync("PaymentVerifyOutcomeUnknown", correlationId, transaction, PaymentStatus.Verifying.ToString(), PaymentStatus.ReconciliationRequired.ToString(), "ReviewRequired", cancellationToken);
            throw;
        }

        if (!gatewayResult.Succeeded || gatewayResult.Amount != transaction.Amount || string.IsNullOrWhiteSpace(gatewayResult.GatewayReference))
        {
            transaction.MarkReconciliationRequired();
            await repository.SaveChangesAsync(cancellationToken);
            var reason = gatewayResult.ErrorCode
                ?? (gatewayResult.Amount != transaction.Amount ? "VERIFY_AMOUNT_MISMATCH" : "VERIFY_REFERENCE_MISSING");
            await WriteAuditAsync(
                "PaymentReconciliationRequired",
                correlationId,
                transaction,
                PaymentStatus.Verifying.ToString(),
                $"{PaymentStatus.ReconciliationRequired}:{reason}",
                "ReviewRequired",
                cancellationToken);
            return PaymentVerifyResult.Failure(transaction, reason);
        }

        transaction.MarkSucceeded(gatewayResult.GatewayReference);
        await repository.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync("PaymentVerified", correlationId, transaction, PaymentStatus.Verifying.ToString(), PaymentStatus.Succeeded.ToString(), "Success", cancellationToken);
        return PaymentVerifyResult.Success(transaction);
    }

    private Task WriteAuditAsync(
        string eventType,
        string correlationId,
        PaymentTransaction transaction,
        string? previousState,
        string newState,
        string outcome,
        CancellationToken cancellationToken) =>
        auditEventWriter.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                eventType,
                correlationId,
                transaction.ServiceRequestId,
                null,
                outcome,
                previousState,
                newState),
            cancellationToken);

    private static bool CryptographicEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}

public sealed record PaymentCreateResult(PaymentTransaction Transaction, string? GatewayToken, bool AlreadyProcessed, bool Succeeded, string? ErrorCode)
{
    public static PaymentCreateResult Created(PaymentTransaction t, string token) => new(t, token, false, true, null);
    public static PaymentCreateResult FromExisting(PaymentTransaction t) => new(t, t.GatewayToken, true, t.Status is PaymentStatus.Succeeded or PaymentStatus.Pending, null);
    public static PaymentCreateResult Failed(PaymentTransaction t, string? error) => new(t, null, false, false, error);
}

public sealed record PaymentVerifyResult(PaymentTransaction Transaction, bool AlreadySucceeded, bool Succeeded, string? ErrorCode)
{
    public static PaymentVerifyResult Success(PaymentTransaction t) => new(t, false, true, null);
    public static PaymentVerifyResult FromAlreadySucceeded(PaymentTransaction t) => new(t, true, true, null);
    public static PaymentVerifyResult Failure(PaymentTransaction t, string error) => new(t, false, false, error);
}
