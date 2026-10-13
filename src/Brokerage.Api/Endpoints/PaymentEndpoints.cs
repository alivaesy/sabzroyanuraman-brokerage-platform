using Brokerage.Application.Authorization;
using Brokerage.Application.Contracts;
using Brokerage.Application.Services;
using Brokerage.Domain.Enums;
using Brokerage.Infrastructure.Payments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace Brokerage.Api.Endpoints;

public static class PaymentEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/payments", async (
            CreatePaymentRequest request,
            HttpContext context,
            ICurrentUser currentUser,
            IServiceRequestRepository serviceRequests,
            PaymentService paymentService,
            SadadOptions sadadOptions,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            context.Response.Headers.CacheControl = "no-store";

            if (!configuration.GetValue<bool>("Payment:Enabled"))
                return Results.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Payment initiation is disabled.",
                    extensions: new Dictionary<string, object?> { ["code"] = "PAYMENT_NOT_ENABLED" });

            if (request.ServiceRequestId == Guid.Empty || string.IsNullOrWhiteSpace(currentUser.UserId))
                return Results.BadRequest(new { error = "INVALID_PAYMENT_REQUEST" });

            var serviceRequest = await serviceRequests.GetByIdAsync(request.ServiceRequestId, cancellationToken);
            if (serviceRequest is null)
                return Results.NotFound(new { error = "SERVICE_REQUEST_NOT_FOUND" });

            if (!string.Equals(serviceRequest.ApplicantUserId, currentUser.UserId, StringComparison.Ordinal))
                return Results.Forbid();

            // Tariffs are deliberately server-owned and fail closed. No client-provided amount
            // or fallback/demo amount is accepted. Values are in Iranian rials (IRR).
            var tariffKey = $"Payment:Tariffs:{serviceRequest.ServiceCode}";
            var amount = configuration.GetValue<long?>(tariffKey);
            if (amount is null or <= 0)
                return Results.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Payment tariff is not configured.",
                    extensions: new Dictionary<string, object?> { ["code"] = "PAYMENT_TARIFF_NOT_CONFIGURED" });

            var idempotencyKey = context.Request.Headers["Idempotency-Key"].ToString();
            if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200)
                return Results.BadRequest(new { error = "INVALID_IDEMPOTENCY_KEY" });

            try
            {
                sadadOptions.Validate();
            }
            catch (InvalidOperationException)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Payment gateway is not configured.",
                    extensions: new Dictionary<string, object?> { ["code"] = "PAYMENT_GATEWAY_NOT_CONFIGURED" });
            }

            PaymentCreateResult result;
            try
            {
                result = await paymentService.CreateAsync(
                    serviceRequest.Id,
                    amount.Value,
                    idempotencyKey,
                    sadadOptions.CallbackUrl,
                    context.TraceIdentifier,
                    cancellationToken);
            }
            catch (HttpRequestException)
            {
                // The provider may have accepted the order even though the response was lost.
                // The service persists the pending transaction; never turn this into a retryable 500.
                return Results.Accepted(value: new
                {
                    status = Brokerage.Domain.Enums.PaymentStatus.Pending.ToString(),
                    error = "PAYMENT_CREATE_OUTCOME_UNKNOWN"
                });
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Results.Accepted(value: new
                {
                    status = Brokerage.Domain.Enums.PaymentStatus.Pending.ToString(),
                    error = "PAYMENT_CREATE_OUTCOME_UNKNOWN"
                });
            }

            if (string.Equals(result.ErrorCode, "IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_REQUEST", StringComparison.Ordinal))
                return Results.Conflict(new { error = result.ErrorCode });

            // An idempotent replay must not send a user back to Sadad after the
            // transaction has left Pending. In particular, reconciliation-required
            // payments must be reviewed against the provider's authoritative record.
            if (result.AlreadyProcessed && result.Transaction.Status != Brokerage.Domain.Enums.PaymentStatus.Pending)
            {
                var replay = new
                {
                    paymentId = result.Transaction.Id,
                    amount = result.Transaction.Amount,
                    currency = result.Transaction.Currency,
                    status = result.Transaction.Status.ToString(),
                    alreadyProcessed = true,
                    error = result.Transaction.Status switch
                    {
                        Brokerage.Domain.Enums.PaymentStatus.ReconciliationRequired => "PAYMENT_REQUIRES_RECONCILIATION",
                        Brokerage.Domain.Enums.PaymentStatus.Verifying => "PAYMENT_VERIFICATION_IN_PROGRESS",
                        Brokerage.Domain.Enums.PaymentStatus.Succeeded => (string?)null,
                        _ => "PAYMENT_NOT_PENDING"
                    }
                };

                if (result.Transaction.Status == Brokerage.Domain.Enums.PaymentStatus.Succeeded)
                    return Results.Ok(replay);

                if (result.Transaction.Status is Brokerage.Domain.Enums.PaymentStatus.ReconciliationRequired
                    or Brokerage.Domain.Enums.PaymentStatus.Verifying)
                    return Results.Accepted(value: replay);

                return Results.Conflict(replay);
            }

            if (string.IsNullOrWhiteSpace(result.GatewayToken))
            {
                if (result.Transaction.Status is Brokerage.Domain.Enums.PaymentStatus.Pending or Brokerage.Domain.Enums.PaymentStatus.ReconciliationRequired)
                    return Results.Accepted(value: new
                    {
                        paymentId = result.Transaction.Id,
                        status = result.Transaction.Status.ToString(),
                        error = result.ErrorCode ?? "PAYMENT_REQUIRES_RECONCILIATION"
                    });

                return Results.BadRequest(new
                {
                    paymentId = result.Transaction.Id,
                    status = result.Transaction.Status.ToString(),
                    error = result.ErrorCode ?? "PAYMENT_CREATE_FAILED"
                });
            }

            var redirectUrl = $"{sadadOptions.PaymentUrl.TrimEnd('/')}?token={Uri.EscapeDataString(result.GatewayToken)}";
            return Results.Ok(new
            {
                paymentId = result.Transaction.Id,
                amount = result.Transaction.Amount,
                currency = result.Transaction.Currency,
                status = result.Transaction.Status.ToString(),
                alreadyProcessed = result.AlreadyProcessed,
                redirectUrl
            });
        })
        .RequireAuthorization(AuthorizationPolicies.Applicant)
        .RequireAuthorization(AuthorizationPolicies.MfaVerified)
        .RequireRateLimiting("payment-create");

        // Sadad returns the browser to this public endpoint. The callback token is only a
        // correlation value: PaymentService verifies it against the persisted token and then
        // performs server-to-server verification with Sadad before recording success.
        app.MapMethods("/payments/sadad/callback", new[] { HttpMethods.Get, HttpMethods.Post }, async (
            HttpContext context,
            PaymentService paymentService,
            CancellationToken cancellationToken) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; style-src 'unsafe-inline'; frame-ancestors 'none'";
            string token;
            string orderId;

            if (context.Request.HasFormContentType)
            {
                // Sadad only needs a small URL-encoded callback. Bound the body before parsing
                // so an anonymous callback cannot force an unbounded form allocation.
                const long maxCallbackBodyBytes = 16 * 1024;
                if (context.Request.ContentLength is > maxCallbackBodyBytes ||
                    !string.Equals(
                        context.Request.ContentType?.Split(';', 2)[0].Trim(),
                        "application/x-www-form-urlencoded",
                        StringComparison.OrdinalIgnoreCase))
                    return CallbackPage("اطلاعات بازگشت پرداخت معتبر نیست. وضعیت پرداخت باید بررسی شود.");

                var bodySizeFeature = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
                if (bodySizeFeature is { IsReadOnly: false })
                    bodySizeFeature.MaxRequestBodySize = maxCallbackBodyBytes;

                var form = await context.Request.ReadFormAsync(cancellationToken);
                token = FirstNonEmpty(form["Token"].ToString(), form["token"].ToString());
                orderId = FirstNonEmpty(form["OrderId"].ToString(), form["OrderID"].ToString(), form["orderId"].ToString());
            }
            else
            {
                token = FirstNonEmpty(context.Request.Query["Token"].ToString(), context.Request.Query["token"].ToString());
                orderId = FirstNonEmpty(
                    context.Request.Query["OrderId"].ToString(),
                    context.Request.Query["OrderID"].ToString(),
                    context.Request.Query["orderId"].ToString());
            }

            // Bound untrusted callback fields before database/provider work.
            if (string.IsNullOrWhiteSpace(token) || token.Length > 512 || orderId.Length > 64 ||
                !Guid.TryParseExact(orderId, "N", out var paymentId))
                return CallbackPage("اطلاعات بازگشت پرداخت معتبر نیست. وضعیت پرداخت باید بررسی شود.");

            try
            {
                var result = await paymentService.VerifyAsync(
                    paymentId, token, context.TraceIdentifier, cancellationToken);

                return result.Succeeded
                    ? CallbackPage("پرداخت با موفقیت تأیید شد.")
                    : CallbackPage("وضعیت پرداخت هنوز قابل تأیید نیست و نیازمند بررسی است.");
            }
            catch (PaymentTransactionNotFoundException)
            {
                return CallbackPage("وضعیت پرداخت قابل شناسایی نیست و نیازمند بررسی است.");
            }
            catch (HttpRequestException)
            {
                // PaymentService persists an ambiguous verification result before propagating
                // gateway transport failures. Never repeat verification automatically here.
                return CallbackPage("نتیجه پرداخت قطعی نیست و برای جلوگیری از پرداخت تکراری نیازمند بررسی است.");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return CallbackPage("نتیجه پرداخت قطعی نیست و برای جلوگیری از پرداخت تکراری نیازمند بررسی است.");
            }
        })
        .AllowAnonymous()
        .RequireRateLimiting("payment-callback");
    }

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static IResult CallbackPage(string message) =>
        Results.Content(
            $"<!doctype html><html lang=\"fa\" dir=\"rtl\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>وضعیت پرداخت</title><body><main><h1>وضعیت پرداخت</h1><p>{message}</p></main></body></html>",
            "text/html; charset=utf-8");
}

public sealed record CreatePaymentRequest(Guid ServiceRequestId);
