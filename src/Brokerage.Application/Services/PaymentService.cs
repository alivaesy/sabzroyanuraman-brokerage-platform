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
    public async Task<PaymentCreateResult> CreateAsync(
        Guid serviceRequestId,
        long amount,
        string idempotencyKey,
        string returnUrl,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var existing = await repository.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            await WriteAuditAsync(
                "PaymentAlreadyProcessed",
                correlationId,
                existing.Id,
                existing.Status.ToString(),
                "IdempotencyKeyReused",
                cancellationToken);

            return PaymentCreateResult.FromExisting(existing);
        }

        var transaction = new PaymentTransaction(serviceRequestId, amount, "IRR", idempotencyKey);
        await repository.AddAsync(transaction, cancellationToken);

        PaymentGatewayCreateResult gatewayResult;
        try
        {
            gatewayResult = await gateway.CreatePaymentAsync(
                new PaymentGatewayCreateRequest(
                    transaction.Id.ToString("N"),
                    transaction.Amount,
                    transaction.Currency,
                    returnUrl),
                cancellationToken);
        }
        catch
        {
            transaction.MarkFailed();
            await repository.SaveChangesAsync(cancellationToken);
            await WriteAuditAsync(
                "PaymentFailed",
                correlationId,
                transaction.Id,
                PaymentStatus.Pending.ToString(),
                PaymentStatus.Failed.ToString(),
                cancellationToken);
            throw;
        }

        if (!gatewayResult.Succeeded || string.IsNullOrWhiteSpace(gatewayResult.GatewayToken))
        {
            transaction.MarkFailed();
            await repository.SaveChangesAsync(cancellationToken);
            await WriteAuditAsync(
                "PaymentFailed",
                correlationId,
                transaction.Id,
                PaymentStatus.Pending.ToString(),
                gatewayResult.ErrorCode ?? PaymentStatus.Failed.ToString(),
                cancellationToken);

            return PaymentCreateResult.Failed(transaction, gatewayResult.ErrorCode);
        }

        transaction.MarkGatewayCreated(gatewayResult.GatewayToken);
        await repository.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            "PaymentCreated",
            correlationId,
            transaction.Id,
            null,
            PaymentStatus.Pending.ToString(),
            cancellationToken);

        return PaymentCreateResult.Created(transaction, gatewayResult.GatewayToken);
    }

    public async Task<PaymentVerifyResult> VerifyAsync(
        Guid paymentId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var transaction = await repository.GetByIdAsync(paymentId, cancellationToken)
            ?? throw new InvalidOperationException("Payment transaction was not found.");

        if (transaction.Status == PaymentStatus.Succeeded)
            return PaymentVerifyResult.FromAlreadySucceeded(transaction);

        if (transaction.Status != PaymentStatus.Pending || string.IsNullOrWhiteSpace(transaction.GatewayToken))
            return PaymentVerifyResult.Failure(transaction, "PAYMENT_NOT_VERIFIABLE");

        transaction.MarkVerifying();

        PaymentGatewayVerifyResult gatewayResult;
        try
        {
            gatewayResult = await gateway.VerifyPaymentAsync(transaction.GatewayToken, cancellationToken);
        }
        catch
        {
            await repository.SaveChangesAsync(cancellationToken);
            await WriteAuditAsync(
                "PaymentFailed",
                correlationId,
                transaction.Id,
                PaymentStatus.Pending.ToString(),
                "VERIFY_EXCEPTION",
                cancellationToken);
            throw;
        }

        if (!gatewayResult.Succeeded ||
            gatewayResult.Amount != transaction.Amount ||
            string.IsNullOrWhiteSpace(gatewayResult.GatewayReference))
        {
            transaction.MarkFailed();
            await repository.SaveChangesAsync(cancellationToken);

            await WriteAuditAsync(
                "PaymentFailed",
                correlationId,
                transaction.Id,
                PaymentStatus.Pending.ToString(),
                gatewayResult.ErrorCode ?? "VERIFY_REJECTED",
                cancellationToken);

            return PaymentVerifyResult.Failure(transaction, gatewayResult.ErrorCode ?? "VERIFY_REJECTED");
        }

        transaction.MarkSucceeded(gatewayResult.GatewayReference);
        await repository.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            "PaymentVerified",
            correlationId,
            transaction.Id,
            PaymentStatus.Pending.ToString(),
            PaymentStatus.Succeeded.ToString(),
            cancellationToken);

        return PaymentVerifyResult.Success(transaction);
    }

    private Task WriteAuditAsync(
        string eventType,
        string correlationId,
        Guid paymentId,
        string? previousState,
        string newState,
        CancellationToken cancellationToken)
    {
        return auditEventWriter.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                eventType,
                correlationId,
                null,
                null,
                "Success",
                previousState,
                newState,
                null,
                null,
                null),
            cancellationToken);
    }
}

public sealed record PaymentCreateResult(
    PaymentTransaction Transaction,
    string? GatewayToken,
    bool AlreadyProcessed,
    bool Succeeded,
    string? ErrorCode)
{
    public static PaymentCreateResult Created(PaymentTransaction t, string token) =>
        new(t, token, false, true, null);

    public static PaymentCreateResult FromExisting(PaymentTransaction t) =>
        new(t, t.GatewayToken, true, t.Status == PaymentStatus.Succeeded || t.Status == PaymentStatus.Pending, null);

    public static PaymentCreateResult Failed(PaymentTransaction t, string? error) =>
        new(t, null, false, false, error);
}

public sealed record PaymentVerifyResult(
    PaymentTransaction Transaction,
    bool AlreadySucceeded,
    bool Succeeded,
    string? ErrorCode)
{
    public static PaymentVerifyResult Success(PaymentTransaction t) =>
        new(t, false, true, null);

    public static PaymentVerifyResult FromAlreadySucceeded(PaymentTransaction t) =>
        new(t, true, true, null);

    public static PaymentVerifyResult Failure(PaymentTransaction t, string error) =>
        new(t, false, false, error);
}
