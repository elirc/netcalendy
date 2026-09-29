using System.Reflection;
using NetCalendy.Application.Bookings.Commands.CreateBooking;
using NetCalendy.Application.Common;
using NetCalendy.Application.Schedules.Queries.GetAvailableSlots;
using NetCalendy.Domain.Bookings;
using NetCalendy.Domain.EventTypes;
using NetCalendy.Domain.Repositories;
using NetCalendy.Domain.Schedules;
using NetCalendy.Domain.Tenants;
using NetCalendy.Domain.Users;

var passed = 0;
var failed = 0;
async Task Run(string name, Func<Task> test)
{
    try { await test(); passed++; Console.WriteLine($"ok - {name}"); }
    catch (Exception error) { failed++; Console.WriteLine($"not ok - {name}: {error}"); }
}
void Check(bool value, string message) { if (!value) throw new Exception(message); }

foreach (var scenario in new[] { "missing", "foreign-host", "foreign-tenant", "inactive" })
{
    await Run($"booking rejects {scenario} before conflict lookup or writes", async () =>
    {
        var f = new Fixture(scenario);
        var result = await f.BookingHandler.Handle(f.Command, CancellationToken.None);
        Check(result.IsFailure, "request unexpectedly succeeded");
        Check(result.Error.Code == (scenario == "inactive" ? "EventType.Inactive" : "EventType.NotFound"), "unstable error contract");
        Check(f.DownstreamCalls == 0 && f.Added.Count == 0 && f.Saves == 0, "rejected request reached downstream state");
    });
    await Run($"availability rejects {scenario} before schedule or booking reads", async () =>
    {
        var f = new Fixture(scenario);
        var result = await f.SlotsHandler.Handle(f.Query, CancellationToken.None);
        Check(result.IsFailure, "request unexpectedly succeeded");
        Check(result.Error.Code == (scenario == "inactive" ? "EventType.Inactive" : "EventType.NotFound"), "unstable error contract");
        Check(f.DownstreamCalls == 0, "rejected request read schedules or bookings");
    });
}

await Run("matching active event creates and saves the correctly scoped booking", async () =>
{
    var f = new Fixture("valid");
    var result = await f.BookingHandler.Handle(f.Command, CancellationToken.None);
    Check(result.IsSuccess && f.Added.Count == 1 && f.Saves == 1, "valid create was not persisted once");
    var saved = f.Added.Single();
    Check(saved.HostId == UserId.From(f.Host) && saved.TenantId == TenantId.From(f.Tenant), "scope changed in persistence");
    Check(saved.EventTypeId == f.Event!.Id, "wrong event reference");
    Check(result.Value.EndTime == f.Command.StartTime.AddMinutes(30), "duration changed");
});
await Run("matching active event retains existing conflict rejection", async () =>
{
    var f = new Fixture("valid") { Conflicting = true };
    var result = await f.BookingHandler.Handle(f.Command, CancellationToken.None);
    Check(result.IsFailure && result.Error.Code == "Booking.TimeConflict", "existing conflict behavior changed");
    Check(f.Added.Count == 0 && f.Saves == 0, "conflicting booking persisted");
});
await Run("matching event without a schedule still returns an empty slot list", async () =>
{
    var f = new Fixture("valid");
    var result = await f.SlotsHandler.Handle(f.Query, CancellationToken.None);
    Check(result.IsSuccess && result.Value.Count == 0, "missing schedule no longer gives empty success");
    Check(f.DownstreamCalls == 1, "unexpected downstream reads");
});

Console.WriteLine($"# tests {passed + failed}\n# pass {passed}\n# fail {failed}\n# skipped 0");
return failed == 0 ? 0 : 1;

public class ContractProxy : DispatchProxy
{
    public Func<MethodInfo, object?[]?, object?> InvokeContract { get; set; } = null!;
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => InvokeContract(targetMethod!, args);
    public static T Make<T>(Func<MethodInfo, object?[]?, object?> contract) where T : class
    {
        var proxy = Create<T, ContractProxy>();
        ((ContractProxy)(object)proxy).InvokeContract = contract;
        return proxy;
    }
}

public sealed class Fixture
{
    public Guid Tenant { get; } = Guid.NewGuid();
    public Guid Host { get; } = Guid.NewGuid();
    public EventType? Event { get; }
    public int DownstreamCalls { get; private set; }
    public int Saves { get; private set; }
    public bool Conflicting { get; init; }
    public List<Booking> Added { get; } = [];
    public CreateBookingCommand Command { get; }
    public GetAvailableSlotsQuery Query { get; }
    public CreateBookingHandler BookingHandler { get; }
    public GetAvailableSlotsHandler SlotsHandler { get; }

    public Fixture(string scenario)
    {
        Event = scenario == "missing" ? null : EventType.Create(
            TenantId.From(scenario == "foreign-tenant" ? Guid.NewGuid() : Tenant),
            UserId.From(scenario == "foreign-host" ? Guid.NewGuid() : Host), "Chat", "chat", 30).Value;
        if (scenario == "inactive") Event!.Deactivate();
        var start = DateTimeOffset.UtcNow.AddDays(5);
        Command = new(Event?.Id.Value ?? Guid.NewGuid(), Host, start, "Alex", "alex@example.test", "UTC");
        var day = DateOnly.FromDateTime(start.UtcDateTime);
        Query = new(Command.EventTypeId, Host, day, day);
        var events = ContractProxy.Make<IEventTypeRepository>((method, _) => method.Name == "GetByIdAsync"
            ? Task.FromResult(Event) : throw new Exception($"Unexpected event call: {method.Name}"));
        var bookings = ContractProxy.Make<IBookingRepository>((method, args) =>
        {
            DownstreamCalls++;
            if (method.Name == "GetConflictingBookingsAsync")
            {
                var rows = new List<Booking>();
                if (Conflicting) rows.Add(Booking.Create(TenantId.From(Tenant), Event!.Id, UserId.From(Host),
                    Invitee.Create("Other", "other@example.test", "UTC").Value, start, start.AddMinutes(30)).Value);
                return Task.FromResult<IReadOnlyList<Booking>>(rows);
            }
            if (method.Name == "Add") { Added.Add((Booking)args![0]!); return null; }
            throw new Exception($"Unexpected booking call: {method.Name}");
        });
        var schedules = ContractProxy.Make<IScheduleRepository>((method, _) =>
        {
            DownstreamCalls++;
            return method.Name == "GetDefaultByUserAsync" ? Task.FromResult<Schedule?>(null)
                : throw new Exception($"Unexpected schedule call: {method.Name}");
        });
        var tenant = ContractProxy.Make<ICurrentTenantService>((method, _) => method.Name == "get_TenantId" ? Tenant : "fixture");
        var uow = ContractProxy.Make<IUnitOfWork>((_, _) => { Saves++; return Task.FromResult(1); });
        BookingHandler = new(bookings, events, tenant, uow);
        SlotsHandler = new(events, schedules, bookings, tenant);
    }
}
