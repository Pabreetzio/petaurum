# Specs

Platform-neutral descriptions of what Petaurum is and does. Every app in `apps/` implements these specs. They describe behaviour, data, and look and feel, not code.

- `domain/` — the data model shared by every app (what a Device is, and so on).
- `features/` — user-facing features: what they show, how they behave, and what counts as done.

## Conventions

- Each spec has a **Status** line: `draft`, `accepted`, or `implemented (<apps>)`.
- Use **must** / **should** / **may** for requirements.
- A spec lists anything that differs per platform under a **Platform notes** heading, so the rest stays platform-neutral.
- When an app implements part of a spec, the app's README says which parts.
