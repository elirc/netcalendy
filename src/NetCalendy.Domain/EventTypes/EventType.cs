using NetCalendy.Domain.Common;
using NetCalendy.Domain.Tenants;
using NetCalendy.Domain.Users;

namespace NetCalendy.Domain.EventTypes;

public sealed class EventType : AggregateRoot<EventTypeId>
{
    private EventType(
        EventTypeId id,
        TenantId tenantId,
        UserId ownerId,
        string name,
        string slug,
        Duration duration,
        string? description,
        string color)
        : base(id)
    {
        TenantId = tenantId;
        OwnerId = ownerId;
        Name = name;
        Slug = slug;
        Duration = duration;
        Description = description;
        Color = color;
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public TenantId TenantId { get; }
    public UserId OwnerId { get; }
    public string Name { get; private set; }
    public string Slug { get; private set; }
    public Duration Duration { get; private set; }
    public string? Description { get; private set; }
    public string Color { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; }

    public static Result<EventType> Create(
        TenantId tenantId,
        UserId ownerId,
        string name,
        string slug,
        int durationMinutes,
        string? description = null,
        string color = "#0069FF")
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<EventType>(Error.Validation("EventType.InvalidName", "Name cannot be empty."));

        if (string.IsNullOrWhiteSpace(slug))
            return Result.Failure<EventType>(Error.Validation("EventType.InvalidSlug", "Slug cannot be empty."));

        var durationResult = Duration.Create(durationMinutes);
        if (durationResult.IsFailure) return Result.Failure<EventType>(durationResult.Error);

        var et = new EventType(EventTypeId.New(), tenantId, ownerId, name.Trim(), slug, durationResult.Value, description, color);
        et.RaiseDomainEvent(new EventTypeCreatedEvent(et.Id, et.TenantId, et.OwnerId));
        return Result.Success(et);
    }

    public Result Update(string name, int durationMinutes, string? description, string color)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(Error.Validation("EventType.InvalidName", "Name cannot be empty."));

        var durationResult = Duration.Create(durationMinutes);
        if (durationResult.IsFailure) return Result.Failure(durationResult.Error);

        Name = name.Trim();
        Duration = durationResult.Value;
        Description = description;
        Color = color;
        return Result.Success();
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
