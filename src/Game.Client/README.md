# Game.Client (Timefall Deck)

This client currently implements **Phase 1** of the visual roadmap: a minimal screen system with manual switching for testing.

## What is implemented

- MonoGame client bootstraps and runs.
- A `ScreenManager` owns and updates/draws the active screen.
- Placeholder screens:
  - `MainMenu`
  - `Map`
  - `Combat`
- Manual debug switching between screens.

## How to run

From the repository root:

```bash
dotnet run --project src/Game.Client/Game.Client.csproj
```

> Note: You need the .NET SDK and graphics/runtime dependencies required by MonoGame DesktopGL.

## How to interact

Once the game window is open:

- Press `F1` to switch to **MainMenu** screen.
- Press `F2` to switch to **Map** screen.
- Press `F3` to switch to **Combat** screen.

Each screen renders a different background color so you can confirm switching works.

## Current limitations (expected in Phase 1)

- No real gameplay flow wiring yet.
- No `GameState`-driven UI.
- No map/combat data rendering.
- No Reward screen implementation yet.

This is intentional to keep Phase 1 focused on client screen structure only.
