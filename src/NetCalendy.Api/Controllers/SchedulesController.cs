using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetCalendy.Api.Extensions;
using NetCalendy.Application.Schedules.Commands.CreateSchedule;
using NetCalendy.Application.Schedules.Queries.GetAvailableSlots;

namespace NetCalendy.Api.Controllers;

[ApiController]
[Route("api/schedules")]
public sealed class SchedulesController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(CreateScheduleResponse), 201)]
    public async Task<IActionResult> Create([FromBody] CreateScheduleCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.ToCreatedResult("GetSchedule", new { id = result.IsSuccess ? result.Value.ScheduleId : Guid.Empty });
    }

    /// <summary>
    /// Public endpoint — no auth required. Returns available slots for booking.
    /// </summary>
    [HttpGet("available-slots")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<AvailableSlotDto>), 200)]
    public async Task<IActionResult> GetAvailableSlots(
        [FromQuery] Guid eventTypeId,
        [FromQuery] Guid hostUserId,
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        CancellationToken ct)
    {
        if (endDate < startDate) return BadRequest("endDate must be >= startDate.");
        if ((endDate.ToDateTime(TimeOnly.MinValue) - startDate.ToDateTime(TimeOnly.MinValue)).Days > 60)
            return BadRequest("Date range cannot exceed 60 days.");

        var result = await mediator.Send(new GetAvailableSlotsQuery(eventTypeId, hostUserId, startDate, endDate), ct);
        return result.ToActionResult();
    }
}
