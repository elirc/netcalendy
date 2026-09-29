using MediatR;
using Microsoft.AspNetCore.Mvc;
using NetCalendy.Api.Extensions;
using NetCalendy.Application.Tenants.Commands.CreateTenant;

namespace NetCalendy.Api.Controllers;

[ApiController]
[Route("api/tenants")]
public sealed class TenantsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(CreateTenantResponse), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<IActionResult> Create([FromBody] CreateTenantCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.ToCreatedResult(nameof(Create), new { id = result.IsSuccess ? result.Value.TenantId : Guid.Empty });
    }
}
