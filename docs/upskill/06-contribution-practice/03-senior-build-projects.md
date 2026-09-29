# Senior Build Projects

Six open-ended projects that require system-level thinking: design decisions, tradeoffs, production concerns, and implementation across multiple layers.

---

## PROJECT-S01: Transactional Outbox for Domain Events

**Type:** Reliability architecture  
**Estimated effort:** 3–5 days  
**Prerequisite reading:** `04-side-effects-async-and-reliability.md`

**The problem:**  
Domain events (`BookingCreatedEvent`, `BookingCancelledEvent`) are dispatched after `SaveChanges` in `AppDbContext`. If the handler throws, the booking is saved but the side effect (email, webhook, audit log) is lost. If the process crashes between save and publish, same result.

**Your task:**  
Implement the Transactional Outbox pattern:

1. **Schema:** Add an `outbox_messages` table with columns: `id`, `type`, `payload` (JSON), `created_at`, `processed_at` (nullable), `error` (nullable).

2. **Write phase:** In `AppDbContext.SaveChangesAsync`, instead of calling `publisher.Publish(event)`, serialize the domain event to JSON and write it to `outbox_messages` in the same transaction as the business entity.

3. **Process phase:** Add a hosted service (`IHostedService`) that polls `outbox_messages` for unprocessed entries, deserializes them, calls `publisher.Publish(...)`, and marks them processed.

4. **Idempotency:** If the same message is processed twice (restart mid-batch), the handler must be idempotent. Document how.

**Design decisions to document:**
- What happens if the polling service fails mid-batch?
- How do you order events? (Insert order, sequence number)
- How do you prevent multiple app instances from processing the same message?
- When do you delete processed messages?

**Definition of done:**
- [ ] Domain event is written to outbox in same transaction as booking
- [ ] Hosted service processes outbox entries
- [ ] Handler called even if app restarts between write and process
- [ ] Integration test: kill the app between save and process, restart, verify handler runs
- [ ] ADR (Architecture Decision Record) explaining tradeoffs

---

## PROJECT-S02: Concurrent Booking Double-Write Prevention (RISK-003)

**Type:** Correctness/concurrency  
**Estimated effort:** 2–3 days  
**Prerequisite reading:** `06-architecture-critique.md` RISK-003

**The problem:**  
Two simultaneous requests for the same host + time slot both pass the conflict check (no DB lock between read and write) and both bookings are inserted. The host gets double-booked.

**Your task:**  
Fix this at the database level. Two approaches — implement one:

**Option A — Unique partial index:**
```sql
-- Prevents two non-cancelled bookings with overlapping times
-- Requires PostgreSQL exclusion constraint (not standard unique index)
CREATE EXTENSION btree_gist;
ALTER TABLE bookings ADD CONSTRAINT no_overlap
  EXCLUDE USING gist (
    host_id WITH =,
    tstzrange(start_time, end_time) WITH &&
  )
  WHERE (status != 'Cancelled');
```

**Option B — Pessimistic lock:**
In `CreateBookingHandler`, before the conflict check:
```csharp
await db.Database.ExecuteSqlAsync(
    $"SELECT pg_advisory_xact_lock({hostId.GetHashCode()})", ct);
```
This serializes booking creation for the same host within a transaction.

**Design questions to answer:**
- What is the performance impact of each approach?
- What happens if Option A's exclusion constraint is violated — what does PostgreSQL return?
- How does the handler convert a DB constraint violation to a `409 Conflict` response?
- Option B holds a lock for the entire transaction. What is the maximum concurrent throughput?

**Definition of done:**
- [ ] Load test: 10 concurrent requests for same slot → only 1 booking created
- [ ] Handler returns 409 on constraint violation, not 500
- [ ] ADR documenting chosen approach and tradeoffs

---

## PROJECT-S03: Add Webhook Delivery System

**Type:** Feature architecture  
**Estimated effort:** 4–6 days  
**Prerequisites:** PROJECT-S01 (or implement outbox inline)

**The problem:**  
Hosts want to be notified when a booking is created (e.g., to trigger Zapier, CRM updates, Slack notifications). Build a webhook delivery system.

**Requirements:**

1. **Domain:** `WebhookEndpoint` aggregate — per-tenant, per-event-type subscriptions. Fields: `url`, `secret` (for HMAC signature), `events` (list of event types to subscribe to: `booking.created`, `booking.cancelled`), `isActive`.

2. **Delivery:** When `BookingCreatedEvent` fires, find active webhook endpoints subscribed to `booking.created` for the booking's tenant. POST a JSON payload to each URL with `X-Signature: sha256=<hmac>`.

3. **Retry:** Failed deliveries (network error, non-2xx response) should retry with exponential backoff (1s, 5s, 30s, 2min).

