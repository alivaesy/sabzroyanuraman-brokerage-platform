using Brokerage.Application.UseCases;
using Brokerage.Domain.Enums;
using Brokerage.Domain.Entities;
using Brokerage.Application.Contracts;
using Brokerage.Application.Services;
using Brokerage.Application.Integration;
using Brokerage.Application.Validation;
using Brokerage.Infrastructure.Integration;
using Brokerage.Infrastructure.Persistence;
using Brokerage.Infrastructure.Payments;
using Brokerage.Api.Middleware;
using Brokerage.Api.Authentication;
using Brokerage.Api.Authorization;
using Brokerage.Api.Endpoints;
using Brokerage.Api.Telemetry;
using Brokerage.Application.Models;
using Brokerage.Application.Exceptions;
using Brokerage.Application.Authorization;
using Brokerage.Application.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Fail closed before service registration: SQLite is the development persistence provider,
// not an approved multi-instance production database. Do not let future integration wiring
// accidentally make a SQLite-backed production deployment appear supported.
if (!builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "Production persistence is not configured. The application currently uses SQLite; configure and verify an approved production database provider, migrations, concurrency, backup, and restore before deployment.");
}

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IOperationalMetrics, OperationalMetrics>();

static string GetRateLimitPartitionKey(HttpContext context)
{
    var userId = context.User.FindFirst(IdentityClaims.UserId)?.Value
        ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? context.User.Identity?.Name
        ?? "anonymous";
    var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    return $"{userId}:{remoteIp}";
}

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    static FixedWindowRateLimiterOptions CreateOptions() => new()
    {
        PermitLimit = 5,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
        AutoReplenishment = true
    };

    options.AddPolicy("otp", context =>
    {
        var partitionKey = GetRateLimitPartitionKey(context);
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => CreateOptions());
    });

    options.AddPolicy("identity-verification", context =>
    {
        var partitionKey = GetRateLimitPartitionKey(context);
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => CreateOptions());
    });

    options.AddPolicy("audit-export", context =>
    {
        var partitionKey = GetRateLimitPartitionKey(context);
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
 
    options.AddPolicy("operational-read", context =>
    {
        var partitionKey = GetRateLimitPartitionKey(context);
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });

    options.AddPolicy("service-request-read", context =>
    {
        var partitionKey = GetRateLimitPartitionKey(context);
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });

    options.AddPolicy("service-request-create", context =>
    {
        var partitionKey = GetRateLimitPartitionKey(context);
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });

    options.AddPolicy("payment-create", context =>
    {
        var partitionKey = GetRateLimitPartitionKey(context);
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });

    options.AddPolicy("payment-callback", context =>
    {
        var partitionKey = GetRateLimitPartitionKey(context);
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });

    options.OnRejected = (context, _) =>
    {
        var response = context.HttpContext.Response;
        response.Headers.CacheControl = "no-store";

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            var seconds = Math.Max(1L, (long)Math.Ceiling(retryAfter.TotalSeconds));
            response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        }

        return ValueTask.CompletedTask;
    };
});
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IAuditEventWriter, AuditEventWriter>();
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddAuthentication(DevelopmentAuthenticationHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(
            DevelopmentAuthenticationHandler.SchemeName,
            _ => { });
}
else
{
    var authority = builder.Configuration["Authentication:Production:Authority"];
    var audience = builder.Configuration["Authentication:Production:Audience"];

    if (string.IsNullOrWhiteSpace(authority) || string.IsNullOrWhiteSpace(audience))
        throw new InvalidOperationException(
            "Production authentication is not configured. Set Authentication:Production:Authority and Authentication:Production:Audience.");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = authority;
            options.Audience = audience;
            options.RequireHttpsMetadata =
                builder.Configuration.GetValue("Authentication:Production:RequireHttpsMetadata", true);

            options.TokenValidationParameters = new TokenValidationParameters
            {
                NameClaimType = IdentityClaims.UserId,
                RoleClaimType = IdentityClaims.Role
            };
        });
}

