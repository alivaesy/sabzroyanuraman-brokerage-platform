using Brokerage.Application.UseCases;
using Brokerage.Domain.Enums;
using Brokerage.Application.Contracts;
using Brokerage.Application.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddScoped<CreateServiceRequest>();
builder.Services.AddScoped<WorkflowService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

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

app.Run();
