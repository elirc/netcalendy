using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NetCalendy.Application.Bookings.Commands.CreateBooking;
using NetCalendy.Domain.EventTypes;
using NetCalendy.Domain.Schedules;
using NetCalendy.Domain.Tenants;
using NetCalendy.Domain.Users;
using NetCalendy.Infrastructure.Persistence;
using NetCalendy.Integration.Tests.Fixtures;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace NetCalendy.Integration.Tests.BookingsEndpoints;

[Collection("Integration")]
public sealed class CreateBookingEndpointTests(IntegrationTestWebAppFactory factory)
    : IClassFixture<IntegrationTestWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task POST_bookings_WithValidSlot_Returns201()
    {
        var (tenantId, userId, eventTypeId) = await SeedData(factory);
        var token = JwtTokenFactory.GenerateToken(userId, tenantId);
        _client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var start = DateTimeOffset.UtcNow.AddDays(2);
        // Find next Monday at 9am
        while (start.DayOfWeek != DayOfWeek.Monday) start = start.AddDays(1);
        start = new DateTimeOffset(start.Year, start.Month, start.Day, 9, 0, 0, TimeSpan.Zero);

        var command = new CreateBookingCommand(
            eventTypeId, userId, start,
            "Alice Smith", "alice@example.com", "UTC");

        var response = await _client.PostAsJsonAsync("/api/bookings", command);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CreateBookingResponse>();
        body.Should().NotBeNull();
        body!.InviteeName.Should().Be("Alice Smith");
        body.CancellationToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task POST_bookings_WithInvalidEmail_Returns400()
    {
        var (tenantId, userId, eventTypeId) = await SeedData(factory);

        var command = new CreateBookingCommand(
            eventTypeId, userId, DateTimeOffset.UtcNow.AddDays(1),
            "Alice", "not-an-email", "UTC");

        var response = await _client.PostAsJsonAsync("/api/bookings", command);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_bookings_cancel_WithValidToken_Returns200()
    {
        var (tenantId, userId, eventTypeId) = await SeedData(factory);
        var start = GetNextMonday9am();

        var createResponse = await _client.PostAsJsonAsync("/api/bookings",
            new CreateBookingCommand(eventTypeId, userId, start, "Bob", "bob@test.com", "UTC"));
        var booking = await createResponse.Content.ReadFromJsonAsync<CreateBookingResponse>();

        var cancelResponse = await _client.PostAsJsonAsync("/api/bookings/cancel",
            new { CancellationToken = booking!.CancellationToken, Reason = "Changed my mind" });

        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static DateTimeOffset GetNextMonday9am()
    {
        var d = DateTimeOffset.UtcNow.AddDays(2);
        while (d.DayOfWeek != DayOfWeek.Monday) d = d.AddDays(1);
        return new DateTimeOffset(d.Year, d.Month, d.Day, 9, 0, 0, TimeSpan.Zero);
    }

    private static async Task<(Guid tenantId, Guid userId, Guid eventTypeId)> SeedData(IntegrationTestWebAppFactory f)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant = Tenant.Create("Test Org", "test-org").Value;
        var user = User.Create(tenant.Id, "host@test.com", "Host User", "UTC").Value;
        var eventType = EventType.Create(tenant.Id, user.Id, "30 Min", "30-min", 30).Value;
        var schedule = Schedule.Create(tenant.Id, user.Id, "Working Hours", "UTC").Value;

        db.Tenants.Add(tenant);
        db.Users.Add(user);
        db.EventTypes.Add(eventType);
        db.Schedules.Add(schedule);
        await db.SaveChangesAsync();

        return (tenant.Id.Value, user.Id.Value, eventType.Id.Value);
    }
}
