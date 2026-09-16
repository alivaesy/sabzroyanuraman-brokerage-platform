using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using Brokerage.Application.Integration;
using Microsoft.Extensions.DependencyInjection;

namespace Brokerage.Api.Tests;

public class BrokerageApiTests
{
    [Fact]
    public async Task RootEndpoint_ReturnsSuccess()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        var response = await client.GetAsync("/");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task CreateS01_WithValidTestIdentity_ReturnsSuccess()
{
    await using var application = new WebApplicationFactory<Program>();
    using var client = application.CreateClient();

    var content = new StringContent(
        """
        {
            "nationalIdentifier": "TEST-123"
        }
        """,
        System.Text.Encoding.UTF8,
        "application/json");

    var response = await client.PostAsync(
        "/service-requests/s01",
        content);

    Assert.Equal(
        System.Net.HttpStatusCode.OK,
        response.StatusCode);

    var json =
        await response.Content.ReadFromJsonAsync<
            System.Text.Json.JsonElement>();

    Assert.True(json.TryGetProperty("id", out var id));
    Assert.Equal(
    System.Text.Json.JsonValueKind.String,
    id.ValueKind);

    Assert.True(
    Guid.TryParse(id.GetString(), out var requestId));

    Assert.NotEqual(
    Guid.Empty,
    requestId);

    Assert.True(json.TryGetProperty("serviceCode", out var serviceCode));
    Assert.Equal(
    System.Text.Json.JsonValueKind.Number,
    serviceCode.ValueKind);

    Assert.True(
    serviceCode.GetInt32() > 0);

    Assert.True(
    json.TryGetProperty("currentWorkflowStageId", out var stageId));

    Assert.Equal(
    System.Text.Json.JsonValueKind.String,
    stageId.ValueKind);

    Assert.True(
    Guid.TryParse(stageId.GetString(), out var workflowStageId));

    Assert.NotEqual(
    Guid.Empty,
    workflowStageId);


}
    [Fact]
    public async Task CreateS01_WithInvalidTestIdentity_ReturnsBadRequest()
{
    await using var application = new WebApplicationFactory<Program>();
    using var client = application.CreateClient();

    var content = new StringContent(
        """
        {
            "nationalIdentifier": "TEST-999"
        }
        """,
        System.Text.Encoding.UTF8,
        "application/json");

    var response = await client.PostAsync(
        "/service-requests/s01",
        content);

    Assert.Equal(
        System.Net.HttpStatusCode.BadRequest,
        response.StatusCode);
}
[Fact]
public async Task CreateS01_WithInvalidTestIdentity_ReturnsStandardErrorResponse()
{
    await using var application = new WebApplicationFactory<Program>();
    using var client = application.CreateClient();

    var content = new StringContent(
        """
        {
            "nationalIdentifier": "TEST-999"
        }
        """,
        System.Text.Encoding.UTF8,
        "application/json");

    var response = await client.PostAsync(
        "/service-requests/s01",
        content);

    Assert.Equal(
        System.Net.HttpStatusCode.BadRequest,
        response.StatusCode);

    var errorResponse =
        await response.Content.ReadFromJsonAsync<
            Brokerage.Application.Models.ErrorResponse>();

    Assert.NotNull(errorResponse);
    Assert.Equal("BROKERAGE_ERROR", errorResponse.Code);
    Assert.Equal(
        "Identity verification failed.",
        errorResponse.Message);
    Assert.False(
        string.IsNullOrWhiteSpace(errorResponse.CorrelationId));
}
[Fact]
    public async Task CreateS01_WhenOrganizationSubmissionFails_ReturnsStandardErrorResponse()
    {
        await using var application =
        new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddScoped<
                    IOrganizationApiClient,
                    FailingOrganizationApiClient>();
            });
        });

        using var client = application.CreateClient();

        var content = new StringContent(
            """
            {
                "nationalIdentifier": "TEST-123"
            }
            """,
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync(
            "/service-requests/s01",
            content);

        Assert.Equal(
            System.Net.HttpStatusCode.BadRequest,
            response.StatusCode);

        var errorResponse =
            await response.Content.ReadFromJsonAsync<
                Brokerage.Application.Models.ErrorResponse>();

        Assert.NotNull(errorResponse);
        Assert.Equal(
            "BROKERAGE_ERROR",
            errorResponse.Code);

        Assert.Equal(
            "Mock organization submission failed.",
            errorResponse.Message);

        Assert.False(
            string.IsNullOrWhiteSpace(
                errorResponse.CorrelationId));
    }
    private sealed class FailingOrganizationApiClient
        : IOrganizationApiClient
    {
        public Task<OrganizationApiResult> SubmitAsync(
            string serviceCode,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                OrganizationApiResult.Failure(
                    OrganizationIntegrationErrorType.ServerError,
                    "Mock organization submission failed."));
        }

        public Task<OrganizationApiResult> GetStatusAsync(
            string trackingId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                OrganizationApiResult.Success(
                    "MOCK-STATUS"));
        }
    }
}