builder.Services.AddScoped<IMfaVerificationStore, PersistentMfaVerificationStore>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, MfaAuthorizationHandler>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.Applicant, policy => policy.RequireRole(UserRole.Applicant.ToString()));
    options.AddPolicy(AuthorizationPolicies.Expert, policy => policy.RequireRole(UserRole.Expert.ToString()));
    options.AddPolicy(AuthorizationPolicies.Support, policy => policy.RequireRole(UserRole.Support.ToString()));
    options.AddPolicy(AuthorizationPolicies.TechnicalSecurity, policy => policy.RequireRole(UserRole.TechnicalSecurity.ToString()));
    options.AddPolicy(AuthorizationPolicies.OrganizationObserver, policy => policy.RequireRole(UserRole.OrganizationObserver.ToString()));
    options.AddPolicy(AuthorizationPolicies.Administrator, policy => policy.RequireRole(UserRole.Administrator.ToString()));
    options.AddPolicy(AuthorizationPolicies.OperationalMonitoring, policy => policy.RequireRole(
        UserRole.TechnicalSecurity.ToString(),
        UserRole.OrganizationObserver.ToString(),
        UserRole.Administrator.ToString()));
    options.AddPolicy(AuthorizationPolicies.PaymentOperations, policy => policy.RequireRole(
        UserRole.TechnicalSecurity.ToString(),
        UserRole.Administrator.ToString()));
    options.AddPolicy(AuthorizationPolicies.MfaVerified, policy => policy.AddRequirements(new MfaRequirement()));
});

builder.Services.AddDbContext<BrokerageDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("BrokerageDb")
        ?? "Data Source=brokerage.db"));

builder.Services.AddScoped<IServiceRequestRepository, ServiceRequestRepository>();
builder.Services.AddScoped<IPaymentTransactionRepository, PaymentTransactionRepository>();
builder.Services.AddSingleton(_ => SadadOptions.FromConfiguration(builder.Configuration));
// A bounded provider timeout prevents a stalled gateway from holding request resources indefinitely.
builder.Services.AddHttpClient<SadadPaymentGateway>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    // Sadad request bodies include signed payment data. Do not forward them automatically
    // if a configured provider endpoint responds with a redirect to another host.
    AllowAutoRedirect = false
});
builder.Services.AddScoped<IPaymentGateway>(services => services.GetRequiredService<SadadPaymentGateway>());
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<IIdentityVerificationStateRepository, IdentityVerificationStateRepository>();
builder.Services.AddScoped<CreateServiceRequest>();
builder.Services.AddScoped<CreateS01ServiceRequest>();
builder.Services.AddScoped<VerifyIdentity>();
builder.Services.AddScoped<WorkflowService>();
builder.Services.AddScoped<IOrganizationIntegrationService, OrganizationIntegrationService>();
builder.Services.AddScoped<OrganizationRetryPolicy>();
builder.Services.AddScoped<OrganizationRetryOptions>();
builder.Services.AddScoped<OrganizationTimeoutOptions>();
builder.Services.AddScoped<OrganizationRetryExecutor>();
builder.Services.AddScoped<IIdentityVerificationService, IdentityVerificationService>();
builder.Services.AddScoped<INationalIdentifierValidator, IranianNationalIdentifierValidator>();
builder.Services.AddSingleton<IOtpCodeHasher>(_ =>
{
    var configuredKey = builder.Configuration["Otp:HashKeyBase64"]
        ?? Environment.GetEnvironmentVariable("BROKERAGE_OTP_HASH_KEY");

    if (!string.IsNullOrWhiteSpace(configuredKey))
    {
        try
        {
            return new HmacOtpCodeHasher(Convert.FromBase64String(configuredKey));
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Otp:HashKeyBase64 must contain a valid Base64 key.", ex);
        }
    }

    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException(
            "Production OTP hashing is not configured. Set Otp:HashKeyBase64 or BROKERAGE_OTP_HASH_KEY to a secret key of at least 256 bits.");

    return new HmacOtpCodeHasher(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
});

builder.Services.AddScoped<IOtpService, PersistentOtpService>();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<Brokerage.Application.Integration.IOrganizationApiClient, MockOrganizationApiClient>();
    builder.Services.AddScoped<ISanaClient, MockSanaClient>();
    builder.Services.AddScoped<IShahkarClient, MockShahkarClient>();
}
else
{
    throw new InvalidOperationException(
        "Production integrations are not configured. Organization API, Sana, and Shahkar must use approved production adapters; development mocks are not permitted outside Development.");
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<BrokerageDbContext>();
    await DatabaseInitializer.InitializeAsync(dbContext, app.Configuration["DatabaseInitialization:Mode"]);
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();
app.UseRouting();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestTelemetryMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
        return Task.CompletedTask;
    });

    await next();
});

