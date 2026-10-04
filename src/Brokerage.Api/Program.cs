using Brokerage.Application.UseCases;
using Brokerage.Domain.Enums;
using Brokerage.Domain.Entities;
using Brokerage.Application.Contracts;
using Brokerage.Application.Services;
using Brokerage.Application.Integration;
using Brokerage.Infrastructure.Integration;
using Brokerage.Infrastructure.Persistence;
using Brokerage.Api.Middleware;
using Brokerage.Api.Authentication;
using Brokerage.Application.Models;
using Brokerage.Application.Authorization;
using Brokerage.Application.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddOpenApi();

builder.Services.AddAuthentication(DevelopmentAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(
        DevelopmentAuthenticationHandler.SchemeName,
        _ => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.Applicant, policy => policy.RequireRole(UserRole.Applicant.ToString()));
    options.AddPolicy(AuthorizationPolicies.Expert, policy => policy.RequireRole(UserRole.Expert.ToString()));
    options.AddPolicy(AuthorizationPolicies.Support, policy => policy.RequireRole(UserRole.Support.ToString()));
    options.AddPolicy(AuthorizationPolicies.TechnicalSecurity, policy => policy.RequireRole(UserRole.TechnicalSecurity.ToString()));
    options.AddPolicy(AuthorizationPolicies.OrganizationObserver, policy => policy.RequireRole(UserRole.OrganizationObserver.ToString()));
    options.AddPolicy(AuthorizationPolicies.Administrator, policy => policy.RequireRole(UserRole.Administrator.ToString()));
    options.AddPolicy(AuthorizationPolicies.MfaVerified, policy => policy.RequireClaim(IdentityClaims.MfaVerified, "true"));
});

builder.Services.AddDbContext<BrokerageDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("BrokerageDb")
        ?? "Data Source=brokerage.db"));

builder.Services.AddScoped<IServiceRequestRepository, ServiceRequestRepository>();
builder.Services.AddScoped<CreateServiceRequest>();
builder.Services.AddScoped<CreateS01ServiceRequest>();
builder.Services.AddScoped<WorkflowService>();
builder.Services.AddScoped<IOrganizationIntegrationService, OrganizationIntegrationService>();
builder.Services.AddScoped<OrganizationRetryPolicy>();
builder.Services.AddScoped<OrganizationRetryOptions>();
builder.Services.AddScoped<OrganizationTimeoutOptions>();
builder.Services.AddScoped<OrganizationRetryExecutor>();
builder.Services.AddScoped<Brokerage.Application.Integration.IOrganizationApiClient, MockOrganizationApiClient>();
builder.Services.AddScoped<IIdentityVerificationService, IdentityVerificationService>();
builder.Services.AddScoped<ISanaClient, MockSanaClient>();
builder.Services.AddScoped<IShahkarClient, MockShahkarClient>();
builder.Services.AddSingleton<IOtpService, InMemoryOtpService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<BrokerageDbContext>();
    await DatabaseInitializer.InitializeAsync(dbContext);
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseHttpsRedirection();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "Brokerage.Api", status = "running" }));

app.MapGet("/identity/me", (HttpContext context) =>
{
    var userId = context.User.FindFirst(IdentityClaims.UserId)?.Value;
    var role = context.User.FindFirst(IdentityClaims.Role)?.Value;
    return Results.Ok(new { userId, role });
}).RequireAuthorization();

app.MapPost("/identity/otp/challenges", async (
    HttpContext context,
    IOtpService otpService,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    var userId = context.User.FindFirst(IdentityClaims.UserId)?.Value;
    if (string.IsNullOrWhiteSpace(userId))
        return Results.Unauthorized();

    var challenge = await otpService.IssueAsync(userId, cancellationToken);

    loggerFactory.CreateLogger("Audit").LogInformation(
        "AuditEvent {@AuditEvent}",
        new AuditEvent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "OtpChallengeIssued",
            context.TraceIdentifier,
            null,
            null,
            "Success",
            null,
            "Issued"));

    return Results.Ok(new
    {
        challenge.ChallengeId,
        challenge.ExpiresAt
    });
}).RequireAuthorization();

app.MapPost("/identity/otp/verify", async (
    HttpContext context,
    OtpVerificationRequest request,
    IOtpService otpService,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    var userId = context.User.FindFirst(IdentityClaims.UserId)?.Value;
    if (string.IsNullOrWhiteSpace(userId))
        return Results.Unauthorized();

    var verified = await otpService.VerifyAsync(
        userId,
        request.ChallengeId,
        request.Code,
        cancellationToken);

    loggerFactory.CreateLogger("Audit").LogInformation(
        "AuditEvent {@AuditEvent}",
        new AuditEvent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "OtpVerification",
            context.TraceIdentifier,
            null,
            null,
            verified ? "Success" : "Failure",
            null,
            verified ? "Verified" : "Rejected"));

    return verified
        ? Results.Ok(new { verified = true })
        : Results.BadRequest(new { verified = false });
}).RequireAuthorization();

