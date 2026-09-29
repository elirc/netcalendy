using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetCalendy.Api.Extensions;
using NetCalendy.Application.EventTypes.Commands.CreateEventType;
using NetCalendy.Application.EventTypes.Commands.DeleteEventType;
using NetCalendy.Application.EventTypes.Commands.UpdateEventType;
using NetCalendy.Application.EventTypes.Queries.GetEventType;
using NetCalendy.Application.EventTypes.Queries.ListEventTypes;

namespace NetCalendy.Api.Controllers;

[ApiController]
[Route("api/event-types")]
[Authorize]
public sealed class EventTypesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EventTypeDto>), 200)]
    public async Task<IActionResult> List([FromQuery] Guid? ownerId, CancellationToken ct)
    {
        var result = await mediator.Send(new ListEventTypesQuery(ownerId), ct);
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}", Name = nameof(GetById))]
    [ProducesResponseType(typeof(EventTypeDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetEventTypeQuery(id), ct);
        return result.ToActionResult();
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreateEventTypeResponse), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<IActionResult> Create([FromBody] CreateEventTypeCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.ToCreatedResult(nameof(GetById), new { id = result.IsSuccess ? result.Value.EventTypeId : Guid.Empty });
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEventTypeRequest request, CancellationToken ct)
    {
        var command = new UpdateEventTypeCommand(id, request.Name, request.DurationMinutes, request.Description, request.Color);
        var result = await mediator.Send(command, ct);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteEventTypeCommand(id), ct);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }
}

public sealed record UpdateEventTypeRequest(string Name, int DurationMinutes, string? Description, string Color);
