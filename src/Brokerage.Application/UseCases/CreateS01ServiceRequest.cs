using Brokerage.Application.Integration;
using Brokerage.Application.Services;
using Brokerage.Domain.Entities;
using Brokerage.Domain.Enums;
using Brokerage.Application.Exceptions;

namespace Brokerage.Application.UseCases;

public class CreateS01ServiceRequest
{
    private readonly WorkflowService _workflowService;
    private readonly IIdentityVerificationService _identityVerificationService;

    public CreateS01ServiceRequest(
        WorkflowService workflowService,
        IIdentityVerificationService identityVerificationService)
    {
        _workflowService = workflowService;
        _identityVerificationService = identityVerificationService;
    }

    public async Task<ServiceRequest> ExecuteAsync(
        string nationalIdentifier,
        CancellationToken cancellationToken = default)
    {
        var isVerified = await _identityVerificationService.VerifyAsync(
            nationalIdentifier,
            cancellationToken);

        if (!isVerified)
        {
            throw new BrokerageException(
                "Identity verification failed.");
        }

        var request = new ServiceRequest(ServiceCode.S01);

        _workflowService.CreateStage(
            request,
            S01StageCode.IdentityVerification.ToString());

        return request;
    }
}