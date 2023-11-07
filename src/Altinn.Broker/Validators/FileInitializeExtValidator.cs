using Altinn.Broker.Models;

using FluentValidation;

namespace Altinn.Broker.Validators;

public class FileInitializeExtValidator : AbstractValidator<FileInitalizeExt>
{
    public FileInitializeExtValidator()
    {
        RuleFor(file => file.Recipients)
            .NotEmpty()
            .WithMessage("One or more recipient is required.")
            .Must(recipients => recipients?.Exists(a => string.IsNullOrEmpty(a)) == false)
            .WithMessage("Cannot provide empty recipient.");

        RuleFor(file => file.Sender).NotEmpty().WithMessage("Sender must be defined for Request.");
        RuleFor(file => file.SendersFileReference).NotEmpty();
    }
}
