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
        var persistedTransaction = await repository.CreateOrGetByIdempotencyKeyAsync(transaction, cancellationToken);

        // The unique database constraint is the final arbiter when two requests with the
        // same idempotency key arrive concurrently. Only the request that inserted the row
        // may contact Sadad; the loser reuses the persisted result without a second Create.
        if (persistedTransaction.Id != transaction.Id)
        {
            if (persistedTransaction.ServiceRequestId != serviceRequestId ||
                persistedTransaction.Amount != amount ||
                !string.Equals(persistedTransaction.Currency, "IRR", StringComparison.Ordinal))
            {
                const string conflictCode = "IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_REQUEST";
                await WriteAuditAsync("PaymentIdempotencyConflict", correlationId, persistedTransaction, persistedTransaction.Status.ToString(), conflictCode, "Failure", cancellationToken);
                return PaymentCreateResult.Failed(persistedTransaction, conflictCode);
            }

            await WriteAuditAsync("PaymentAlreadyProcessed", correlationId, persistedTransaction, persistedTransaction.Status.ToString(), "IdempotencyKeyConcurrentReplay", "Success", cancellationToken);
            return PaymentCreateResult.FromExisting(persistedTransaction);
        }

        PaymentGatewayCreateResult gatewayResult;
        try
        {
            gatewayResult = await gateway.CreatePaymentAsync(
                new PaymentGatewayCreateRequest(transaction.Id.ToString("N"), transaction.Amount, transaction.Currency, returnUrl),
                cancellationToken);
        }
        catch
        {
            await WriteAuditAsync("PaymentGatewayUnavailable", correlationId, transaction, PaymentStatus.Pending.ToString(), "CreateOutcomeUnknown", "ReviewRequired", CancellationToken.None);
            throw;
        }

        if (!gatewayResult.Succeeded || string.IsNullOrWhiteSpace(gatewayResult.GatewayToken))
        {
            if (IsAmbiguousCreateOutcome(gatewayResult.ErrorCode))
            {
                // HTTP failures and malformed/missing gateway responses do not prove that
                // Sadad rejected the order. Keep the record pending for manual reconciliation.
                await WriteAuditAsync("PaymentCreateOutcomeUnknown", correlationId, transaction, PaymentStatus.Pending.ToString(), "CreateOutcomeUnknown", "ReviewRequired", CancellationToken.None);
                return PaymentCreateResult.RequiresReconciliation(transaction, gatewayResult.ErrorCode ?? "CREATE_OUTCOME_UNKNOWN");
            }

            transaction.MarkFailed();
            await repository.SaveChangesAsync(CancellationToken.None);
            await WriteAuditAsync("PaymentFailed", correlationId, transaction, PaymentStatus.Pending.ToString(), gatewayResult.ErrorCode ?? PaymentStatus.Failed.ToString(), "Failure", CancellationToken.None);
            return PaymentCreateResult.Failed(transaction, gatewayResult.ErrorCode);
        }

        transaction.MarkGatewayCreated(gatewayResult.GatewayToken);
        // Once Sadad has returned a token, persist it even if the browser disconnects;
        // otherwise the provider-side order could exist without a locally stored token.
        await repository.SaveChangesAsync(CancellationToken.None);
        await WriteAuditAsync("PaymentCreated", correlationId, transaction, null, PaymentStatus.Pending.ToString(), "Success", CancellationToken.None);
        return PaymentCreateResult.Created(transaction, gatewayResult.GatewayToken);
    }

    public async Task<PaymentVerifyResult> VerifyAsync(Guid paymentId, string? callbackToken, string correlationId, CancellationToken cancellationToken = default)
    {
        var transaction = await repository.GetByIdAsync(paymentId, cancellationToken)
            ?? throw new PaymentTransactionNotFoundException();

        // Callback verification must prove possession of the exact issued token even when
        // the payment already succeeded; otherwise an arbitrary caller could probe payment state.
        if (string.IsNullOrWhiteSpace(callbackToken) ||
            string.IsNullOrWhiteSpace(transaction.GatewayToken) ||
            !CryptographicEquals(callbackToken, transaction.GatewayToken))
            return PaymentVerifyResult.Failure(transaction, "CALLBACK_TOKEN_MISMATCH");

        if (transaction.Status == PaymentStatus.Succeeded)
            return PaymentVerifyResult.FromAlreadySucceeded(transaction);

        if (transaction.Status != PaymentStatus.Pending)
            return PaymentVerifyResult.Failure(transaction, "PAYMENT_NOT_VERIFIABLE");

        if (!await repository.TryBeginVerificationAsync(paymentId, cancellationToken))
            return PaymentVerifyResult.Failure(transaction, "PAYMENT_VERIFICATION_IN_PROGRESS");

        transaction.MarkVerifying();
        // Once verification is claimed, persist the state transition even if the browser disconnects.
        // TryBeginVerificationAsync has already atomically moved the database row to Verifying.
        await repository.SaveChangesAsync(CancellationToken.None);

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
            // The request token may be cancelled precisely because the provider timed out or
            // the browser disconnected. Use an independent token for the durable safety state
            // and audit event; otherwise the payment could remain Verifying indefinitely.
            await repository.SaveChangesAsync(CancellationToken.None);
            await WriteAuditAsync("PaymentVerifyOutcomeUnknown", correlationId, transaction, PaymentStatus.Verifying.ToString(), PaymentStatus.ReconciliationRequired.ToString(), "ReviewRequired", CancellationToken.None);
            throw;
        }

        if (!gatewayResult.Succeeded || gatewayResult.Amount != transaction.Amount || string.IsNullOrWhiteSpace(gatewayResult.GatewayReference) ||
            !string.Equals(gatewayResult.OrderId, transaction.Id.ToString("N"), StringComparison.OrdinalIgnoreCase))
        {
            transaction.MarkReconciliationRequired();
            await repository.SaveChangesAsync(cancellationToken);
            var reason = gatewayResult.ErrorCode
                ?? (!gatewayResult.Succeeded
                    ? "VERIFY_REJECTED"
                    : gatewayResult.Amount != transaction.Amount
                        ? "VERIFY_AMOUNT_MISMATCH"
                        : string.IsNullOrWhiteSpace(gatewayResult.GatewayReference)
                            ? "VERIFY_REFERENCE_MISSING"
                            : "VERIFY_ORDER_MISMATCH");
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

    private static bool IsAmbiguousCreateOutcome(string? errorCode)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
            return false;

        // Provider response classification is intentionally conservative: a truncated or
        // oversized body cannot establish that Sadad rejected the order. Normalize the
        // code defensively so casing/outer whitespace cannot turn an unknown outcome into
        // a terminal Failed transaction and invite a duplicate payment attempt.
        var normalizedCode = errorCode.Trim();

        return normalizedCode.StartsWith("HTTP_", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalizedCode, "INVALID_GATEWAY_RESPONSE", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalizedCode, "GATEWAY_RESPONSE_TOO_LARGE", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalizedCode, "SADAD_TOKEN_MISSING", StringComparison.OrdinalIgnoreCase);
    }

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
    public static PaymentCreateResult FromExisting(PaymentTransaction t) =>
        t.Status == PaymentStatus.Pending && string.IsNullOrWhiteSpace(t.GatewayToken)
            ? RequiresReconciliation(t, "PAYMENT_CREATE_OUTCOME_UNKNOWN")
            : new(t, t.GatewayToken, true, t.Status is PaymentStatus.Succeeded or PaymentStatus.Pending, null);
    public static PaymentCreateResult RequiresReconciliation(PaymentTransaction t, string error) => new(t, null, true, false, error);
    public static PaymentCreateResult Failed(PaymentTransaction t, string? error) => new(t, null, false, false, error);
}

public sealed record PaymentVerifyResult(PaymentTransaction Transaction, bool AlreadySucceeded, bool Succeeded, string? ErrorCode)
{
    public static PaymentVerifyResult Success(PaymentTransaction t) => new(t, false, true, null);
    public static PaymentVerifyResult FromAlreadySucceeded(PaymentTransaction t) => new(t, true, true, null);
    public static PaymentVerifyResult Failure(PaymentTransaction t, string error) => new(t, false, false, error);
}


public sealed class PaymentTransactionNotFoundException() : Exception("Payment transaction was not found.");
