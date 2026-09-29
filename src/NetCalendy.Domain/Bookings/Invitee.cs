using NetCalendy.Domain.Common;

namespace NetCalendy.Domain.Bookings;

public sealed class Invitee : ValueObject
{
    private Invitee(string name, string email, string? notes, string timezone)
    {
        Name = name;
        Email = email;
        Notes = notes;
        Timezone = timezone;
    }

    public string Name { get; }
    public string Email { get; }
    public string? Notes { get; }
    public string Timezone { get; }

    public static Result<Invitee> Create(string name, string email, string timezone, string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Invitee>(Error.Validation("Invitee.InvalidName", "Invitee name cannot be empty."));

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return Result.Failure<Invitee>(Error.Validation("Invitee.InvalidEmail", "A valid email is required."));

        if (string.IsNullOrWhiteSpace(timezone))
            return Result.Failure<Invitee>(Error.Validation("Invitee.InvalidTimezone", "Timezone cannot be empty."));

        return Result.Success(new Invitee(name.Trim(), email.Trim().ToLowerInvariant(), notes, timezone));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Email;
        yield return Name;
    }
}
