using Brokerage.Application.UseCases;
using Brokerage.Domain.Enums;
using Brokerage.Application.Contracts;
using Brokerage.Application.Services;
using Brokerage.Application.Integration;
using Brokerage.Infrastructure.Integration;
using Brokerage.Api.Middleware;
using Brokerage.Application.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddScoped<CreateServiceRequest>();
builder.Services.AddScoped<CreateS01ServiceRequest>();
builder.Services.AddScoped<WorkflowService>();
builder.Services.AddScoped<IOrganizationIntegrationService, OrganizationIntegrationService>();
builder.Services.AddScoped<OrganizationRetryPolicy>();
builder.Services.AddScoped<OrganizationRetryOptions>();
builder.Services.AddScoped<OrganizationTimeoutOptions>();
builder.Services.AddScoped<OrganizationRetryExecutor>();
builder.Services.AddScoped<
    Brokerage.Application.Integration.IOrganizationApiClient,
    MockOrganizationApiClient>();
builder.Services.AddScoped<IIdentityVerificationService, IdentityVerificationService>();
builder.Services.AddScoped<ISanaClient, MockSanaClient>();
builder.Services.AddScoped<IShahkarClient, MockShahkarClient>();

var app = builder.Build();

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
    CreateS01RequestModel model) =>
{
    var request = await useCase.ExecuteAsync(
        model);

    return Results.Ok(new
    {
        request.Id,
        request.ServiceCode,
        request.Status,
        request.CreatedAt,
        request.UpdatedAt,
        request.CurrentWorkflowStageId
    });
});
app.Run();
