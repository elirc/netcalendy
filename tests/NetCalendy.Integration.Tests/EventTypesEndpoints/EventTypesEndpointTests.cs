using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NetCalendy.Application.EventTypes.Commands.CreateEventType;
using NetCalendy.Application.EventTypes.Queries.GetEventType;
using NetCalendy.Domain.Tenants;
using NetCalendy.Domain.Users;
using NetCalendy.Infrastructure.Persistence;
using NetCalendy.Integration.Tests.Fixtures;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace NetCalendy.Integration.Tests.EventTypesEndpoints;

[Collection("Integration")]
public sealed class EventTypesEndpointTests(IntegrationTestWebAppFactory factory)
    : IClassFixture<IntegrationTestWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task POST_eventTypes_Unauthenticated_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/event-types",
            new CreateEventTypeCommand("30 Min Chat", "30-min-chat", 30, null));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_eventTypes_Authenticated_Returns201()
    {
        var (tenantId, userId) = await SeedTenantAndUser(factory);
        SetAuthHeader(userId, tenantId);

        var response = await _client.PostAsJsonAsync("/api/event-types",
            new CreateEventTypeCommand("30 Min Chat", "30-min-chat", 30, "Quick sync", "#0069FF"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CreateEventTypeResponse>();
        body!.Slug.Should().Be("30-min-chat");
    }

    [Fact]
    public async Task GET_eventTypes_id_Returns200ForExistingType()
    {
        var (tenantId, userId) = await SeedTenantAndUser(factory);
        SetAuthHeader(userId, tenantId);

        var createResp = await _client.PostAsJsonAsync("/api/event-types",
            new CreateEventTypeCommand("Deep Dive", "deep-dive", 60, null));
        var created = await createResp.Content.ReadFromJsonAsync<CreateEventTypeResponse>();

        var getResp = await _client.GetAsync($"/api/event-types/{created!.EventTypeId}");

        getResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await getResp.Content.ReadFromJsonAsync<EventTypeDto>();
        dto!.Name.Should().Be("Deep Dive");
    }

    [Fact]
    public async Task DELETE_eventTypes_id_Returns204()
    {
        var (tenantId, userId) = await SeedTenantAndUser(factory);
        SetAuthHeader(userId, tenantId);

        var createResp = await _client.PostAsJsonAsync("/api/event-types",
            new CreateEventTypeCommand("To Delete", "to-delete", 15, null));
        var created = await createResp.Content.ReadFromJsonAsync<CreateEventTypeResponse>();

        var delResp = await _client.DeleteAsync($"/api/event-types/{created!.EventTypeId}");

        delResp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private void SetAuthHeader(Guid userId, Guid tenantId)
    {
        var token = JwtTokenFactory.GenerateToken(userId, tenantId);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task<(Guid tenantId, Guid userId)> SeedTenantAndUser(IntegrationTestWebAppFactory f)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant = Tenant.Create("Events Test Org", $"events-org-{Guid.NewGuid():N[..8]}").Value;
        var user = User.Create(tenant.Id, $"{Guid.NewGuid():N}@test.com", "Host", "UTC").Value;

        db.Tenants.Add(tenant);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return (tenant.Id.Value, user.Id.Value);
    }
}
