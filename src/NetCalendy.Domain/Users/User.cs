using NetCalendy.Domain.Common;
using NetCalendy.Domain.Tenants;

namespace NetCalendy.Domain.Users;

public sealed class User : AggregateRoot<UserId>
{
    private User(UserId id, TenantId tenantId, Email email, string name, string timezone, UserRole role)
        : base(id)
    {
        TenantId = tenantId;
        Email = email;
        Name = name;
        Timezone = timezone;
        Role = role;
        CreatedAt = DateTimeOffset.UtcNow;
        IsActive = true;
    }

    public TenantId TenantId { get; }
    public Email Email { get; private set; }
    public string Name { get; private set; }
    public string Timezone { get; private set; }
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; }

    public static Result<User> Create(TenantId tenantId, string emailValue, string name, string timezone, UserRole role = UserRole.Member)
    {
        var emailResult = Email.Create(emailValue);
        if (emailResult.IsFailure) return Result.Failure<User>(emailResult.Error);

        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<User>(Error.Validation("User.InvalidName", "Name cannot be empty."));

        if (string.IsNullOrWhiteSpace(timezone))
            return Result.Failure<User>(Error.Validation("User.InvalidTimezone", "Timezone cannot be empty."));

        var user = new User(UserId.New(), tenantId, emailResult.Value, name.Trim(), timezone, role);
        user.RaiseDomainEvent(new UserRegisteredEvent(user.Id, user.TenantId, user.Email.Value));
        return Result.Success(user);
    }

    public Result UpdateProfile(string name, string timezone)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(Error.Validation("User.InvalidName", "Name cannot be empty."));

        Name = name.Trim();
        Timezone = timezone;
        return Result.Success();
    }

    public void ChangeRole(UserRole newRole) => Role = newRole;
    public void Deactivate() => IsActive = false;
}