// Identity and service-request APIs return account-specific or case-specific data.
// Apply no-store before authorization/endpoint execution so error responses are covered too.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/identity") ||
        context.Request.Path.StartsWithSegments("/service-requests") ||
        context.Request.Path.StartsWithSegments("/payments") ||
        context.Request.Path.StartsWithSegments("/ops/payments") ||
        context.Request.Path.StartsWithSegments("/audit/events/export"))
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Task.CompletedTask;
        });
    }

    await next();
});

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "Brokerage.Api", status = "running" }));
app.MapGet("/health", (HttpContext context) =>
{
    context.Response.Headers.CacheControl = "no-store";
    return Results.Ok(new { status = "healthy" });
});
app.MapGet("/ready", async (HttpContext context, BrokerageDbContext dbContext, CancellationToken cancellationToken) =>
{
    context.Response.Headers.CacheControl = "no-store";
    var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
    return canConnect
        ? Results.Ok(new { status = "ready" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
});

app.MapGet("/ops/metrics", (HttpContext context, IOperationalMetrics metrics) =>
{
    context.Response.Headers.CacheControl = "no-store";
    return Results.Ok(metrics.Snapshot());
}).RequireAuthorization(AuthorizationPolicies.OperationalMonitoring).RequireRateLimiting("operational-read");

app.MapGet("/ops/status", async (
    HttpContext context,
    BrokerageDbContext dbContext,
    IOperationalMetrics metrics,
    CancellationToken cancellationToken) =>
{
    context.Response.Headers.CacheControl = "no-store";
    var databaseCheck = System.Diagnostics.Stopwatch.StartNew();
    var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
    databaseCheck.Stop();

    var snapshot = metrics.Snapshot();
    var payload = new
    {
        status = canConnect ? "operational" : "degraded",
        ready = canConnect,
        observedAt = snapshot.ObservedAt,
        startedAt = snapshot.StartedAt,
        uptime = snapshot.Uptime,
        totalRequestCount = snapshot.TotalRequestCount,
        errorRatePercent = snapshot.ErrorRatePercent,
        averageApiLatencyMilliseconds = snapshot.AverageElapsedMilliseconds,
        requestsPerMinute = snapshot.RequestsPerMinute,
        databaseCheckElapsedMilliseconds = databaseCheck.Elapsed.TotalMilliseconds
    };

    return canConnect
        ? Results.Ok(payload)
        : Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable);
}).RequireAuthorization(AuthorizationPolicies.OperationalMonitoring).RequireRateLimiting("operational-read");
AuditExportEndpoints.Map(app);
PaymentOperationalEndpoints.Map(app);
PaymentEndpoints.Map(app);

app.MapPost("/identity/verify", async (
    ICurrentUser currentUser,
    VerifyIdentityRequest request,
    VerifyIdentity useCase,
    HttpContext context,
    CancellationToken cancellationToken) =>
{
    context.Response.Headers.CacheControl = "no-store";

    if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
        return Results.Unauthorized();

    var result = await useCase.ExecuteAsync(
        currentUser.UserId,
        request.NationalIdentifier,
        context.TraceIdentifier,
        cancellationToken);

    return result.Verified
        ? Results.Ok(new { verified = true })
        : Results.BadRequest(new { verified = false });
}).RequireAuthorization(AuthorizationPolicies.Applicant).RequireRateLimiting("identity-verification");

app.MapGet("/identity/me", async (ICurrentUser currentUser, IIdentityVerificationStateRepository stateRepository, CancellationToken cancellationToken) =>
{
    var state = currentUser.UserId is null ? null : await stateRepository.GetAsync(currentUser.UserId, cancellationToken);
    return Results.Ok(new { userId = currentUser.UserId, role = currentUser.Role, isMfaVerified = currentUser.IsMfaVerified,
        isIdentityVerified = state?.IsVerified == true, identityVerifiedAt = state?.VerifiedAt });
}).RequireAuthorization();

app.MapPost("/identity/otp/challenges", async (
    ICurrentUser currentUser,
    IOtpService otpService,
    IAuditEventWriter auditEventWriter,
    IUnitOfWork unitOfWork,
    HttpContext context,
    CancellationToken cancellationToken) =>
{
    context.Response.Headers.CacheControl = "no-store";

    if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
        return Results.Unauthorized();

    // Challenge persistence and its mandatory audit record are one local transaction.
    // If audit persistence fails, the challenge must not remain usable but undisclosed.
    await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
    var challenge = await otpService.IssueAsync(currentUser.UserId, cancellationToken);

    await auditEventWriter.WriteAsync(new AuditEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, "OtpChallengeIssued", context.TraceIdentifier,
        null, null, "Success", null, "Issued", currentUser.UserId, currentUser.Role, context.Connection.RemoteIpAddress?.ToString()), cancellationToken);

    await transaction.CommitAsync(cancellationToken);
    return Results.Ok(new { challenge.ChallengeId, challenge.ExpiresAt });
}).RequireAuthorization().RequireRateLimiting("otp");

