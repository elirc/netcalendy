using FluentValidation;

namespace NetCalendy.Application.EventTypes.Commands.CreateEventType;

public sealed class CreateEventTypeValidator : AbstractValidator<CreateEventTypeCommand>
{
    public CreateEventTypeValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(63)
            .Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$")
            .WithMessage("Slug must be lowercase alphanumeric with hyphens only.");
        RuleFor(x => x.DurationMinutes).GreaterThan(0).LessThanOrEqualTo(480);
        RuleFor(x => x.Color).Matches(@"^#[0-9A-Fa-f]{6}$").WithMessage("Color must be a valid hex color.");
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);
    }
}
