using MediatR;
using NetCalendy.Domain.Common;

namespace NetCalendy.Application.Tenants.Commands.CreateTenant;

public sealed record CreateTenantCommand(string Name, string Slug) : IRequest<Result<CreateTenantResponse>>;

public sealed record CreateTenantResponse(Guid TenantId, string Name, string Slug);
