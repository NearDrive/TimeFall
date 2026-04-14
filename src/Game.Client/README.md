# Game.Client (Timefall Deck)

This client now implements **Phase 2** of the visual roadmap: client input is wired to real `GameAction` dispatch through `IGameSession`.

## What is implemented

- MonoGame client bootstraps with real game content from `Game.Data`.
- A `GameSession` is created from `GameState.CreateInitial(...)` and debug-bootstrapped into a playable run state.
- `InputHandler` provides edge-triggered keyboard and mouse input.
- `MapScreen` converts node clicks into real `MoveToNodeAction` calls.
- `CombatScreen` converts card clicks into real `PlayCardAction` calls.
- `CombatScreen` converts end-turn button clicks (and `E`) into real `EndTurnAction` calls.
- All actions are dispatched through `session.ApplyPlayerAction(action)`.

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
  - Click node placeholder squares to dispatch `MoveToNodeAction`.
- Press `F3` to switch to **Combat**.
  - Click card placeholder rectangles to dispatch `PlayCardAction`.
  - Click the end-turn rectangle (or press `E`) to dispatch `EndTurnAction`.

## Temporary debug assumptions for Phase 2

- Client startup applies existing run-setup actions to ensure the session is in a usable map state for testing input mapping.
- Entering the combat screen auto-starts combat via `BeginCombatAction` when the session is currently in map exploration.
- These are thin client bootstrap helpers only; gameplay rules still execute in `Game.Core` via `GameSession`.

## Current limitations

- UI remains placeholder-only rectangles (no polished visual layer yet).
- No drag/drop, animations, or event playback in this phase.
- No advanced flow transitions beyond required input/action wiring.
