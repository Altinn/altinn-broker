using Altinn.Broker.Models;

using FluentValidation;

namespace Altinn.Broker.Validators;

/// <summary>
/// Claass contining validation logic for the <see cref="EmailNotificationOrderRequestExt"/> model
/// </summary>
public class InitiateBrokerShipmentValidator : AbstractValidator<InitiateBrokerShipmentRequestExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InitiateBrokerShipmentValidator"/> class.
    /// </summary>
    public InitiateBrokerShipmentValidator()
    {
        RuleFor(shipment => shipment.Recipients)
            .NotEmpty()
            .WithMessage("One or more recipient is required.")
            .Must(recipients => recipients?.Exists(a => string.IsNullOrEmpty(a)) == false)
            .WithMessage("Cannot provide empty recipient.");

        RuleFor(shipment => shipment.ServiceCode).NotEmpty().WithMessage("ServiceCode must be defined for InitiateBrokerShipment.");
        RuleFor(shipment => shipment.ServiceEditionCode).NotEqual(0).WithMessage("ServiceEditionCode must be defined for InitiateBrokerShipment.");
        RuleFor(shipment => shipment.SendersReference).NotEmpty();
    }
}