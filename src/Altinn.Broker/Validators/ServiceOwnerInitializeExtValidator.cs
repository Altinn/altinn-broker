using Altinn.Broker.Models;
using Altinn.Broker.Models.ServiceOwner;

using FluentValidation;

namespace Altinn.Broker.Validators;

public class ServiceOwnerInitializeExtValidator : AbstractValidator<ServiceOwnerInitializeExt>
{
    public ServiceOwnerInitializeExtValidator()
    {
        RuleFor(serviceOwner => serviceOwner.Id).Matches(@"^\d{4}:\d{9}$").WithMessage("ServiceOwnerId should be on the Maskinporten form with countrycode:organizationnumber, for instance 0192:910753614");
    }
}
