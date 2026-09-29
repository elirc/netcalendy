# 01 — Code Review Mindset

## What Code Review Is For

Code review is not a gate to catch bugs. It is:
- A shared understanding check: does the team know what this code does?
- A design conversation: is this the right abstraction?
- A knowledge transfer mechanism: every review is a teaching moment

The bugs code review does catch are a bonus, not the point. Automated tests catch bugs more reliably than human eyes.

---

## As a Reviewer: What to Actually Look For

### Priority order (most important first):

**1. Correctness of behavior, not just correctness of code**  
Does this code do what the ticket asked? Does it handle the cases the ticket didn't mention? Read the ticket before reading the diff.

**2. Security**  
For every new endpoint or data access: check the auth checklist from `05-security-checklist.md`. Does it filter by tenant? Does the update verify ownership?

**3. Unintended side effects**  
What else does this change break? Look at callers of modified functions. Check if a DB schema change could break existing queries.

**4. Testability and test coverage**  
Are there tests? Do they test behavior or just assert the code runs? What cases are missing?

**5. Design fit**  
Does this follow the patterns already in the codebase? If it introduces a new pattern, is there a reason? Is the reason documented?

**What NOT to spend review time on:**  
Formatting, naming conventions enforced by the linter, subjective style preferences. These have an automated home. Don't make humans do what computers do better.

---

## Giving Feedback: The Three Types

Label your comments so the author knows what to do with them:

**🚫 Blocking (must fix before merge):**  
Correctness bugs, security issues, missing tests for a risky change, violations of a hard architectural rule.

> Example: "This query doesn't filter by tenant. A cross-tenant read is a data breach. Blocking."

**⚠️ Non-blocking important:**  
Real concerns that you'd accept a documented tradeoff for. The author should respond — either fix it or explain why not.

> Example: "This loads all bookings into memory. Fine for now, but leaves a performance cliff. Should we add a comment or a follow-up ticket?"

**💡 Optional / suggestion:**  
Nice-to-haves, style preferences, alternative approaches you'd consider. The author can take or leave them without explanation.

> Example: "You could use `switch` expression here for readability. Up to you."

---

## Giving Feedback: The Tone

**Describe the impact, not the person.**

Bad: "You forgot to add tenant filtering."  
Good: "This query loads results without tenant scope — any authenticated user can read any resource."

**Ask questions before making assertions.**

Bad: "This is wrong."  
Good: "I'm not sure I understand the approach here. What happens if `startTime` is in a DST-transition hour?"

**Be specific.**

Bad: "This is hard to read."  
Good: "The nested ternaries on line 45 are hard to follow. Would a few local variables help here?"

---

## Receiving Feedback

**Assume good intent.** The reviewer is trying to make the code better, not criticize you.

**Ask for clarification before defending.** "I'm not sure I understand — can you say more about the concern?" costs nothing and prevents 90% of review arguments.

**Distinguish "I don't agree" from "I don't understand."** If you don't understand the feedback, ask. If you understand and disagree, say so with reasoning — don't silently fix it and resent it.

**It's not personal.** The code is not you. Deleting a function you spent a day on is not a loss if the team agrees it was the right call.

---

## Reviewing This Codebase Specifically

When reviewing a PR in this repo, run through these checks:

**Every new handler:**
- [ ] Returns `Result<T>`, not throws
- [ ] Passes `CancellationToken` to all async calls
- [ ] Has at least one corresponding test

**Every new endpoint:**
- [ ] Has `[Authorize]` or explicit `[AllowAnonymous]` (intentional, documented)
- [ ] Maps result to correct HTTP status via `ToActionResult()`

**Every new "get by ID":**
- [ ] Checks tenant scope after loading

**Every new DB query:**
- [ ] No N+1 risk
- [ ] Uses parameterized values (EF Core default — but watch for `FromSqlRaw`)

**Every new string field from user input:**
- [ ] Validated in FluentValidation
