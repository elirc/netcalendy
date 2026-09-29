using MediatR;
using NetCalendy.Application.Common;
using NetCalendy.Domain.Common;
using NetCalendy.Domain.Repositories;
using NetCalendy.Domain.Schedules;
using NetCalendy.Domain.Tenants;
using NetCalendy.Domain.Users;

namespace NetCalendy.Application.Schedules.Commands.CreateSchedule;

public sealed class CreateScheduleHandler(
    IScheduleRepository schedules,
    ICurrentUserService currentUser,
    ICurrentTenantService currentTenant,
    IUnitOfWork uow)
    : IRequestHandler<CreateScheduleCommand, Result<CreateScheduleResponse>>
{
    public async Task<Result<CreateScheduleResponse>> Handle(CreateScheduleCommand cmd, CancellationToken ct)
    {
        var tenantId = TenantId.From(currentTenant.TenantId!.Value);
        var userId = UserId.From(currentUser.UserId!.Value);

        var result = Schedule.Create(tenantId, userId, cmd.Name, cmd.Timezone);
        if (result.IsFailure) return Result.Failure<CreateScheduleResponse>(result.Error);

        var schedule = result.Value;

        if (cmd.Rules is { Count: > 0 })
        {
            var setResult = schedule.SetRules(cmd.Rules.Select(r => (r.DayOfWeek, r.Start, r.End)));
            if (setResult.IsFailure) return Result.Failure<CreateScheduleResponse>(setResult.Error);
        }

        schedules.Add(schedule);
        await uow.SaveChangesAsync(ct);

        return Result.Success(new CreateScheduleResponse(schedule.Id.Value, schedule.Name, schedule.Timezone));
    }
}
