using Brokerage.Domain.Enums;

namespace Brokerage.Domain.Entities;

public sealed class PaymentTransaction
{
    public Guid Id { get; private set; }
    public Guid ServiceRequestId { get; private set; }
    public long Amount { get; private set; }
    public string Currency { get; private set; } = null!;
    public PaymentStatus Status { get; private set; }
    public string IdempotencyKey { get; private set; } = null!;
    public string? GatewayToken { get; private set; }
    public string? GatewayReference { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? VerifiedAt { get; private set; }

    private PaymentTransaction() { }

    public PaymentTransaction(
        Guid serviceRequestId,
        long amount,
        string currency,
        string idempotencyKey)
    {
        if (serviceRequestId == Guid.Empty)
            throw new ArgumentException("Service request ID cannot be empty.", nameof(serviceRequestId));
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Payment amount must be positive.");
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency cannot be empty.", nameof(currency));
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key cannot be empty.", nameof(idempotencyKey));

        Id = Guid.NewGuid();
        ServiceRequestId = serviceRequestId;
        Amount = amount;
        Currency = currency.Trim().ToUpperInvariant();
        IdempotencyKey = idempotencyKey.Trim();
        Status = PaymentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public void MarkGatewayCreated(string gatewayToken)
    {
        if (Status is PaymentStatus.Succeeded or PaymentStatus.Cancelled)
            throw new InvalidOperationException($"Payment cannot receive a gateway token in status {Status}.");
        if (string.IsNullOrWhiteSpace(gatewayToken))
            throw new ArgumentException("Gateway token cannot be empty.", nameof(gatewayToken));

        GatewayToken = gatewayToken;
        Touch();
    }

    public void MarkSucceeded(string gatewayReference, DateTimeOffset? verifiedAt = null)
    {
        if (string.IsNullOrWhiteSpace(gatewayReference))
            throw new ArgumentException("Gateway reference cannot be empty.", nameof(gatewayReference));
        if (Status is PaymentStatus.Failed or PaymentStatus.Cancelled)
            throw new InvalidOperationException($"A {Status.ToString().ToLowerInvariant()} payment cannot succeed.");

        GatewayReference = gatewayReference;
        Status = PaymentStatus.Succeeded;
        VerifiedAt = verifiedAt ?? DateTimeOffset.UtcNow;
        Touch();
    }

    public void MarkFailed()
    {
        if (Status == PaymentStatus.Succeeded)
            throw new InvalidOperationException("A succeeded payment cannot be marked failed.");

        Status = PaymentStatus.Failed;
        Touch();
    }

    public void MarkCancelled()
    {
        if (Status == PaymentStatus.Succeeded)
            throw new InvalidOperationException("A succeeded payment cannot be cancelled.");

        Status = PaymentStatus.Cancelled;
        Touch();
    }

    public void MarkVerifying()
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidOperationException($"Only pending payments can be verified; current status is {Status}.");

        Status = PaymentStatus.Verifying;
        Touch();
    }

    public void MarkVerificationUnavailable()
    {
        if (Status != PaymentStatus.Verifying)
            return;

        Status = PaymentStatus.Pending;
        Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
