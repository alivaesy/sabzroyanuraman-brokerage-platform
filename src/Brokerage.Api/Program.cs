using Brokerage.Application.UseCases;
using Brokerage.Domain.Enums;
using Brokerage.Application.Contracts;
using Brokerage.Application.Services;
using Brokerage.Application.Integration;
using Brokerage.Infrastructure.Integration;
using Brokerage.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddScoped<CreateServiceRequest>();
builder.Services.AddScoped<CreateS01ServiceRequest>();
builder.Services.AddScoped<WorkflowService>();
builder.Services.AddScoped<IOrganizationIntegrationService, MockOrganizationIntegrationService>();
builder.Services.AddScoped<IIdentityVerificationService, MockIdentityVerificationService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
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
    string nationalIdentifier) =>
{
    var request = await useCase.ExecuteAsync(
        nationalIdentifier);

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
