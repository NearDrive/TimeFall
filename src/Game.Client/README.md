# Game.Client (Timefall Deck)

This client now implements **Phase 3** of the visual roadmap: it renders real `GameSession.State` in `MapScreen` and `CombatScreen`, while preserving the Phase 2 input-to-action path.

## What is implemented

- MonoGame client still bootstraps with real game content from `Game.Data` and runs on a real `GameSession`.
- Added shared rendering primitives:
  - 1x1 white pixel texture for colored rectangle UI blocks.
  - Minimal built-in bitmap debug text renderer (no content-pipeline font dependency).
- `CombatScreen` now renders real combat state:
  - Player HP / Armor / Resources / deck counts.
  - One enemy panel per enemy with id, HP, Armor, Resources.
  - Hand cards as rectangles with card index, name, and cost from real card definitions.
  - Visible `End Turn` button (`click` and `E` still dispatch `EndTurnAction`).
- `MapScreen` now renders real map state:
  - All nodes from the real map graph.
  - Current node highlighted.
  - Adjacent nodes highlighted and used as click targets for `MoveToNodeAction`.
  - Visited nodes marked separately for easier debug readability.
- Input behavior remains Phase 2 aligned:
  - Rendered hand card rectangles are exactly the clickable `PlayCardAction(index)` regions.
  - Rendered end-turn rectangle is exactly the clickable `EndTurnAction` region.
  - Rendered adjacent node rectangles are exactly the clickable `MoveToNodeAction(nodeId)` regions.

## How to run

From the repository root:

```bash
dotnet run --project src/Game.Client/Game.Client.csproj
```

> Note: You need the .NET SDK and graphics/runtime dependencies required by MonoGame DesktopGL.

## How to interact

Once the game window is open:

- Press `F1` to switch to **MainMenu**.
- Press `F2` to switch to **Map**.
  - Click highlighted adjacent nodes to dispatch `MoveToNodeAction`.
- Press `F3` to switch to **Combat**.
  - Click hand cards to dispatch `PlayCardAction`.
  - Click `End Turn` (or press `E`) to dispatch `EndTurnAction`.

## Temporary debug assumptions

- Client startup applies existing run-setup actions to ensure the session is in a usable map state.
- Entering the combat screen auto-starts combat via `BeginCombatAction` when currently in map exploration.
- These remain thin client bootstrap helpers only; gameplay rules still execute in `Game.Core` via `GameSession`.

## Current limitations

- Rendering is intentionally simple (rectangles + debug text + flat colors) for debug visibility.
- No animation/event playback, drag/drop, tooltip, VFX, SFX, or polished layout system in this phase.
- No gameplay logic is duplicated in the client.
