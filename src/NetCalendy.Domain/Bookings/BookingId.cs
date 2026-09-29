namespace NetCalendy.Domain.Bookings;

public record BookingId(Guid Value)
{
    public static BookingId New() => new(Guid.NewGuid());
    public static BookingId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