app.MapPost("/identity/otp/verify", async (
    ICurrentUser currentUser,
    OtpVerificationRequest request,
    IOtpService otpService,
    IMfaVerificationStore mfaVerificationStore,
    IAuditEventWriter auditEventWriter,
    HttpContext context,
    CancellationToken cancellationToken) =>
{
    context.Response.Headers.CacheControl = "no-store";

    if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
        return Results.Unauthorized();

    var verified = await otpService.VerifyAsync(currentUser.UserId, request.ChallengeId, request.Code, cancellationToken);

    // Persist the mandatory audit event before granting the MFA state. If audit
    // persistence fails, the request fails closed and no new verification is granted.
    await auditEventWriter.WriteAsync(new AuditEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, "OtpVerification", context.TraceIdentifier,
        null, null, verified ? "Success" : "Failure", null, verified ? "Verified" : "Rejected",
        currentUser.UserId, currentUser.Role, context.Connection.RemoteIpAddress?.ToString()), cancellationToken);

    if (verified)
        mfaVerificationStore.MarkVerified(currentUser.UserId, DateTimeOffset.UtcNow);

    return verified
        ? Results.Ok(new { verified = true })
        : Results.BadRequest(new { verified = false });
}).RequireAuthorization().RequireRateLimiting("otp");

app.MapGet("/identity/mfa-required", () => Results.Ok(new { authorized = true }))
    .RequireAuthorization(AuthorizationPolicies.MfaVerified);

app.MapGet("/identity/applicant-only", () => Results.Ok(new { authorized = true }))
    .RequireAuthorization(AuthorizationPolicies.Applicant);

app.MapPost("/service-requests", async (
    CreateServiceRequest useCase,
    ServiceCode serviceCode,
    ICurrentUser currentUser,
    IIdentityVerificationStateRepository identityStates,
    HttpContext context,
    CancellationToken cancellationToken) =>
{
    if (!Enum.IsDefined(serviceCode))
        return Results.BadRequest(new { message = "Unsupported service code." });

    if (serviceCode == ServiceCode.S01)
        return Results.BadRequest(new { message = "S01 requests must use the dedicated S01 workflow." });

    // Only S02/S03 have an explicitly confirmed generic request path. Keep enum
    // values for future services from becoming available before their requirements
    // and dedicated acceptance criteria are approved.
    if (serviceCode is not (ServiceCode.S02 or ServiceCode.S03))
        return Results.BadRequest(new { message = "Only approved S02/S03 services can use this workflow." });

    if (string.IsNullOrWhiteSpace(currentUser.UserId))
        return Results.Unauthorized();

    var identityState = await identityStates.GetAsync(currentUser.UserId, cancellationToken);
    if (identityState?.IsVerified != true)
        return Results.Forbid();

    var request = await useCase.ExecuteAsync(
        serviceCode,
        "INITIAL",
        currentUser.UserId,
        cancellationToken,
        context.TraceIdentifier,
        currentUser.Role,
        context.Connection.RemoteIpAddress?.ToString());
    return Results.Ok(new { request.Id, request.ServiceCode, request.Status, request.CreatedAt, request.UpdatedAt, request.CurrentWorkflowStageId });
}).RequireAuthorization(AuthorizationPolicies.Applicant).RequireRateLimiting("service-request-create");

