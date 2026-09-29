using MediatR;
using NetCalendy.Application.Common;
using NetCalendy.Domain.Common;
using NetCalendy.Domain.Repositories;
using NetCalendy.Domain.Tenants;
using NetCalendy.Domain.Users;

namespace NetCalendy.Application.Users.Commands.RegisterUser;

public sealed class RegisterUserHandler(
    IUserRepository users,
    ITenantRepository tenants,
    IUnitOfWork uow)
    : IRequestHandler<RegisterUserCommand, Result<RegisterUserResponse>>
{
    public async Task<Result<RegisterUserResponse>> Handle(RegisterUserCommand cmd, CancellationToken ct)
    {
        var tenantId = TenantId.From(cmd.TenantId);
        var tenant = await tenants.GetByIdAsync(tenantId, ct);
        if (tenant is null)
            return Result.Failure<RegisterUserResponse>(Error.NotFound("Tenant", cmd.TenantId));

        if (await users.EmailExistsInTenantAsync(cmd.Email, tenantId, ct))
            return Result.Failure<RegisterUserResponse>(
                Error.Conflict("User.EmailTaken", $"'{cmd.Email}' is already registered in this organization."));

        var result = User.Create(tenantId, cmd.Email, cmd.Name, cmd.Timezone, cmd.Role);
        if (result.IsFailure) return Result.Failure<RegisterUserResponse>(result.Error);

        users.Add(result.Value);
        await uow.SaveChangesAsync(ct);

        return Result.Success(new RegisterUserResponse(result.Value.Id.Value, result.Value.Email.Value, result.Value.Name));
    }
}
