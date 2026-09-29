# Contribution Practice

This section turns the codebase into a training lab for real-world software development. Each file contains tickets, projects, and katas designed to build the habits of a working engineer, not just the knowledge.

## Contents

| File | Topic |
|---|---|
| [01-good-first-tickets.md](01-good-first-tickets.md) | 20 junior-level tickets |
| [02-mid-level-feature-tickets.md](02-mid-level-feature-tickets.md) | 12 mid-level feature tickets |
| [03-senior-build-projects.md](03-senior-build-projects.md) | 6 senior-level build projects |
| [04-refactor-and-design-katas.md](04-refactor-and-design-katas.md) | Design and refactoring exercises |

## How to Use These Tickets

Pick a ticket. Before writing any code:
1. Read the referenced files
2. Draw the data flow on paper (or in your head) — from HTTP request to DB and back
3. Write the test first (or at least identify what test would verify your change is correct)
4. Make the change
5. Run the tests: `dotnet test`
6. Write a commit message that explains *why*, not *what*

The point is not to complete tickets quickly. The point is to practice the habits: read before write, test before ship, explain before close.
