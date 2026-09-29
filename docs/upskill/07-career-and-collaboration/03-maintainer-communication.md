# 03 — Maintainer Communication

## Being a Good Contributor to Someone Else's Codebase

Whether you're contributing to an open-source project or to a codebase maintained by a senior engineer on your team, the same principles apply.

**Read before you write.** Before opening a PR, read the existing code in the area you're changing. Does your approach match the patterns already in use? If not, is there a good reason to diverge?

**Scope tightly.** A PR that fixes a bug plus refactors a module plus adds a feature will get slow review and hard-to-understand feedback. One concern per PR.

**Don't make reviewers guess.** If your PR description says "various fixes," reviewers have to reverse-engineer your intent from the diff. Write what you intended, and reviewers can focus on whether you succeeded.

**Accept that maintainers know context you don't.** A change that looks obviously correct to you might conflict with an ongoing refactor, an unreleased API change, or a performance constraint you can't see. When a reviewer blocks something that seems fine, ask why before assuming they're wrong.

---

## Being a Good Maintainer

When you're the senior engineer whose codebase others are contributing to:

**Respond promptly.** Nothing demotivates a contributor faster than a PR that sits unreviewed for a week. Even "I'll look at this Thursday" is better than silence.

**Make the bar explicit.** The best way to get PRs that pass review is to write down what you want. This repo's `docs/upskill/` documentation exists partly for this reason — it communicates architecture decisions to contributors before they write a line of code.

**Distinguish personal preference from hard requirement.** "I would have done this differently" is not a blocking comment unless you can articulate why the current approach has a real cost.

**Acknowledge good work.** Code review is not only for corrections. If a contributor did something elegant or caught a real bug, say so.

---

## Asking Good Technical Questions

When you're stuck, how you ask determines how fast you get unblocked.

**Bad question:** "My tests are failing. Help?"

**Good question:**
> I'm implementing TICKET-001 (UpdateEventTypeValidator). My test `PUT_eventType_WithEmptyName_Returns400` is failing — it returns 200 instead of 400.
>
> I've confirmed the validator is registered (added a log line). I've confirmed `ValidationBehavior` runs (it logs for other commands). I think the issue is that the validator isn't being picked up for `UpdateEventTypeCommand`, but I'm not sure why.
>
> Relevant files: `UpdateEventTypeValidator.cs`, `DependencyInjection.cs`, `ValidationBehavior.cs`. Hypothesis: the assembly scan might not be including the validator because of a namespace mismatch?

This question shows:
- What you're trying to do
- What you've already tried
- What you think the problem might be
- Where to look

The responder can confirm or redirect your hypothesis instead of starting from scratch.

---

## When to Escalate vs. When to Figure It Out

**Figure it out first when:**
- The answer is in the codebase (read the code)
- The answer is in the docs (check `docs/upskill/`)
- 30 minutes of focused investigation would resolve it

**Escalate when:**
- You've investigated and found something surprising (a possible bug, an undocumented constraint)
- The fix requires changes to shared infrastructure you don't own
- You've spent > 2 hours on something and aren't making progress

When escalating, lead with what you found, not what you're trying to do. "I found that `ICurrentTenantService.TenantId` is always null in integration tests because the test JWT doesn't include `tenant_id` — is that expected?" gets resolved faster than "My integration test fails."

---

## Disagreeing with a Technical Decision

Disagreeing is fine. Disagreeing poorly is how trust erodes.

**The right frame:** "Here's the tradeoff I see, and here's my preference. What am I missing?"

**Not the right frame:** "This is wrong" / "This is bad code" / "I wouldn't do it this way."

If you've made your case clearly and the decision goes the other way, accept it and move on. Relitigating a closed decision in code comments or hallway conversations corrodes the team.

If you believe the decision is genuinely harmful (security risk, data loss potential), escalate through the right channels — don't work around it in code.