app.MapPost("/service-requests/s01", async (
    CreateS01ServiceRequest useCase, CreateS01RequestModel model, ICurrentUser currentUser,
    IIdentityVerificationStateRepository identityStates, HttpContext context,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(currentUser.UserId))
        return Results.Unauthorized();

    var identityState = await identityStates.GetAsync(currentUser.UserId, cancellationToken);
    if (identityState?.IsVerified != true)
        return Results.Forbid();

    var request = await useCase.ExecuteAsync(
        model, currentUser.UserId, context.TraceIdentifier, cancellationToken,
        currentUser.Role, context.Connection.RemoteIpAddress?.ToString());
    return Results.Ok(new { request.Id, request.ServiceCode, request.Status, request.CreatedAt, request.UpdatedAt,
        request.CurrentWorkflowStageId, request.OrganizationTrackingId });
}).RequireAuthorization(AuthorizationPolicies.Applicant).RequireRateLimiting("service-request-create");

app.MapGet("/service-requests/{id:guid}", async (
    Guid id,
    IServiceRequestRepository repository,
    ICurrentUser currentUser,
    CancellationToken cancellationToken) =>
{
    var request = await repository.GetByIdAsync(id, cancellationToken);
    if (request is null) return Results.NotFound();
    if (!CanAccessServiceRequest(currentUser, request))
        return Results.Forbid();

    return Results.Ok(new { request.Id, request.ServiceCode, request.Status,
        request.CreatedAt, request.UpdatedAt, request.CurrentWorkflowStageId, request.OrganizationTrackingId, request.OrganizationStatus });
}).RequireAuthorization().RequireRateLimiting("service-request-read");

app.MapGet("/service-requests/{id:guid}/workflow-stages", async (
    Guid id,
    IServiceRequestRepository repository,
    ICurrentUser currentUser,
    CancellationToken cancellationToken) =>
{
    var request = await repository.GetByIdAsync(id, cancellationToken);
    if (request is null) return Results.NotFound();
    if (!CanAccessServiceRequest(currentUser, request))
        return Results.Forbid();

    var stages = await repository.GetWorkflowStagesAsync(id, cancellationToken);
    return Results.Ok(stages.Select(stage => new { stage.Id, stage.ServiceRequestId, stage.StageCode, stage.CreatedAt, stage.CompletedAt }));
}).RequireAuthorization().RequireRateLimiting("service-request-read");

