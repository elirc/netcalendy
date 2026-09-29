namespace NetCalendy.Domain.EventTypes;

public record EventTypeId(Guid Value)
{
    public static EventTypeId New() => new(Guid.NewGuid());
    public static EventTypeId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
