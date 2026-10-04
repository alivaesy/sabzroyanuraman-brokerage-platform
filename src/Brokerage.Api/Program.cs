using Brokerage.Application.UseCases;
using Brokerage.Domain.Enums;
using Brokerage.Application.Contracts;
using Brokerage.Application.Services;
using Brokerage.Application.Integration;
using Brokerage.Infrastructure.Integration;
using Brokerage.Infrastructure.Persistence;
using Brokerage.Api.Middleware;
using Brokerage.Application.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

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

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<BrokerageDbContext>();
    await DatabaseInitializer.InitializeAsync(dbContext);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.MapGet("/", () => Results.Ok(new
{
    service = "Brokerage.Api",
    status = "running"
}));

app.MapPost("/service-requests", (
    CreateServiceRequest useCase,
    ServiceCode serviceCode) =>
{
    var request = useCase.Execute(serviceCode, "INITIAL");

    return Results.Ok(new
    {
        request.Id,
        request.ServiceCode,
        request.Status,
        request.CreatedAt,
        request.UpdatedAt
    });
});

app.MapPost("/service-requests/s01", async (
    CreateS01ServiceRequest useCase,
    CreateS01RequestModel model,
    CancellationToken cancellationToken) =>
{
    var request = await useCase.ExecuteAsync(model, cancellationToken);

    return Results.Ok(new
    {
        request.Id,
        request.ServiceCode,
        request.Status,
        request.CreatedAt,
        request.UpdatedAt,
        request.CurrentWorkflowStageId,
        request.OrganizationTrackingId
    });
});

app.MapGet("/service-requests/{id:guid}", async (
    Guid id,
    IServiceRequestRepository repository,
    CancellationToken cancellationToken) =>
{
    var request = await repository.GetByIdAsync(id, cancellationToken);

    return request is null
        ? Results.NotFound()
        : Results.Ok(new
        {
            request.Id,
            request.ServiceCode,
            request.Status,
            request.CreatedAt,
            request.UpdatedAt,
            request.CurrentWorkflowStageId,
            request.OrganizationTrackingId
        });
});

app.MapGet("/service-requests/{id:guid}/organization-status", async (
    Guid id,
    IServiceRequestRepository repository,
    IOrganizationIntegrationService organizationIntegrationService,
    CancellationToken cancellationToken) =>
{
    var request = await repository.GetByIdAsync(id, cancellationToken);

    if (request is null)
        return Results.NotFound();

    if (string.IsNullOrWhiteSpace(request.OrganizationTrackingId))
    {
        return Results.BadRequest(new
        {
            message = "The service request has no organization tracking ID."
        });
    }

    var organizationStatus = await organizationIntegrationService.GetStatusAsync(
        request.OrganizationTrackingId,
        cancellationToken);

    request.SetOrganizationStatus(organizationStatus);
    await repository.SaveChangesAsync(cancellationToken);

    return Results.Ok(new
    {
        request.Id,
        request.ServiceCode,
        request.Status,
        request.OrganizationTrackingId,
        request.OrganizationStatus
    });
});

app.Run();
