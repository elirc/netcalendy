# Answers and review rubric

Case one succeeds when the remaining invitee and domain validations also pass.
The saved booking carries tenant T, host H and E's actual ID; its end is the
requested start plus E.Duration.Minutes. The harness verifies one Add and one
SaveChanges call. An answer that merely says “201” skips the handler's contract:
the HTTP status belongs to controller mapping and was not executed by this test.

Case two returns EventType.NotFound before conflict or schedule lookup. H's
independent existence would not prove ownership of E. Case three returns the
same result for a tenant mismatch. In both cases the event lookup is allowed,
but operations on the requested host's calendar must not follow. Case four
returns EventType.Inactive only for a matching tuple. A wrong-host inactive event
returns not found because scope is checked before state. Case five succeeds with
an empty slot list after one default-schedule lookup; an absent schedule is an
existing supported empty result, unlike an absent selected event.

The cancellation-token exercise is useful if cancellation propagation is part
of the operation's documented contract. Avoid asserting arbitrary local variable
names or object construction order. A controller integration test adds HTTP
binding, tenant resolution and error translation; it cannot be replaced by a
handler double returning a fabricated status code. Its evidence should remain
separate from the focused source-slice compilation.

Score a review on four dimensions: correct resource relationship, early rejection,
preserved legal behavior and accurate evidence boundaries. Require the author to
show both handlers because availability and booking form a public workflow.
Reject client-only checks and checks performed after Add. Ask which error is
returned for a resource outside scope and whether it reveals more information
than the existing not-found path.

For the concurrency design, there is no single answer proven by this patch.
A credible proposal names a real database mechanism and tests two interleaving
transactions. A pre-write conflict query alone is not an atomic guarantee.
The accepted review should acknowledge that remaining concern without expanding
a small, testable relationship repair into an unverified scheduling redesign.
