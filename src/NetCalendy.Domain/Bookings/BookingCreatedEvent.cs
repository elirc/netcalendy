using NetCalendy.Domain.Common;
using NetCalendy.Domain.Tenants;
using NetCalendy.Domain.Users;

namespace NetCalendy.Domain.Bookings;

public sealed record BookingCreatedEvent(
    BookingId BookingId,
    TenantId TenantId,
    UserId HostId,
    string InviteeEmail) : DomainEvent;

public sealed record BookingCancelledEvent(
    BookingId BookingId,
    TenantId TenantId,
    UserId HostId,
    string InviteeEmail) : DomainEvent;
