# Native look on every platform

**Decided:** 2026-10

Petaurum does not have one look shared across platforms. Each app follows its platform's current design language and uses its native UI toolkit, so it feels like it belongs on that device:

| Platform | Design language | Toolkit |
|----------|-----------------|---------|
| Windows | Fluent Design (Windows 11): Mica, cards, Segoe UI Variable, Segoe Fluent Icons | WinUI 3 (Windows App SDK) |

Later apps add a row here when they start.

## Why

Petaurum is a utility people keep open next to their system's own tools. Matching the platform (its materials, type, icons, light and dark themes, accent colour and accessibility) makes it feel trustworthy and familiar, and the platform's toolkit does most of that work for us.

## What this means

- `specs/` say *what* a screen shows and how it behaves, never how it looks. Platform-specific presentation goes under **Platform notes**.
- Shared concepts that need a picture, such as a device's type, are named in the specs (`tv`, `router`, `printer`), and each app maps them to its platform's own icons.
- Apps use their theme's colours and type styles instead of hard-coded ones, so light, dark, high-contrast and accent colours work.
