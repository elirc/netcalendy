using FluentAssertions;
using NSubstitute;
using NetCalendy.Application.Common;
using NetCalendy.Application.EventTypes.Commands.CreateEventType;
using NetCalendy.Application.Tests.Common;
using NetCalendy.Domain.EventTypes;
using NetCalendy.Domain.Repositories;
using NetCalendy.Domain.Tenants;
using NetCalendy.Domain.Users;
using Xunit;

namespace NetCalendy.Application.Tests.EventTypes;

public sealed class CreateEventTypeHandlerTests
{
    private readonly IEventTypeRepository _eventTypes = Substitute.For<IEventTypeRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    private CreateEventTypeHandler CreateHandler() => new(
        _eventTypes,
        new MockCurrentUserService(_userId),
        new MockCurrentTenantService(_tenantId),
        _uow);

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateEventType()
    {
        _eventTypes.SlugExistsForUserAsync(Arg.Any<string>(), Arg.Any<UserId>(), Arg.Any<TenantId>())
            .Returns(false);

        var command = new CreateEventTypeCommand("30 Min Chat", "30-min-chat", 30, "A quick chat", "#FF5733");
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("30 Min Chat");
        result.Value.DurationMinutes.Should().Be(30);
        _eventTypes.Received(1).Add(Arg.Any<EventType>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSlugAlreadyExists_ShouldReturnConflict()
    {
        _eventTypes.SlugExistsForUserAsync(Arg.Any<string>(), Arg.Any<UserId>(), Arg.Any<TenantId>())
            .Returns(true);

        var command = new CreateEventTypeCommand("30 Min Chat", "30-min-chat", 30, null);
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("EventType.SlugTaken");
        _eventTypes.DidNotReceive().Add(Arg.Any<EventType>());
    }
}
