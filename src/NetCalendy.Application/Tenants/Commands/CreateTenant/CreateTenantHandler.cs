using MediatR;
using NetCalendy.Application.Common;
using NetCalendy.Domain.Common;
using NetCalendy.Domain.Repositories;
using NetCalendy.Domain.Tenants;

namespace NetCalendy.Application.Tenants.Commands.CreateTenant;

public sealed class CreateTenantHandler(
    ITenantRepository tenants,
    IUnitOfWork uow)
    : IRequestHandler<CreateTenantCommand, Result<CreateTenantResponse>>
{
    public async Task<Result<CreateTenantResponse>> Handle(CreateTenantCommand cmd, CancellationToken ct)
    {
        if (await tenants.SlugExistsAsync(cmd.Slug, ct))
            return Result.Failure<CreateTenantResponse>(
                Error.Conflict("Tenant.SlugTaken", $"Slug '{cmd.Slug}' is already in use."));

        var result = Tenant.Create(cmd.Name, cmd.Slug);
        if (result.IsFailure) return Result.Failure<CreateTenantResponse>(result.Error);

        tenants.Add(result.Value);
        await uow.SaveChangesAsync(ct);

        return Result.Success(new CreateTenantResponse(result.Value.Id.Value, result.Value.Name, result.Value.Slug));
    }
}