app.MapGet("/identity/applicant-only", () => Results.Ok(new { authorized = true }))
    .RequireAuthorization(AuthorizationPolicies.Applicant);

app.MapGet("/identity/mfa-required", () => Results.Ok(new { authorized = true }))
    .RequireAuthorization(AuthorizationPolicies.MfaVerified);

app.MapPost("/service-requests", (CreateServiceRequest useCase, ServiceCode serviceCode) =>
{
    var request = useCase.Execute(serviceCode, "INITIAL");
    return Results.Ok(new { request.Id, request.ServiceCode, request.Status, request.CreatedAt, request.UpdatedAt });
});

app.MapPost("/service-requests/s01", async (
    CreateS01ServiceRequest useCase, CreateS01RequestModel model, HttpContext context,
    ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
{
    var request = await useCase.ExecuteAsync(model, context.TraceIdentifier, cancellationToken);
    loggerFactory.CreateLogger("Audit").LogInformation("AuditEvent {@AuditEvent}", new AuditEvent(
        Guid.NewGuid(), DateTimeOffset.UtcNow, "ServiceRequestCreated", context.TraceIdentifier,
        request.Id, request.CurrentWorkflowStageId?.ToString(), "Success", null, request.Status.ToString()));
    return Results.Ok(new { request.Id, request.ServiceCode, request.Status, request.CreatedAt, request.UpdatedAt,
        request.CurrentWorkflowStageId, request.OrganizationTrackingId });
});

app.MapGet("/service-requests/{id:guid}", async (Guid id, IServiceRequestRepository repository, CancellationToken cancellationToken) =>
{
    var request = await repository.GetByIdAsync(id, cancellationToken);
    return request is null ? Results.NotFound() : Results.Ok(new { request.Id, request.ServiceCode, request.Status,
        request.CreatedAt, request.UpdatedAt, request.CurrentWorkflowStageId, request.OrganizationTrackingId, request.OrganizationStatus });
});

app.MapGet("/service-requests/{id:guid}/workflow-stages", async (Guid id, IServiceRequestRepository repository, CancellationToken cancellationToken) =>
{
    var request = await repository.GetByIdAsync(id, cancellationToken);
    if (request is null) return Results.NotFound();
    var stages = await repository.GetWorkflowStagesAsync(id, cancellationToken);
    return Results.Ok(stages.Select(stage => new { stage.Id, stage.ServiceRequestId, stage.StageCode, stage.CreatedAt, stage.CompletedAt }));
});

app.MapGet("/service-requests/{id:guid}/organization-status", async (
    Guid id, IServiceRequestRepository repository, IOrganizationIntegrationService organizationIntegrationService,
    HttpContext context, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
{
    var request = await repository.GetByIdAsync(id, cancellationToken);
    if (request is null) return Results.NotFound();
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
        loggerFactory.CreateLogger("Audit").LogInformation("AuditEvent {@AuditEvent}", new AuditEvent(
            Guid.NewGuid(), DateTimeOffset.UtcNow, "WorkflowStageCompleted", context.TraceIdentifier,
            request.Id, followUpStage.StageCode, "Success", followUpStage.StageCode, followUpStage.StageCode));
    }

    var resultStage = workflowStages.SingleOrDefault(stage => stage.StageCode == S01StageCode.ResultNotification.ToString());
    if (resultStage is null)
    {
        resultStage = new WorkflowStage(request.Id, S01StageCode.ResultNotification.ToString());
        await repository.AddWorkflowStageAsync(resultStage, cancellationToken);
        request.SetCurrentWorkflowStage(resultStage.Id);
        loggerFactory.CreateLogger("Audit").LogInformation("AuditEvent {@AuditEvent}", new AuditEvent(
            Guid.NewGuid(), DateTimeOffset.UtcNow, "WorkflowStageCreated", context.TraceIdentifier,
            request.Id, resultStage.StageCode, "Success", null, resultStage.StageCode));
    }

    await repository.SaveChangesAsync(cancellationToken);
    loggerFactory.CreateLogger("Audit").LogInformation("AuditEvent {@AuditEvent}", new AuditEvent(
        Guid.NewGuid(), DateTimeOffset.UtcNow, "OrganizationStatusReceived", context.TraceIdentifier,
        request.Id, resultStage.StageCode, "Success", previousOrganizationStatus, organizationStatus));

    return Results.Ok(new { request.Id, request.ServiceCode, request.Status, request.OrganizationTrackingId,
        request.OrganizationStatus, request.CurrentWorkflowStageId });
});

app.Run();

public sealed record OtpVerificationRequest(string ChallengeId, string Code);