4. **Audit:** `webhook_delivery_log` table — every delivery attempt: endpoint, payload, response code, latency, attempt number.

**Security requirement:**  
HMAC-SHA256 the payload with the endpoint's secret. The receiver verifies the signature. Document how a receiver validates it.

**Definition of done:**
- [ ] Webhook endpoint CRUD (create, list, delete)
- [ ] Booking creation triggers webhook delivery
- [ ] Retry on failure (3 attempts with backoff)
- [ ] Delivery log accessible via API
- [ ] Signature verification documented and tested

---

## PROJECT-S04: Multi-Schedule Support and Timezone-Aware Slot Generation

**Type:** Domain complexity  
**Estimated effort:** 3–5 days

**The problem:**  
Currently, a host has one default Mon–Fri schedule. Real users need multiple schedules (different hours for different event types, blocked days, overrides).

**Requirements:**

1. **Domain changes:**
   - `Schedule` can have multiple named time windows per day
   - `ScheduleOverride` — a specific date range with different hours (e.g., vacation, conference)
   - `EventType` can be linked to a specific schedule (or use the default)

2. **Availability calculation:**
   - Consider the host's IANA timezone when interpreting schedule hours
   - Apply overrides: if a date falls in a `ScheduleOverride`, use override hours
   - Handle DST transitions correctly (a "9am" slot in a timezone observing DST is 14:00 UTC in winter and 13:00 UTC in summer)

3. **Slot generation overhaul:**
   - `Schedule.GetAvailableSlots(date, durationMinutes, timezone)` must handle:
     - Multiple non-contiguous windows in a day (e.g., 9am–12pm, 2pm–5pm)
     - Date overrides
     - DST edge cases (2:30 AM on spring-forward day may not exist)

**Definition of done:**
- [ ] Host can create a schedule with morning and afternoon windows
- [ ] Host can block specific dates via overrides
- [ ] Slot generation correctly handles DST transitions
- [ ] Unit tests for DST edge cases

---

## PROJECT-S05: SaaS Tenant Onboarding Flow

**Type:** Feature + infrastructure  
**Estimated effort:** 4–6 days

**The problem:**  
There's no way to create tenants or users in this system. The database has `tenants` and `users` tables but no API to populate them. A real SaaS needs onboarding.

**Requirements:**

1. **Registration endpoint:** `POST /api/tenants/register` — creates a tenant + owner user. Input: `tenantName`, `slug`, `ownerEmail`. Output: `tenantId`, `userId`, JWT token.

2. **Idempotency:** Duplicate `slug` or `ownerEmail` returns a clear error, not 500.

3. **Tenant isolation verification:** After registration, the new tenant's events, bookings, and users are completely isolated from other tenants.

4. **No auth server in repo** (established constraint). The registration endpoint is the only token-issuing endpoint. It should issue a JWT with the correct `tenant_id`, `tenant_slug`, `sub` claims.

**Security concern to address:**  
The registration endpoint is public and issues JWTs. How do you prevent it from being used to spam-create tenants? (Rate limiting, email verification, or CAPTCHA — pick one and implement it.)

**Definition of done:**
- [ ] `POST /api/tenants/register` creates tenant + user + returns JWT
- [ ] Integration test: register two tenants, verify complete data isolation
- [ ] Duplicate slug/email returns 409
- [ ] Spam protection mechanism in place

---

## PROJECT-S06: Performance Profiling and Query Optimization

**Type:** Performance investigation  
**Estimated effort:** 2–3 days

**The problem:**  
`GetAvailableSlotsHandler` and several list endpoints are unoptimized. Before optimizing, you need data.

**Your task:**

**Phase 1 — Seed and measure:**
1. Write a seed script that creates 10 tenants, each with 5 hosts, each with 500 bookings over 6 months
2. Run `GET /api/schedules/available-slots` against one of these hosts and measure:
   - Total response time
   - Number of DB queries
   - Memory allocated (use `dotnet-trace` or BenchmarkDotNet)

**Phase 2 — Identify hotspots:**
3. Enable EF Core SQL logging
4. Record all SQL statements for the available-slots request
5. Identify which queries take the most time (use `EXPLAIN ANALYZE` in PostgreSQL)

**Phase 3 — Optimize:**
6. Implement at least one concrete improvement:
   - Push slot filtering to SQL
   - Add composite indexes on frequently-queried column pairs
   - Switch from loading full `Booking` entities to projected tuples for conflict checking

**Phase 4 — Measure again:**
7. Re-run the same benchmark after the optimization
8. Document: % improvement, what you changed, what you rejected and why

**Definition of done:**
- [ ] Baseline measurements documented
- [ ] At least one concrete optimization implemented
- [ ] Post-optimization measurements documented
- [ ] Written analysis: what changed, tradeoffs, what you'd do next
