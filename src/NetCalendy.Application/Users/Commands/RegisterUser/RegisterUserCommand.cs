using MediatR;
using NetCalendy.Domain.Common;
using NetCalendy.Domain.Users;

namespace NetCalendy.Application.Users.Commands.RegisterUser;

public sealed record RegisterUserCommand(
    Guid TenantId,
    string Email,
    string Name,
    string Timezone,
    UserRole Role = UserRole.Member) : IRequest<Result<RegisterUserResponse>>;

public sealed record RegisterUserResponse(Guid UserId, string Email, string Name);
