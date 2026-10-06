using Brokerage.Application.UseCases;
using Brokerage.Domain.Enums;
using Brokerage.Domain.Entities;
using Brokerage.Application.Contracts;
using Brokerage.Application.Services;
using Brokerage.Application.Integration;
using Brokerage.Application.Validation;
using Brokerage.Infrastructure.Integration;
using Brokerage.Infrastructure.Persistence;
using Brokerage.Api.Middleware;
using Brokerage.Api.Authentication;
using Brokerage.Api.Authorization;
using Brokerage.Api.Endpoints;
using Brokerage.Application.Models;
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

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("otp", context =>
    {
        var partitionKey = context.User.Identity?.Name
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IAuditEventWriter, AuditEventWriter>();

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
    options.AddPolicy(AuthorizationPolicies.MfaVerified, policy => policy.AddRequirements(new MfaRequirement()));
});

builder.Services.AddDbContext<BrokerageDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("BrokerageDb")
        ?? "Data Source=brokerage.db"));

builder.Services.AddScoped<IServiceRequestRepository, ServiceRequestRepository>();
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
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        return Task.CompletedTask;
    });

    await next();
});

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "Brokerage.Api", status = "running" }));
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/ready", async (BrokerageDbContext dbContext, CancellationToken cancellationToken) =>
{
    var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
    return canConnect
        ? Results.Ok(new { status = "ready" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
});
AuditExportEndpoints.Map(app);

app.MapPost("/identity/verify", async (
    ICurrentUser currentUser,
    VerifyIdentityRequest request,
    VerifyIdentity useCase,
    HttpContext context,
    CancellationToken cancellationToken) =>
{
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
}).RequireAuthorization(AuthorizationPolicies.Applicant);

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
    HttpContext context,
    CancellationToken cancellationToken) =>
{
    if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
        return Results.Unauthorized();

    var challenge = await otpService.IssueAsync(currentUser.UserId, cancellationToken);

    await auditEventWriter.WriteAsync(new AuditEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, "OtpChallengeIssued", context.TraceIdentifier,
        null, null, "Success", null, "Issued", currentUser.UserId, currentUser.Role, context.Connection.RemoteIpAddress?.ToString()), cancellationToken);

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
    if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
        return Results.Unauthorized();

    var verified = await otpService.VerifyAsync(currentUser.UserId, request.ChallengeId, request.Code, cancellationToken);

    if (verified)
        mfaVerificationStore.MarkVerified(currentUser.UserId, DateTimeOffset.UtcNow);

    await auditEventWriter.WriteAsync(new AuditEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, "OtpVerification", context.TraceIdentifier,
        null, null, verified ? "Success" : "Failure", null, verified ? "Verified" : "Rejected",
        currentUser.UserId, currentUser.Role, context.Connection.RemoteIpAddress?.ToString()), cancellationToken);

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
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(currentUser.UserId))
        return Results.Unauthorized();

    var identityState = await identityStates.GetAsync(currentUser.UserId, cancellationToken);
    if (identityState?.IsVerified != true)
        return Results.Forbid();

    var request = useCase.Execute(serviceCode, "INITIAL", currentUser.UserId);
    return Results.Ok(new { request.Id, request.ServiceCode, request.Status, request.CreatedAt, request.UpdatedAt });
}).RequireAuthorization(AuthorizationPolicies.Applicant);

app.MapPost("/service-requests/s01", async (
    CreateS01ServiceRequest useCase, CreateS01RequestModel model, ICurrentUser currentUser,
    IIdentityVerificationStateRepository identityStates, HttpContext context,
    IAuditEventWriter auditEventWriter, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(currentUser.UserId))
        return Results.Unauthorized();

    var identityState = await identityStates.GetAsync(currentUser.UserId, cancellationToken);
    if (identityState?.IsVerified != true)
        return Results.Forbid();

    var request = await useCase.ExecuteAsync(model, currentUser.UserId, context.TraceIdentifier, cancellationToken);
    await auditEventWriter.WriteAsync(new AuditEvent(
        Guid.NewGuid(), DateTimeOffset.UtcNow, "ServiceRequestCreated", context.TraceIdentifier,
        request.Id, request.CurrentWorkflowStageId?.ToString(), "Success", null, request.Status.ToString(),
        currentUser.UserId, currentUser.Role, context.Connection.RemoteIpAddress?.ToString()), cancellationToken);
    return Results.Ok(new { request.Id, request.ServiceCode, request.Status, request.CreatedAt, request.UpdatedAt,
        request.CurrentWorkflowStageId, request.OrganizationTrackingId });
}).RequireAuthorization(AuthorizationPolicies.Applicant);

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
}).RequireAuthorization();

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
}).RequireAuthorization();

app.MapGet("/service-requests/{id:guid}/organization-status", async (
    Guid id, IServiceRequestRepository repository, ICurrentUser currentUser,
    IOrganizationIntegrationService organizationIntegrationService,
    HttpContext context, IAuditEventWriter auditEventWriter, CancellationToken cancellationToken) =>
{
    var request = await repository.GetByIdAsync(id, cancellationToken);
    if (request is null) return Results.NotFound();
    if (!CanAccessServiceRequest(currentUser, request))
        return Results.Forbid();
    if (string.IsNullOrWhiteSpace(request.OrganizationTrackingId))
        return Results.BadRequest(new { message = "The service request has no organization tracking ID." });

    var previousOrganizationStatus = request.OrganizationStatus;
    var organizationStatus = await organizationIntegrationService.GetStatusAsync(request.OrganizationTrackingId, cancellationToken);
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

    return Results.Ok(new { request.Id, request.ServiceCode, request.Status, request.OrganizationTrackingId,
        request.OrganizationStatus, request.CurrentWorkflowStageId });
}).RequireAuthorization();

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