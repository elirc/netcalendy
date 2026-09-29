using MediatR;
using NetCalendy.Domain.Common;

namespace NetCalendy.Application.Schedules.Commands.CreateSchedule;

public sealed record AvailabilityRuleDto(DayOfWeek DayOfWeek, TimeOnly Start, TimeOnly End);

public sealed record CreateScheduleCommand(
    string Name,
    string Timezone,
    IReadOnlyList<AvailabilityRuleDto>? Rules = null) : IRequest<Result<CreateScheduleResponse>>;

public sealed record CreateScheduleResponse(Guid ScheduleId, string Name, string Timezone);
