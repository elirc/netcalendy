using NetCalendy.Domain.Common;

namespace NetCalendy.Domain.Users;

public sealed class Email : ValueObject
{
    private Email(string value) => Value = value;

    public string Value { get; }

    public static Result<Email> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<Email>(Error.Validation("Email.Empty", "Email cannot be empty."));

        value = value.Trim().ToLowerInvariant();

        if (!IsValid(value))
            return Result.Failure<Email>(Error.Validation("Email.Invalid", $"'{value}' is not a valid email address."));

        return Result.Success(new Email(value));
    }

    private static bool IsValid(string email) =>
        System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
