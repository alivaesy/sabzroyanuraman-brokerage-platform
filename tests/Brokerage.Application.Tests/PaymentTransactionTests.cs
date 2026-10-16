using Brokerage.Domain.Entities;
using Brokerage.Domain.Enums;

namespace Brokerage.Application.Tests;

public class PaymentTransactionTests
{
    [Fact]
    public void NewPayment_StartsPendingAndUsesIrCurrency()
    {
        var requestId = Guid.NewGuid();
        var payment = new PaymentTransaction(requestId, 1500000, "irr", "idem-001");
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal("IRR", payment.Currency);
        Assert.Equal(1500000, payment.Amount);
        Assert.Equal(requestId, payment.ServiceRequestId);
    }

    [Fact]
    public void DuplicateIdempotencyKey_IsRepresentedAsStableDomainIdentity()
    {
        var payment = new PaymentTransaction(Guid.NewGuid(), 1000, "IRR", "idem-duplicate");
        Assert.Equal("idem-duplicate", payment.IdempotencyKey);
        Assert.NotEqual(Guid.Empty, payment.Id);
    }

    [Fact]
    public void VerifySuccess_RequiresReferenceAndSetsVerifiedAt()
    {
        var payment = new PaymentTransaction(Guid.NewGuid(), 1000, "IRR", "idem-verify");
        payment.MarkGatewayCreated("sadad-token");
        payment.MarkSucceeded("trace-123");
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal("sadad-token", payment.GatewayToken);
        Assert.Equal("trace-123", payment.GatewayReference);
        Assert.NotNull(payment.VerifiedAt);
    }

    [Fact]
    public void FailedPayment_CannotLaterBecomeSucceededWithoutValidTransition()
    {
        var payment = new PaymentTransaction(Guid.NewGuid(), 1000, "IRR", "idem-fail");
        payment.MarkFailed();
        Assert.Throws<InvalidOperationException>(() => payment.MarkSucceeded("trace"));
    }

    [Fact]
    public void VerificationWithAmbiguousGatewayResult_CanBeMovedToReconciliationRequiredButNotAutoSettled()
    {
        var payment = new PaymentTransaction(Guid.NewGuid(), 1000, "IRR", "idem-reconcile");
        payment.MarkGatewayCreated("sadad-token");
        payment.MarkVerifying();
        payment.MarkReconciliationRequired();
        Assert.Equal(PaymentStatus.ReconciliationRequired, payment.Status);
        Assert.Throws<InvalidOperationException>(() => payment.MarkSucceeded("trace"));
        Assert.Throws<InvalidOperationException>(() => payment.MarkFailed());
        Assert.Throws<InvalidOperationException>(() => payment.MarkCancelled());
    }

    [Fact]
    public void VerificationTransportFailure_RequiresReconciliationAndCannotBeRetriedAutomatically()
    {
        var payment = new PaymentTransaction(Guid.NewGuid(), 1000, "IRR", "idem-transport-failure");
        payment.MarkGatewayCreated("sadad-token");
        payment.MarkVerifying();
        payment.MarkVerificationOutcomeUnknown();
        Assert.Equal(PaymentStatus.ReconciliationRequired, payment.Status);
        Assert.Throws<InvalidOperationException>(() => payment.MarkVerifying());
        Assert.Throws<InvalidOperationException>(() => payment.MarkSucceeded("trace"));
        Assert.Throws<InvalidOperationException>(() => payment.MarkFailed());
    }
}
