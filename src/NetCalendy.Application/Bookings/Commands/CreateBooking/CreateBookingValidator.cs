using FluentValidation;

namespace NetCalendy.Application.Bookings.Commands.CreateBooking;

public sealed class CreateBookingValidator : AbstractValidator<CreateBookingCommand>
{
    public CreateBookingValidator()
    {
        RuleFor(x => x.EventTypeId).NotEmpty();
        RuleFor(x => x.HostUserId).NotEmpty();
        RuleFor(x => x.StartTime).GreaterThan(DateTimeOffset.UtcNow).WithMessage("Start time must be in the future.");
        RuleFor(x => x.InviteeName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.InviteeEmail).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.InviteeTimezone).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(1000).When(x => x.Notes is not null);
    }
}
