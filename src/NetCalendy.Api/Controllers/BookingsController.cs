using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetCalendy.Api.Extensions;
using NetCalendy.Application.Bookings.Commands.CancelBooking;
using NetCalendy.Application.Bookings.Commands.CreateBooking;
using NetCalendy.Application.Bookings.Queries.GetBooking;
using NetCalendy.Application.Bookings.Queries.ListBookings;

namespace NetCalendy.Api.Controllers;

[ApiController]
[Route("api/bookings")]
public sealed class BookingsController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Public endpoint — invitees book without authentication.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CreateBookingResponse), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<IActionResult> Create([FromBody] CreateBookingCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.ToCreatedResult(nameof(GetById), new { id = result.IsSuccess ? result.Value.BookingId : Guid.Empty });
    }

    [HttpGet("{id:guid}", Name = nameof(GetById))]
    [Authorize]
    [ProducesResponseType(typeof(BookingDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetBookingQuery(id), ct);
        return result.ToActionResult();
    }

    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<BookingDto>), 200)]
    public async Task<IActionResult> List(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        var result = await mediator.Send(new ListBookingsQuery(
            from ?? DateTimeOffset.UtcNow.AddMonths(-1),
            to ?? DateTimeOffset.UtcNow.AddMonths(3)), ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Public endpoint — invitees or hosts cancel using the cancellation token.
    /// </summary>
    [HttpPost("cancel")]
    [AllowAnonymous]
    [ProducesResponseType(200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<IActionResult> Cancel([FromBody] CancelBookingRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CancelBookingCommand(request.CancellationToken, request.Reason), ct);
        return result.ToActionResult();
    }
}

public sealed record CancelBookingRequest(string CancellationToken, string? Reason);
