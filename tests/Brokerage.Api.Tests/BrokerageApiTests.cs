using Brokerage.Application.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using Brokerage.Application.Integration;
using IntegrationOrganizationApiClient = Brokerage.Application.Integration.IOrganizationApiClient;
using Brokerage.Application.Contracts;
using Brokerage.Domain.Entities;
using Brokerage.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Brokerage.Application.Models;
using Brokerage.Infrastructure.Persistence;

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

    [Theory]
    [InlineData("S02", ServiceCode.S02)]
    [InlineData("S03", ServiceCode.S03)]
    public async Task CreateGenericServiceRequest_PersistsRequestAndInitialWorkflowStage(
        string serviceCodeQuery,
        ServiceCode expectedServiceCode)
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = await CreateVerifiedApplicantClientAsync(application);

        var createResponse = await client.PostAsync($"/service-requests?serviceCode={serviceCodeQuery}", content: null);

        Assert.Equal(System.Net.HttpStatusCode.OK, createResponse.StatusCode);
        Assert.Equal("no-store", createResponse.Headers.CacheControl?.ToString());
        var created = await createResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var requestId = Guid.Parse(created.GetProperty("id").GetString()!);
        var stageId = Guid.Parse(created.GetProperty("currentWorkflowStageId").GetString()!);
        Assert.Equal((int)RequestStatus.Created, created.GetProperty("status").GetInt32());
        Assert.Equal((int)expectedServiceCode, created.GetProperty("serviceCode").GetInt32());

        var requestResponse = await client.GetAsync($"/service-requests/{requestId}");
        Assert.Equal(System.Net.HttpStatusCode.OK, requestResponse.StatusCode);
        Assert.Equal("no-store", requestResponse.Headers.CacheControl?.ToString());
        var persistedRequest = await requestResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(requestId, Guid.Parse(persistedRequest.GetProperty("id").GetString()!));
        Assert.Equal((int)expectedServiceCode, persistedRequest.GetProperty("serviceCode").GetInt32());
        Assert.Equal(stageId, Guid.Parse(persistedRequest.GetProperty("currentWorkflowStageId").GetString()!));

        var stagesResponse = await client.GetAsync($"/service-requests/{requestId}/workflow-stages");
        Assert.Equal(System.Net.HttpStatusCode.OK, stagesResponse.StatusCode);
        Assert.Equal("no-store", stagesResponse.Headers.CacheControl?.ToString());
        var stages = await stagesResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement[]>();
        Assert.NotNull(stages);
        var stage = Assert.Single(stages!);
        Assert.Equal(stageId, Guid.Parse(stage.GetProperty("id").GetString()!));
        Assert.Equal("INITIAL", stage.GetProperty("stageCode").GetString());
    }

    [Theory]
    [InlineData("S02")]
    [InlineData("S03")]
    public async Task CreateGenericServiceRequest_ApplicantWithoutVerifiedIdentity_ReturnsForbidden(
        string serviceCodeQuery)
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", "unverified-generic-applicant");
        client.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.Applicant.ToString());

        var response = await client.PostAsync($"/service-requests?serviceCode={serviceCodeQuery}", content: null);

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Theory]
    [InlineData("S02")]
    [InlineData("S03")]
    public async Task GenericServiceRequest_DifferentApplicantCannotReadCaseOrStages(
        string serviceCodeQuery)
    {
        await using var application = new WebApplicationFactory<Program>();
        using var ownerClient = await CreateVerifiedApplicantClientAsync(application);

        var createResponse = await ownerClient.PostAsync($"/service-requests?serviceCode={serviceCodeQuery}", content: null);
        Assert.Equal(System.Net.HttpStatusCode.OK, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var requestId = created.GetProperty("id").GetString();

        using var otherApplicant = application.CreateClient();
        otherApplicant.DefaultRequestHeaders.Add("X-Test-User-Id", "different-generic-applicant");
        otherApplicant.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.Applicant.ToString());

        var requestResponse = await otherApplicant.GetAsync($"/service-requests/{requestId}");
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, requestResponse.StatusCode);
        Assert.Equal("no-store", requestResponse.Headers.CacheControl?.ToString());

        var stagesResponse = await otherApplicant.GetAsync($"/service-requests/{requestId}/workflow-stages");
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, stagesResponse.StatusCode);
        Assert.Equal("no-store", stagesResponse.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task CreateGenericServiceRequest_CannotBypassS01Workflow_ReturnsBadRequest()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = await CreateVerifiedApplicantClientAsync(application);

        var response = await client.PostAsync("/service-requests?serviceCode=S01", content: null);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateGenericServiceRequest_WithUnsupportedServiceCode_ReturnsBadRequest()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = await CreateVerifiedApplicantClientAsync(application);

        var response = await client.PostAsync("/service-requests?serviceCode=9999", content: null);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("S04")]
    [InlineData("S05")]
    public async Task CreateGenericServiceRequest_UnapprovedServiceCode_ReturnsBadRequest(
        string serviceCodeQuery)
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = await CreateVerifiedApplicantClientAsync(application);

        var response = await client.PostAsync($"/service-requests?serviceCode={serviceCodeQuery}", content: null);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());

        using var scope = application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrokerageDbContext>();
        Assert.Equal(0, await db.ServiceRequests.AsNoTracking().CountAsync());
        Assert.Equal(0, await db.WorkflowStages.AsNoTracking().CountAsync());
        Assert.Equal(0, await db.AuditEvents.AsNoTracking().CountAsync());
    }

    [Theory]
    [InlineData("S02")]
    [InlineData("S03")]
    public async Task OrganizationStatusRefresh_DoesNotAdvanceUnapprovedGenericServiceWorkflow(string serviceCodeQuery)
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = await CreateVerifiedApplicantClientAsync(application);

        var createResponse = await client.PostAsync($"/service-requests?serviceCode={serviceCodeQuery}", content: null);
        Assert.Equal(System.Net.HttpStatusCode.OK, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var requestId = Guid.Parse(created.GetProperty("id").GetString()!);

        using (var scope = application.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BrokerageDbContext>();
            var request = await db.ServiceRequests.SingleAsync(x => x.Id == requestId);
            request.SetOrganizationTrackingId("TEST-TRACKING-ID");
            await db.SaveChangesAsync();
        }

        var refreshResponse = await client.PostAsync($"/service-requests/{requestId}/organization-status", content: null);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, refreshResponse.StatusCode);
        Assert.Equal("no-store", refreshResponse.Headers.CacheControl?.ToString());

        using var verificationScope = application.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<BrokerageDbContext>();
        var persistedRequest = await verificationDb.ServiceRequests.AsNoTracking().SingleAsync(x => x.Id == requestId);
        Assert.Equal(RequestStatus.Created, persistedRequest.Status);
        Assert.Null(persistedRequest.OrganizationStatus);

        var stages = await verificationDb.WorkflowStages.AsNoTracking().Where(x => x.ServiceRequestId == requestId).ToListAsync();
        Assert.Equal("INITIAL", Assert.Single(stages).StageCode);
        Assert.Equal(0, await verificationDb.AuditEvents.AsNoTracking().CountAsync(x =>
            x.ServiceRequestId == requestId &&
            (x.EventType == "OrganizationStatusReceived" || x.EventType == "OrganizationStatusRefreshFailed")));
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
            "Organization API submission failed.",
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
    public async Task PostOrganizationStatus_AdvancesWorkflowToResultNotification()
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

        var statusResponse = await client.PostAsync(
            $"/service-requests/{requestId}/organization-status",
            content: null);

        Assert.Equal(
            System.Net.HttpStatusCode.OK,
            statusResponse.StatusCode);
        Assert.Equal("no-store", statusResponse.Headers.CacheControl?.ToString());

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
    public async Task GetOrganizationStatus_ConcurrentRefreshRejectsStaleWriterAndPreservesSingleTransition()
    {
        var organizationService = new ControlledOrganizationIntegrationService();
        await using var application =
            new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        services.AddSingleton<IOrganizationIntegrationService>(organizationService);
                    });
                });

        using var client = await CreateVerifiedApplicantClientAsync(application);
        using var content = new StringContent(
            """{"nationalIdentifier":"1234567891"}""",
            System.Text.Encoding.UTF8,
            "application/json");
        var createResponse = await client.PostAsync("/service-requests/s01", content);
        Assert.Equal(System.Net.HttpStatusCode.OK, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var requestId = Guid.Parse(created.GetProperty("id").GetString()!);

        var firstRefresh = client.PostAsync($"/service-requests/{requestId}/organization-status", content: null);
        var secondRefresh = client.GetAsync($"/service-requests/{requestId}/organization-status");
        await organizationService.SecondStatusCallStarted.WaitAsync(TimeSpan.FromSeconds(10));

        organizationService.ReleaseFirstStatusCall();

        // Do not assume which HTTP request reached the controlled integration service first.
        // Wait for the first invocation's local transaction to commit before releasing the stale writer.
        using (var pollingScope = application.Services.CreateScope())
        {
            var pollingDbContext = pollingScope.ServiceProvider.GetRequiredService<BrokerageDbContext>();
            ServiceRequest? persistedRequest = null;
            for (var attempt = 0; attempt < 100; attempt++)
            {
                pollingDbContext.ChangeTracker.Clear();
                persistedRequest = await pollingDbContext.ServiceRequests.AsNoTracking()
                    .SingleOrDefaultAsync(request => request.Id == requestId);
                if (persistedRequest?.OrganizationStatus == "FirstStatus")
                    break;

                await Task.Delay(TimeSpan.FromMilliseconds(25));
            }

            Assert.Equal("FirstStatus", persistedRequest?.OrganizationStatus);
        }

        organizationService.ReleaseSecondStatusCall();
        var responses = await Task.WhenAll(firstRefresh, secondRefresh).WaitAsync(TimeSpan.FromSeconds(10));
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];
        Assert.Equal(1, responses.Count(response => response.StatusCode == System.Net.HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(response => response.StatusCode == System.Net.HttpStatusCode.Conflict));
        Assert.All(responses, response => Assert.Contains(response.StatusCode, new[]
        {
            System.Net.HttpStatusCode.OK,
            System.Net.HttpStatusCode.Conflict
        }));

        using var scope = application.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IServiceRequestRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<BrokerageDbContext>();
        var request = await repository.GetByIdAsync(requestId);
        Assert.NotNull(request);
        Assert.Equal("FirstStatus", request!.OrganizationStatus);

        var stages = await repository.GetWorkflowStagesAsync(requestId);
        Assert.Single(stages, stage => stage.StageCode == "ResultNotification");
        Assert.Equal(1, await dbContext.AuditEvents.AsNoTracking().CountAsync(auditEvent =>
            auditEvent.ServiceRequestId == requestId && auditEvent.EventType == "OrganizationStatusReceived"));
        Assert.Equal(1, await dbContext.AuditEvents.AsNoTracking().CountAsync(auditEvent =>
            auditEvent.ServiceRequestId == requestId &&
            auditEvent.EventType == "WorkflowStageCreated" &&
            auditEvent.WorkflowStage == "ResultNotification"));
    }

    [Fact]
    public async Task GetOrganizationStatus_WhenAuditWriteFails_RollsBackAllLocalStatusChanges()
    {
        await using var application =
            new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        services.AddScoped<IAuditEventWriter, FailingOrganizationStatusAuditWriter>();
                    });
                });

        using var client = await CreateVerifiedApplicantClientAsync(application);
        using var content = new StringContent(
            """{"nationalIdentifier":"1234567891"}""",
            System.Text.Encoding.UTF8,
            "application/json");

        var createResponse = await client.PostAsync("/service-requests/s01", content);
        Assert.Equal(System.Net.HttpStatusCode.OK, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var requestId = Guid.Parse(created.GetProperty("id").GetString()!);

        int initialAuditEventCount;
        using (var baselineScope = application.Services.CreateScope())
        {
            var baselineDbContext = baselineScope.ServiceProvider.GetRequiredService<BrokerageDbContext>();
            initialAuditEventCount = await baselineDbContext.AuditEvents.AsNoTracking()
                .CountAsync(auditEvent => auditEvent.ServiceRequestId == requestId);
        }

        var statusResponse = await client.GetAsync($"/service-requests/{requestId}/organization-status");
        Assert.Equal(System.Net.HttpStatusCode.InternalServerError, statusResponse.StatusCode);

        using var scope = application.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IServiceRequestRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<BrokerageDbContext>();
        var request = await repository.GetByIdAsync(requestId);
        Assert.NotNull(request);
        Assert.Null(request!.OrganizationStatus);

        var stages = await repository.GetWorkflowStagesAsync(requestId);
        Assert.Equal(3, stages.Count);
        var followUp = Assert.Single(stages, stage => stage.StageCode == "OrganizationFollowUp");
        Assert.Null(followUp.CompletedAt);
        Assert.DoesNotContain(stages, stage => stage.StageCode == "ResultNotification");

        var persistedEvents = await dbContext.AuditEvents.AsNoTracking()
            .Where(auditEvent => auditEvent.ServiceRequestId == requestId)
            .ToListAsync();
        Assert.DoesNotContain(persistedEvents, auditEvent => auditEvent.EventType == "OrganizationStatusReceived");
        Assert.DoesNotContain(persistedEvents, auditEvent =>
            auditEvent.EventType == "WorkflowStageCreated" &&
            auditEvent.WorkflowStage == "ResultNotification");
        Assert.DoesNotContain(persistedEvents, auditEvent =>
            auditEvent.EventType == "WorkflowStageCompleted" &&
            auditEvent.WorkflowStage == "OrganizationFollowUp");
        Assert.Equal(initialAuditEventCount, persistedEvents.Count);
    }

    [Fact]
    public async Task OrganizationStatusRefresh_OtherApplicantCannotTriggerRemoteRefresh()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var ownerClient = await CreateVerifiedApplicantClientAsync(application);
        using var content = new StringContent(
            """{"nationalIdentifier":"1234567891"}""",
            System.Text.Encoding.UTF8,
            "application/json");
        var createResponse = await ownerClient.PostAsync("/service-requests/s01", content);
        Assert.Equal(System.Net.HttpStatusCode.OK, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var requestId = Guid.Parse(created.GetProperty("id").GetString()!);

        using (var scope = application.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BrokerageDbContext>();
            var request = await db.ServiceRequests.SingleAsync(x => x.Id == requestId);
            request.SetOrganizationTrackingId("OWNER-ONLY-TRACKING-ID");
            await db.SaveChangesAsync();
        }

        using var otherApplicant = application.CreateClient();
        otherApplicant.DefaultRequestHeaders.Add("X-Test-User-Id", "different-status-applicant");
        otherApplicant.DefaultRequestHeaders.Add(
            "X-Test-User-Role",
            Brokerage.Application.Authorization.UserRole.Applicant.ToString());

        var response = await otherApplicant.PostAsync(
            $"/service-requests/{requestId}/organization-status", content: null);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());

        using var verifyScope = application.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<BrokerageDbContext>();
        var persisted = await verifyDb.ServiceRequests.AsNoTracking().SingleAsync(x => x.Id == requestId);
        Assert.Null(persisted.OrganizationStatus);
        Assert.Equal(0, await verifyDb.AuditEvents.AsNoTracking().CountAsync(x =>
            x.ServiceRequestId == requestId &&
            (x.EventType == "OrganizationStatusReceived" ||
             x.EventType == "OrganizationStatusRefreshFailed")));
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
            "Organization API status request failed.",
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

        using var scope = application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrokerageDbContext>();
        var failureAudit = await db.AuditEvents.AsNoTracking().SingleAsync(
            x => x.EventType == "OrganizationStatusRefreshFailed" && x.ServiceRequestId == requestId);
        Assert.Equal("Failure", failureAudit.Outcome);
        Assert.Equal("ProviderError", failureAudit.NewState);
        Assert.DoesNotContain("Organization API status request failed.", failureAudit.NewState);
    }

    [Fact]
    public async Task GetOrganizationStatus_WhenProviderThrowsTransportException_AuditsFailureWithoutDetails()
    {
        await using var application = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddScoped<IntegrationOrganizationApiClient, ThrowingOrganizationStatusApiClient>();
                });
            });
        using var client = await CreateVerifiedApplicantClientAsync(application);

        var createContent = new StringContent(
            """{"nationalIdentifier":"1234567891"}""",
            System.Text.Encoding.UTF8,
            "application/json");
        var createResponse = await client.PostAsync("/service-requests/s01", createContent);
        Assert.Equal(System.Net.HttpStatusCode.OK, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var requestId = Guid.Parse(created.GetProperty("id").GetString()!);

        var response = await client.PostAsync($"/service-requests/{requestId}/organization-status", content: null);
        Assert.Equal(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);

        using var scope = application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrokerageDbContext>();
        var failureAudit = await db.AuditEvents.AsNoTracking().SingleAsync(x =>
            x.ServiceRequestId == requestId && x.EventType == "OrganizationStatusRefreshFailed");
        Assert.Equal("Failure", failureAudit.Outcome);
        Assert.Equal("ProviderError", failureAudit.NewState);
        Assert.DoesNotContain("private transport details", failureAudit.NewState);
        Assert.DoesNotContain("private transport details", failureAudit.PreviousState);

        var request = await db.ServiceRequests.AsNoTracking().SingleAsync(x => x.Id == requestId);
        Assert.Null(request.OrganizationStatus);
        var repository = scope.ServiceProvider.GetRequiredService<IServiceRequestRepository>();
        var stages = await repository.GetWorkflowStagesAsync(requestId);
        Assert.Equal(3, stages.Count);
        Assert.Equal("OrganizationFollowUp", stages[^1].StageCode);
        Assert.Null(stages[^1].CompletedAt);
    }

    [Fact]
    public async Task GetOrganizationStatus_WhenOrganizationApiReturnsEmptyStatus_DoesNotAdvanceWorkflow()
    {
        await using var application =
            new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        services.AddScoped<
                            IntegrationOrganizationApiClient,
                            EmptyOrganizationStatusApiClient>();
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

        var createResponse = await client.PostAsync("/service-requests/s01", content);
        Assert.Equal(System.Net.HttpStatusCode.OK, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var requestId = Guid.Parse(created.GetProperty("id").GetString()!);

        var statusResponse = await client.GetAsync(
            $"/service-requests/{requestId}/organization-status");

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, statusResponse.StatusCode);
        var error = await statusResponse.Content.ReadFromJsonAsync<Brokerage.Application.Models.ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("Organization API status request failed.", error.Message);

        var workflowResponse = await client.GetAsync(
            $"/service-requests/{requestId}/workflow-stages");
        Assert.Equal(System.Net.HttpStatusCode.OK, workflowResponse.StatusCode);
        var stages = await workflowResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement[]>();
        Assert.NotNull(stages);
        Assert.Equal(3, stages!.Length);
        Assert.Equal(
            System.Text.Json.JsonValueKind.Null,
            stages[2].GetProperty("completedAt").ValueKind);
        Assert.Equal(
            "OrganizationFollowUp",
            stages[2].GetProperty("stageCode").GetString());

        using var scope = application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrokerageDbContext>();
        var failureAudit = await db.AuditEvents.AsNoTracking().SingleAsync(
            x => x.EventType == "OrganizationStatusRefreshFailed" && x.ServiceRequestId == requestId);
        Assert.Equal("Failure", failureAudit.Outcome);
        Assert.Equal("ProviderError", failureAudit.NewState);
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
    public async Task OrganizationStatusRefresh_WhenRepeatedAcrossGetAndPost_DoesNotDuplicateResultNotification()
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
        var secondStatusResponse = await client.PostAsync(
            $"/service-requests/{requestId}/organization-status", content: null);
        var thirdStatusResponse = await client.GetAsync(
            $"/service-requests/{requestId}/organization-status");

        Assert.Equal(System.Net.HttpStatusCode.OK, firstStatusResponse.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, secondStatusResponse.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, thirdStatusResponse.StatusCode);
        Assert.Equal("no-store", firstStatusResponse.Headers.CacheControl?.ToString());
        Assert.Equal("no-store", secondStatusResponse.Headers.CacheControl?.ToString());
        Assert.Equal("no-store", thirdStatusResponse.Headers.CacheControl?.ToString());

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

        // Sequential refreshes should record each status response, but should not
        // recreate or re-complete workflow stages after the first successful refresh.
        using var scope = application.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BrokerageDbContext>();
        var auditEvents = await dbContext.AuditEvents.AsNoTracking()
            .Where(auditEvent => auditEvent.ServiceRequestId == requestId)
            .ToListAsync();

        Assert.Equal(3, auditEvents.Count(auditEvent =>
            auditEvent.EventType == "OrganizationStatusReceived"));
        Assert.Single(auditEvents, auditEvent =>
            auditEvent.EventType == "WorkflowStageCreated" &&
            auditEvent.WorkflowStage == "ResultNotification");
        Assert.Single(auditEvents, auditEvent =>
            auditEvent.EventType == "WorkflowStageCompleted" &&
            auditEvent.WorkflowStage == "OrganizationFollowUp");
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


    private sealed class ControlledOrganizationIntegrationService : IOrganizationIntegrationService
    {
        private readonly TaskCompletionSource _secondStatusCallStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseFirstStatusCall =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseSecondStatusCall =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _statusCallCount;

        public Task SecondStatusCallStarted => _secondStatusCallStarted.Task;

        public Task<string> SubmitAsync(string serviceCode, CancellationToken cancellationToken = default)
            => Task.FromResult("MOCK-TRACKING-ID");

        public Task<string> GetStatusAsync(string trackingId, CancellationToken cancellationToken = default)
        {
            var call = Interlocked.Increment(ref _statusCallCount);
            if (call == 1)
                return WaitForReleaseAsync(_releaseFirstStatusCall.Task, "FirstStatus", cancellationToken);

            if (call == 2)
            {
                _secondStatusCallStarted.TrySetResult();
                return WaitForReleaseAsync(_releaseSecondStatusCall.Task, "SecondStatus", cancellationToken);
            }

            throw new InvalidOperationException("Unexpected organization status call in concurrency test.");
        }

        public void ReleaseFirstStatusCall() => _releaseFirstStatusCall.TrySetResult();
        public void ReleaseSecondStatusCall() => _releaseSecondStatusCall.TrySetResult();

        private static async Task<string> WaitForReleaseAsync(
            Task release,
            string status,
            CancellationToken cancellationToken)
        {
            await release.WaitAsync(cancellationToken);
            return status;
        }
    }

    private sealed class FailingOrganizationStatusAuditWriter(
        BrokerageDbContext dbContext,
        ILogger<AuditEventWriter> logger) : IAuditEventWriter
    {
        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            if (auditEvent.EventType == "OrganizationStatusReceived")
                throw new InvalidOperationException("Simulated audit write failure.");

            return new AuditEventWriter(dbContext, logger).WriteAsync(auditEvent, cancellationToken);
        }
    }

    private sealed class ThrowingOrganizationStatusApiClient : IntegrationOrganizationApiClient
    {
        public Task<OrganizationApiResult> SubmitAsync(
            string serviceCode,
            CancellationToken cancellationToken = default)
            => Task.FromResult(OrganizationApiResult.Success(trackingId: "MOCK-TRACKING-ID"));

        public Task<OrganizationApiResult> GetStatusAsync(
            string trackingId,
            CancellationToken cancellationToken = default)
            => throw new HttpRequestException("private transport details");
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

    private sealed class EmptyOrganizationStatusApiClient
        : IntegrationOrganizationApiClient
    {
        public Task<OrganizationApiResult> SubmitAsync(
            string serviceCode,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OrganizationApiResult.Success(trackingId: "MOCK-TRACKING-ID"));
        }

        public Task<OrganizationApiResult> GetStatusAsync(
            string trackingId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OrganizationApiResult.Success());
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
    public async Task OperationalMetrics_AnonymousUser_ReturnsUnauthorized()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        var response = await client.GetAsync("/ops/metrics");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OperationalMetrics_ApplicantRole_ReturnsForbidden()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", "metrics-applicant");
        client.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.Applicant.ToString());

        var response = await client.GetAsync("/ops/metrics");

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OperationalMetrics_OrganizationObserverRole_ReturnsMetrics()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", "metrics-observer");
        client.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.OrganizationObserver.ToString());

        var response = await client.GetAsync("/ops/metrics");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task OperationalStatus_OrganizationObserverRole_ReturnsStatus()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", "status-observer");
        client.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.OrganizationObserver.ToString());

        var response = await client.GetAsync("/ops/status");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(json.GetProperty("ready").GetBoolean());
        Assert.Equal("operational", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task OperationalMetrics_AdministratorRole_ReturnsMetrics()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", "metrics-admin");
        client.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.Administrator.ToString());

        var response = await client.GetAsync("/ops/metrics");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OperationalMetrics_TechnicalSecurityRole_ReturnsResourceAndRequestMetrics()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", "metrics-security");
        client.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.TechnicalSecurity.ToString());

        var healthResponse = await client.GetAsync("/health");
        Assert.Equal(System.Net.HttpStatusCode.OK, healthResponse.StatusCode);

        var response = await client.GetAsync("/ops/metrics");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(json.GetProperty("totalRequestCount").GetInt64() >= 1);
        Assert.True(json.GetProperty("errorRatePercent").GetDouble() >= 0d);
        Assert.True(json.GetProperty("processWorkingSetBytes").GetInt64() > 0);
        Assert.True(json.GetProperty("managedMemoryBytes").GetInt64() > 0);
        Assert.True(json.GetProperty("threadCount").GetInt32() > 0);
        Assert.True(json.GetProperty("endpoints").GetArrayLength() >= 1);
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsHealthyAndSecurityHeaders()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        client.DefaultRequestHeaders.Add("X-Correlation-Id", "health-test-correlation");
        var response = await client.GetAsync("/health");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("health-test-correlation", response.Headers.GetValues("X-Correlation-Id").Single());
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("healthy", json.GetProperty("status").GetString());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("camera=(), microphone=(), geolocation=()", response.Headers.GetValues("Permissions-Policy").Single());
    }

}
