# Petaurum: notes for agents

Monorepo for Petaurum, a local-first springboard to every device on the user's network. See README.md.

- `specs/` is the source of truth for behaviour. Change the spec first, then each app that implements it, and keep each spec's **Status** line current.
- Each app in `apps/<platform>/` is native to its platform and shares no code with the others. Keep platform-specific details out of `specs/` except under **Platform notes**.
- Windows app: `dotnet build apps/windows/Petaurum.Windows.sln`; run with `dotnet run --project apps/windows/src/Petaurum.Windows`.
