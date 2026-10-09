using Brokerage.Api.Authorization;
using Brokerage.Application.Contracts;
using Brokerage.Application.Models;
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
                    payment.UpdatedAt
                })
            });
        }).RequireAuthorization(AuthorizationPolicies.OperationalMonitoring);
    }
}
