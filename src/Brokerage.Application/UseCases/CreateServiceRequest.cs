using Brokerage.Domain.Entities;
using Brokerage.Domain.Enums;

namespace Brokerage.Application.UseCases;

public class CreateServiceRequest
{
    public ServiceRequest Execute(ServiceCode serviceCode)
    {
        return new ServiceRequest(serviceCode);
    }
}
