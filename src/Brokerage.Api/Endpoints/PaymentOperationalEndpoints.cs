using Brokerage.Application.Authorization;
using Brokerage.Application.Contracts;
using Brokerage.Application.Models;
using Brokerage.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace Brokerage.Api.Endpoints;

public static class PaymentOperationalEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/ops/payments/stale-verifications", async (
            HttpContext context,
            IPaymentTransactionRepository repository,
            IAuditEventWriter auditEventWriter,
            int? olderThanMinutes,
            int? limit,
            CancellationToken cancellationToken) =>
        {
            var ageMinutes = olderThanMinutes ?? 10;
            var resultLimit = limit ?? 100;

            if (ageMinutes is < 1 or > 1440)
                return Results.BadRequest(new { error = "olderThanMinutes must be between 1 and 1440." });

            if (resultLimit is < 1 or > 500)
                return Results.BadRequest(new { error = "limit must be between 1 and 500." });

            var observedAt = DateTimeOffset.UtcNow;
            var cutoff = observedAt.AddMinutes(-ageMinutes);
            var candidates = await repository.GetStaleVerifyingAsync(cutoff, resultLimit, cancellationToken);

            await auditEventWriter.WriteAsync(new AuditEvent(
                Guid.NewGuid(),
                observedAt,
                "PaymentReconciliationReview",
                context.TraceIdentifier,
                null,
                null,
                "Success",
                null,
                $"Candidates:{candidates.Count}",
                null,
                null,
                context.Connection.RemoteIpAddress?.ToString()), cancellationToken);

            context.Response.Headers.CacheControl = "no-store";

            return Results.Ok(new
            {
                observedAt,
                cutoff,
                count = candidates.Count,
                items = candidates.Select(payment => new
                {
                    payment.Id,
                    payment.ServiceRequestId,
                    payment.Amount,
                    payment.Currency,
                    status = payment.Status.ToString(),
                    payment.CreatedAt,
                    payment.UpdatedAt,
                    reviewReason = "VerificationStalled",
                    recommendedAction = "Check the gateway's authoritative transaction status before changing local payment state. Do not automatically mark this payment successful or failed."
                })
            });
        }).RequireAuthorization(AuthorizationPolicies.PaymentOperations);

        app.MapGet("/ops/payments/reconciliation-candidates", async (
            HttpContext context,
            IPaymentTransactionRepository repository,
            IAuditEventWriter auditEventWriter,
            int? olderThanMinutes,
            int? limit,
            CancellationToken cancellationToken) =>
        {
            var ageMinutes = olderThanMinutes ?? 10;
            var resultLimit = limit ?? 100;

            if (ageMinutes is < 1 or > 1440)
                return Results.BadRequest(new { error = "olderThanMinutes must be between 1 and 1440." });

            if (resultLimit is < 1 or > 500)
                return Results.BadRequest(new { error = "limit must be between 1 and 500." });

            var observedAt = DateTimeOffset.UtcNow;
            var cutoff = observedAt.AddMinutes(-ageMinutes);
            var candidates = await repository.GetStaleReconciliationCandidatesAsync(cutoff, resultLimit, cancellationToken);

            await auditEventWriter.WriteAsync(new AuditEvent(
                Guid.NewGuid(),
                observedAt,
                "PaymentReconciliationCandidatesListed",
                context.TraceIdentifier,
                null,
                null,
                "Success",
                null,
                $"Candidates:{candidates.Count}",
                null,
                null,
                context.Connection.RemoteIpAddress?.ToString()), cancellationToken);

            context.Response.Headers.CacheControl = "no-store";

            return Results.Ok(new
            {
                observedAt,
                cutoff,
                count = candidates.Count,
                items = candidates.Select(payment => new
                {
                    payment.Id,
                    payment.ServiceRequestId,
                    payment.Amount,
                    payment.Currency,
                    status = payment.Status.ToString(),
                    payment.CreatedAt,
                    payment.UpdatedAt,
                    reviewReason = payment.Status switch
                    {
                        PaymentStatus.Verifying => "VerificationStalled",
                        PaymentStatus.ReconciliationRequired => "GatewayResultNeedsManualReview",
                        _ when payment.GatewayToken is null => "GatewayCreateOutcomeUnknown",
                        _ => "CallbackOrVerificationPending"
                    },
                    recommendedAction = payment.Status switch
                    {
                        PaymentStatus.Verifying => "Check the gateway's authoritative transaction status before changing local payment state.",
                        PaymentStatus.ReconciliationRequired => "Review the provider's authoritative transaction record and compare amount/reference. Do not retry verification or change status until the provider's supported recovery procedure is confirmed.",
                        _ when payment.GatewayToken is null => "Check gateway and local request logs or the merchant portal before retrying Create; the original request may have been accepted.",
                        _ => "Check the authoritative gateway status and use the supported verification flow; do not infer settlement from a callback alone."
                    }
                })
            });
        }).RequireAuthorization(AuthorizationPolicies.PaymentOperations);
    }
}
