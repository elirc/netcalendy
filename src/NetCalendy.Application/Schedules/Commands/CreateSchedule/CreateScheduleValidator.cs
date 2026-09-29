using FluentValidation;

namespace NetCalendy.Application.Schedules.Commands.CreateSchedule;

public sealed class CreateScheduleValidator : AbstractValidator<CreateScheduleCommand>
{
    public CreateScheduleValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Timezone).NotEmpty().MaximumLength(100);
    }
}