static async Task<IResult> RefreshOrganizationStatusAsync(
    Guid id, IServiceRequestRepository repository, ICurrentUser currentUser,
    IOrganizationIntegrationService organizationIntegrationService,
    HttpContext context, IAuditEventWriter auditEventWriter, IUnitOfWork unitOfWork,
    CancellationToken cancellationToken)
{
    var request = await repository.GetByIdAsync(id, cancellationToken);
    if (request is null) return Results.NotFound();
    if (!CanAccessServiceRequest(currentUser, request))
        return Results.Forbid();

    // This endpoint advances S01-specific stages. Do not let a future or generic
    // service accidentally enter that workflow merely because it has a tracking ID.
    if (request.ServiceCode != ServiceCode.S01)
        return Results.BadRequest(new { message = "Organization status refresh is not enabled for this service workflow." });

    if (string.IsNullOrWhiteSpace(request.OrganizationTrackingId))
        return Results.BadRequest(new { message = "The service request has no organization tracking ID." });

    // Keep the remote call outside the database transaction; the local request/stage/audit
    // updates below must commit or roll back together.
    var previousOrganizationStatus = request.OrganizationStatus;
    string organizationStatus;
    try
    {
        organizationStatus = await organizationIntegrationService.GetStatusAsync(
            request.OrganizationTrackingId, cancellationToken);
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
        // Transport/runtime failures are also provider failures. Record only a bounded,
        // generic outcome; never copy provider payloads or exception text into the audit log.
        // No request/stage state is changed, and the original safe API error mapping remains.
        await auditEventWriter.WriteAsync(new AuditEvent(
            Guid.NewGuid(), DateTimeOffset.UtcNow, "OrganizationStatusRefreshFailed", context.TraceIdentifier,
            request.Id, S01StageCode.OrganizationFollowUp.ToString(), "Failure", previousOrganizationStatus,
            "ProviderError", currentUser.UserId, currentUser.Role,
            context.Connection.RemoteIpAddress?.ToString()), CancellationToken.None);
        throw;
    }

    try
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        request.SetOrganizationStatus(organizationStatus);

        var workflowStages = await repository.GetWorkflowStagesAsync(id, cancellationToken);
        var followUpStage = workflowStages.SingleOrDefault(stage => stage.StageCode == S01StageCode.OrganizationFollowUp.ToString());

        if (followUpStage is not null && followUpStage.CompletedAt is null)
        {
            followUpStage.Complete();
            await auditEventWriter.WriteAsync(new AuditEvent(
                Guid.NewGuid(), DateTimeOffset.UtcNow, "WorkflowStageCompleted", context.TraceIdentifier,
                request.Id, followUpStage.StageCode, "Success", null, followUpStage.StageCode,
                currentUser.UserId, currentUser.Role, context.Connection.RemoteIpAddress?.ToString()), cancellationToken);
        }

        var resultStage = workflowStages.SingleOrDefault(stage => stage.StageCode == S01StageCode.ResultNotification.ToString());
        if (resultStage is null)
        {
            resultStage = new WorkflowStage(request.Id, S01StageCode.ResultNotification.ToString());
            await repository.AddWorkflowStageAsync(resultStage, cancellationToken);
            request.SetCurrentWorkflowStage(resultStage.Id);
            await auditEventWriter.WriteAsync(new AuditEvent(
                Guid.NewGuid(), DateTimeOffset.UtcNow, "WorkflowStageCreated", context.TraceIdentifier,
                request.Id, resultStage.StageCode, "Success", null, resultStage.StageCode,
                currentUser.UserId, currentUser.Role, context.Connection.RemoteIpAddress?.ToString()), cancellationToken);
        }

        await repository.SaveChangesAsync(cancellationToken);
        await auditEventWriter.WriteAsync(new AuditEvent(
            Guid.NewGuid(), DateTimeOffset.UtcNow, "OrganizationStatusReceived", context.TraceIdentifier,
            request.Id, resultStage.StageCode, "Success", previousOrganizationStatus, organizationStatus,
            currentUser.UserId, currentUser.Role, context.Connection.RemoteIpAddress?.ToString()), cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new { request.Id, request.ServiceCode, request.Status, request.OrganizationTrackingId,
            request.OrganizationStatus, request.CurrentWorkflowStageId });
    }
    catch (DbUpdateConcurrencyException)
    {
        // Another refresh committed after this request read the old request version.
        // Do not retry the external call or overwrite the newer local transition.
        return Results.Conflict(new
        {
            message = "Organization status changed while this refresh was in progress. Retry the refresh."
        });
    }
}

app.MapGet("/service-requests/{id:guid}/organization-status", RefreshOrganizationStatusAsync)
    .RequireAuthorization().RequireRateLimiting("service-request-read");
app.MapPost("/service-requests/{id:guid}/organization-status", RefreshOrganizationStatusAsync)
    .RequireAuthorization().RequireRateLimiting("service-request-read");

static bool CanAccessServiceRequest(ICurrentUser currentUser, ServiceRequest request)
{
    if (string.IsNullOrWhiteSpace(currentUser.UserId))
        return false;

    if (string.Equals(request.ApplicantUserId, currentUser.UserId, StringComparison.Ordinal))
        return true;

    return currentUser.Role is
        nameof(UserRole.Expert) or
        nameof(UserRole.Support) or
        nameof(UserRole.TechnicalSecurity) or
        nameof(UserRole.OrganizationObserver) or
        nameof(UserRole.Administrator);
}

app.Run();

public sealed record OtpVerificationRequest(string ChallengeId, string Code);
public sealed record VerifyIdentityRequest(string NationalIdentifier);