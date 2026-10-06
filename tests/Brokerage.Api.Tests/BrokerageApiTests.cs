using Brokerage.Application.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using Brokerage.Application.Integration;
using IntegrationOrganizationApiClient = Brokerage.Application.Integration.IOrganizationApiClient;
using Brokerage.Application.Contracts;
using Brokerage.Domain.Entities;
using Brokerage.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Brokerage.Api.Tests;

public class BrokerageApiTests
{
    [Fact]
    public async Task RootEndpoint_ReturnsSuccess()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = await CreateVerifiedApplicantClientAsync(application);

        var response = await client.GetAsync("/");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task OtpChallenge_RateLimit_ReturnsTooManyRequestsAfterFiveRequests()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", "RATE-LIMIT-USER");
        client.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.Applicant.ToString());

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var response = await client.PostAsync("/identity/otp/challenges", content: null);
            Assert.NotEqual(System.Net.HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        var limitedResponse = await client.PostAsync("/identity/otp/challenges", content: null);

        Assert.Equal(System.Net.HttpStatusCode.TooManyRequests, limitedResponse.StatusCode);
    }

    [Fact]
    public async Task CreateS01_AnonymousUser_ReturnsUnauthorized()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();
        using var content = new StringContent(
            """{"nationalIdentifier":"1234567891"}""",
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("/service-requests/s01", content);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateS01_ApplicantWithoutVerifiedIdentity_ReturnsForbidden()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", "unverified-applicant");
        client.DefaultRequestHeaders.Add("X-Test-User-Role", "Applicant");
        using var content = new StringContent(
            """{"nationalIdentifier":"1234567891"}""",
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("/service-requests/s01", content);

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateS01_WithValidTestIdentity_ReturnsSuccess()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = await CreateVerifiedApplicantClientAsync(application);

        var content = new StringContent(
            """
            {
                "nationalIdentifier": "1234567891"
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
    public async Task GetServiceRequest_ForCreatedS01_ReturnsPersistedRequest()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = await CreateVerifiedApplicantClientAsync(application);

        var content = new StringContent(
            """
            {
                "nationalIdentifier": "1234567891"
            }
            """,
            System.Text.Encoding.UTF8,
            "application/json");

        var createResponse = await client.PostAsync(
            "/service-requests/s01",
            content);

        Assert.Equal(
            System.Net.HttpStatusCode.OK,
            createResponse.StatusCode);

        var created =
            await createResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

        var requestId = Guid.Parse(
            created.GetProperty("id").GetString()!);

        var response = await client.GetAsync(
            $"/service-requests/{requestId}");

        Assert.Equal(
            System.Net.HttpStatusCode.OK,
            response.StatusCode);

        var request =
            await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

        Assert.Equal(
            requestId,
            Guid.Parse(request.GetProperty("id").GetString()!));
        Assert.Equal(
            (int)RequestStatus.WaitingForOrganization,
            request.GetProperty("status").GetInt32());
        var trackingId =
            request.GetProperty("organizationTrackingId").GetString();

        Assert.NotNull(trackingId);
        Assert.StartsWith("MOCK-ORG-", trackingId);
        Assert.True(
            Guid.TryParseExact(
                trackingId["MOCK-ORG-".Length..],
                "N",
                out _));
        Assert.Equal(
            System.Text.Json.JsonValueKind.Null,
            request.GetProperty("organizationStatus").ValueKind);
    }

    [Fact]
    public async Task GetServiceRequest_WhenRequestDoesNotExist_ReturnsNotFound()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = await CreateVerifiedApplicantClientAsync(application);

        var response = await client.GetAsync(
            $"/service-requests/{Guid.NewGuid()}");

        Assert.Equal(
            System.Net.HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task GetWorkflowStages_ForCreatedS01_ReturnsPersistedStagesInOrder()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = await CreateVerifiedApplicantClientAsync(application);

        var content = new StringContent(
            """
            {
                "nationalIdentifier": "1234567891"
            }
            """,
            System.Text.Encoding.UTF8,
            "application/json");

        var createResponse = await client.PostAsync(
            "/service-requests/s01",
            content);

        Assert.Equal(
            System.Net.HttpStatusCode.OK,
            createResponse.StatusCode);

        var created =
            await createResponse.Content.ReadFromJsonAsync<
                System.Text.Json.JsonElement>();

        var requestId =
            Guid.Parse(created.GetProperty("id").GetString()!);

        var currentStageId =
            Guid.Parse(
                created.GetProperty("currentWorkflowStageId").GetString()!);

        var response = await client.GetAsync(
            $"/service-requests/{requestId}/workflow-stages");

        Assert.Equal(
            System.Net.HttpStatusCode.OK,
            response.StatusCode);

        var stages =
            await response.Content.ReadFromJsonAsync<
                System.Text.Json.JsonElement[]>();

        Assert.NotNull(stages);
        Assert.Equal(3, stages!.Length);

        Assert.Equal(
            "IdentityVerification",
            stages[0].GetProperty("stageCode").GetString());
        Assert.Equal(
            requestId,
            Guid.Parse(
                stages[0].GetProperty("serviceRequestId").GetString()!));
        Assert.NotEqual(
            System.Text.Json.JsonValueKind.Null,
            stages[0].GetProperty("completedAt").ValueKind);

        Assert.Equal(
            "OrganizationSubmission",
            stages[1].GetProperty("stageCode").GetString());
        Assert.NotEqual(
            System.Text.Json.JsonValueKind.Null,
            stages[1].GetProperty("completedAt").ValueKind);

        Assert.Equal(
            "OrganizationFollowUp",
            stages[2].GetProperty("stageCode").GetString());
        Assert.Equal(
            System.Text.Json.JsonValueKind.Null,
            stages[2].GetProperty("completedAt").ValueKind);
        Assert.Equal(
            currentStageId,
            Guid.Parse(stages[2].GetProperty("id").GetString()!));
    }

    [Fact]
    public async Task GetWorkflowStages_WhenRequestDoesNotExist_ReturnsNotFound()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = await CreateVerifiedApplicantClientAsync(application);

        var response = await client.GetAsync(
            $"/service-requests/{Guid.NewGuid()}/workflow-stages");

        Assert.Equal(
            System.Net.HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateS01_WithInvalidTestIdentity_ReturnsBadRequest()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = await CreateVerifiedApplicantClientAsync(application);

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
        using var client = await CreateVerifiedApplicantClientAsync(application);

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
                            IntegrationOrganizationApiClient,
                            FailingOrganizationApiClient>();
                    });
                });

        using var client = await CreateVerifiedApplicantClientAsync(application);

        var content = new StringContent(
            """
            {
                "nationalIdentifier": "1234567891"
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


    [Fact]
    public async Task GetOrganizationStatus_WhenRequestDoesNotExist_ReturnsNotFound()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = await CreateVerifiedApplicantClientAsync(application);

        var response = await client.GetAsync(
            $"/service-requests/{Guid.NewGuid()}/organization-status");

        Assert.Equal(
            System.Net.HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task GetOrganizationStatus_AdvancesWorkflowToResultNotification()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = await CreateVerifiedApplicantClientAsync(application);

        var content = new StringContent(
            """
            {
                "nationalIdentifier": "1234567891"
            }
            """,
            System.Text.Encoding.UTF8,
            "application/json");

        var createResponse = await client.PostAsync(
            "/service-requests/s01",
            content);

        Assert.Equal(
            System.Net.HttpStatusCode.OK,
            createResponse.StatusCode);

        var created =
            await createResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

        var requestId = Guid.Parse(
            created.GetProperty("id").GetString()!);

        var statusResponse = await client.GetAsync(
            $"/service-requests/{requestId}/organization-status");

        Assert.Equal(
            System.Net.HttpStatusCode.OK,
            statusResponse.StatusCode);

        var statusJson =
            await statusResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

        Assert.Equal(
            "MockStatus",
            statusJson.GetProperty("organizationStatus").GetString());

        var persistedRequestResponse = await client.GetAsync(
            $"/service-requests/{requestId}");

        Assert.Equal(
            System.Net.HttpStatusCode.OK,
            persistedRequestResponse.StatusCode);

        var persistedRequest =
            await persistedRequestResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

        Assert.Equal(
            "MockStatus",
            persistedRequest.GetProperty("organizationStatus").GetString());

        var workflowResponse = await client.GetAsync(
            $"/service-requests/{requestId}/workflow-stages");

        Assert.Equal(
            System.Net.HttpStatusCode.OK,
            workflowResponse.StatusCode);

        var stages =
            await workflowResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement[]>();

        Assert.NotNull(stages);
        Assert.Equal(4, stages!.Length);

        Assert.Equal(
            "OrganizationFollowUp",
            stages[2].GetProperty("stageCode").GetString());
        Assert.NotEqual(
            System.Text.Json.JsonValueKind.Null,
            stages[2].GetProperty("completedAt").ValueKind);

        Assert.Equal(
            "ResultNotification",
            stages[3].GetProperty("stageCode").GetString());
        Assert.Equal(
            System.Text.Json.JsonValueKind.Null,
            stages[3].GetProperty("completedAt").ValueKind);

        Assert.Equal(
            Guid.Parse(stages[3].GetProperty("id").GetString()!),
            Guid.Parse(statusJson.GetProperty("currentWorkflowStageId").GetString()!));
    }

    [Fact]
    public async Task GetOrganizationStatus_WhenOrganizationApiFails_DoesNotAdvanceWorkflow()
    {
        await using var application =
            new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        services.AddScoped<
                            IntegrationOrganizationApiClient,
                            FailingOrganizationStatusApiClient>();
                    });
                });

        using var client = await CreateVerifiedApplicantClientAsync(application);

        var content = new StringContent(
            """
            {
                "nationalIdentifier": "1234567891"
            }
            """,
            System.Text.Encoding.UTF8,
            "application/json");

        var createResponse = await client.PostAsync(
            "/service-requests/s01",
            content);

        Assert.Equal(
            System.Net.HttpStatusCode.OK,
            createResponse.StatusCode);

        var created =
            await createResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

        var requestId = Guid.Parse(
            created.GetProperty("id").GetString()!);

        var statusResponse = await client.GetAsync(
            $"/service-requests/{requestId}/organization-status");

        Assert.Equal(
            System.Net.HttpStatusCode.BadRequest,
            statusResponse.StatusCode);

        var errorResponse =
            await statusResponse.Content.ReadFromJsonAsync<
                Brokerage.Application.Models.ErrorResponse>();

        Assert.NotNull(errorResponse);
        Assert.Equal("BROKERAGE_ERROR", errorResponse.Code);
        Assert.Equal(
            "Mock organization status request failed.",
            errorResponse.Message);

        var workflowResponse = await client.GetAsync(
            $"/service-requests/{requestId}/workflow-stages");

        Assert.Equal(
            System.Net.HttpStatusCode.OK,
            workflowResponse.StatusCode);

        var stages =
            await workflowResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement[]>();

        Assert.NotNull(stages);
        Assert.Equal(3, stages!.Length);
        Assert.Equal(
            "OrganizationFollowUp",
            stages[2].GetProperty("stageCode").GetString());
        Assert.Equal(
            System.Text.Json.JsonValueKind.Null,
            stages[2].GetProperty("completedAt").ValueKind);
    }

    [Fact]
    public async Task GetOrganizationStatus_WhenTrackingIdIsMissing_ReturnsBadRequest()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var scope = application.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IServiceRequestRepository>();

        var request = new ServiceRequest(ServiceCode.S01, "test-applicant");
        await repository.AddAsync(request);
        await repository.SaveChangesAsync();

        using var client = await CreateVerifiedApplicantClientAsync(application);

        var response = await client.GetAsync(
            $"/service-requests/{request.Id}/organization-status");

        Assert.Equal(
            System.Net.HttpStatusCode.BadRequest,
            response.StatusCode);

        var error =
            await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

        Assert.Equal(
            "The service request has no organization tracking ID.",
            error.GetProperty("message").GetString());
    }

    [Fact]
    public async Task GetOrganizationStatus_WhenCalledTwice_DoesNotDuplicateResultNotification()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = await CreateVerifiedApplicantClientAsync(application);

        var content = new StringContent(
            """
            {
                "nationalIdentifier": "1234567891"
            }
            """,
            System.Text.Encoding.UTF8,
            "application/json");

        var createResponse = await client.PostAsync(
            "/service-requests/s01",
            content);

        Assert.Equal(
            System.Net.HttpStatusCode.OK,
            createResponse.StatusCode);

        var created =
            await createResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

        var requestId = Guid.Parse(
            created.GetProperty("id").GetString()!);

        var firstStatusResponse = await client.GetAsync(
            $"/service-requests/{requestId}/organization-status");
        var secondStatusResponse = await client.GetAsync(
            $"/service-requests/{requestId}/organization-status");

        Assert.Equal(
            System.Net.HttpStatusCode.OK,
            firstStatusResponse.StatusCode);
        Assert.Equal(
            System.Net.HttpStatusCode.OK,
            secondStatusResponse.StatusCode);

        var firstStatus =
            await firstStatusResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var secondStatus =
            await secondStatusResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

        Assert.Equal(
            firstStatus.GetProperty("currentWorkflowStageId").GetString(),
            secondStatus.GetProperty("currentWorkflowStageId").GetString());
        Assert.Equal(
            "MockStatus",
            secondStatus.GetProperty("organizationStatus").GetString());

        var persistedRequestResponse = await client.GetAsync(
            $"/service-requests/{requestId}");

        Assert.Equal(
            System.Net.HttpStatusCode.OK,
            persistedRequestResponse.StatusCode);

        var persistedRequest =
            await persistedRequestResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

        Assert.Equal(
            secondStatus.GetProperty("currentWorkflowStageId").GetString(),
            persistedRequest.GetProperty("currentWorkflowStageId").GetString());
        Assert.Equal(
            "MockStatus",
            persistedRequest.GetProperty("organizationStatus").GetString());

        var workflowResponse = await client.GetAsync(
            $"/service-requests/{requestId}/workflow-stages");

        var stages =
            await workflowResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement[]>();

        Assert.NotNull(stages);
        Assert.Equal(4, stages!.Length);
        Assert.Equal(
            1,
            stages.Count(stage =>
                stage.GetProperty("stageCode").GetString() == "ResultNotification"));
    }

    [Fact]
    public async Task GetServiceRequest_AnonymousUser_ReturnsUnauthorized()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        var response = await client.GetAsync($"/service-requests/{Guid.NewGuid()}");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetServiceRequest_OwnerApplicant_ReturnsSuccess()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var ownerClient = await CreateVerifiedApplicantClientAsync(application);

        var content = new StringContent(
            """{"nationalIdentifier":"1234567891"}""",
            System.Text.Encoding.UTF8,
            "application/json");

        var createResponse = await ownerClient.PostAsync("/service-requests/s01", content);
        Assert.Equal(System.Net.HttpStatusCode.OK, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var requestId = Guid.Parse(created.GetProperty("id").GetString()!);

        var response = await ownerClient.GetAsync($"/service-requests/{requestId}");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetServiceRequest_DifferentApplicant_ReturnsForbidden()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var ownerClient = await CreateVerifiedApplicantClientAsync(application);

        var content = new StringContent(
            """{"nationalIdentifier":"1234567891"}""",
            System.Text.Encoding.UTF8,
            "application/json");

        var createResponse = await ownerClient.PostAsync("/service-requests/s01", content);
        Assert.Equal(System.Net.HttpStatusCode.OK, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var requestId = Guid.Parse(created.GetProperty("id").GetString()!);

        using var otherClient = application.CreateClient();
        otherClient.DefaultRequestHeaders.Add("X-Test-User-Id", "other-applicant");
        otherClient.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.Applicant.ToString());

        var response = await otherClient.GetAsync($"/service-requests/{requestId}");

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetServiceRequest_ExpertRole_CanAccessAnotherApplicantsRequest()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var ownerClient = await CreateVerifiedApplicantClientAsync(application);

        var content = new StringContent(
            """{"nationalIdentifier":"1234567891"}""",
            System.Text.Encoding.UTF8,
            "application/json");

        var createResponse = await ownerClient.PostAsync("/service-requests/s01", content);
        Assert.Equal(System.Net.HttpStatusCode.OK, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var requestId = Guid.Parse(created.GetProperty("id").GetString()!);

        using var expertClient = application.CreateClient();
        expertClient.DefaultRequestHeaders.Add("X-Test-User-Id", "expert-001");
        expertClient.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.Expert.ToString());

        var response = await expertClient.GetAsync($"/service-requests/{requestId}");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetWorkflowStages_DifferentApplicant_ReturnsForbidden()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var ownerClient = await CreateVerifiedApplicantClientAsync(application);

        var content = new StringContent(
            """{"nationalIdentifier":"1234567891"}""",
            System.Text.Encoding.UTF8,
            "application/json");

        var createResponse = await ownerClient.PostAsync("/service-requests/s01", content);
        Assert.Equal(System.Net.HttpStatusCode.OK, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var requestId = Guid.Parse(created.GetProperty("id").GetString()!);

        using var otherClient = application.CreateClient();
        otherClient.DefaultRequestHeaders.Add("X-Test-User-Id", "other-applicant");
        otherClient.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.Applicant.ToString());

        var response = await otherClient.GetAsync($"/service-requests/{requestId}/workflow-stages");

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetOrganizationStatus_DifferentApplicant_ReturnsForbidden()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var ownerClient = await CreateVerifiedApplicantClientAsync(application);

        var content = new StringContent(
            """{"nationalIdentifier":"1234567891"}""",
            System.Text.Encoding.UTF8,
            "application/json");

        var createResponse = await ownerClient.PostAsync("/service-requests/s01", content);
        Assert.Equal(System.Net.HttpStatusCode.OK, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var requestId = Guid.Parse(created.GetProperty("id").GetString()!);

        using var otherClient = application.CreateClient();
        otherClient.DefaultRequestHeaders.Add("X-Test-User-Id", "other-applicant");
        otherClient.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.Applicant.ToString());

        var response = await otherClient.GetAsync($"/service-requests/{requestId}/organization-status");

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<HttpClient> CreateVerifiedApplicantClientAsync(WebApplicationFactory<Program> application)
    {
        var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", "test-applicant");
        client.DefaultRequestHeaders.Add("X-Test-User-Role", "Applicant");

        using var scope = application.Services.CreateScope();
        var states = scope.ServiceProvider.GetRequiredService<IIdentityVerificationStateRepository>();
        await states.SaveResultAsync("test-applicant", true, DateTimeOffset.UtcNow);
        return client;
    }

    private sealed class FailingOrganizationStatusApiClient
        : IntegrationOrganizationApiClient
    {
        public Task<OrganizationApiResult> SubmitAsync(
            string serviceCode,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                OrganizationApiResult.Success(
                    trackingId: "MOCK-TRACKING-ID"));
        }

        public Task<OrganizationApiResult> GetStatusAsync(
            string trackingId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                OrganizationApiResult.Failure(
                    OrganizationIntegrationErrorType.ServerError,
                    "Mock organization status request failed."));
        }
    }

    private sealed class FailingOrganizationApiClient
        : IntegrationOrganizationApiClient
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
                    status: "MockStatus"));
        }
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsHealthyAndSecurityHeaders()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("healthy", json.GetProperty("status").GetString());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("camera=(), microphone=(), geolocation=()", response.Headers.GetValues("Permissions-Policy").Single());
    }

}
