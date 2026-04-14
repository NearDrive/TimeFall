# Game.Client (Timefall Deck)

This client now implements **Phase 6** of the visual roadmap: playtesting-oriented debug/stability tooling on top of the playable visual loop and event playback.

## What is implemented

- MonoGame client still bootstraps with real game content from `Game.Data` and runs on a real `GameSession`.
- Client now sets a default backbuffer size of `1280x720` so Phase 3 debug panels/buttons are visible without manual window resize.
- Added shared rendering primitives:
  - 1x1 white pixel texture for colored rectangle UI blocks.
  - Minimal built-in bitmap debug text renderer (no content-pipeline font dependency).
- `CombatScreen` renders real combat state:
  - Player HP / Armor / Resources / deck counts.
  - One enemy panel per enemy with id, HP, Armor, Resources.
  - Hand cards as rectangles with card index, name, and cost from real card definitions.
  - Visible `End Turn` button (`click` and `E` dispatch `EndTurnAction`).
- `MapScreen` now renders real map state:
  - All nodes from the real map graph.
  - Current node highlighted.
  - Adjacent nodes highlighted and used as click targets for `MoveToNodeAction`.
  - Visited nodes marked separately for easier debug readability.
- Added minimal `RewardScreen` that renders real reward choices and dispatches:
  - `ChooseRewardCardAction(cardId)` on option click.
  - `SkipRewardAction` on `Skip` click or `S` key.
- Added state-driven screen routing in `ScreenManager`:
  - `MapExploration -> MapScreen`
  - `Combat -> CombatScreen`
  - `RewardSelection -> RewardScreen`
  - Routing runs automatically on update and after every dispatched player action.
- Added a minimal **client-side event playback queue** in `ScreenManager`:
  - Events returned by `IGameSession.ApplyPlayerAction` are enqueued.
  - Playback runs sequentially (one active event at a time) with short per-event durations.
  - `CombatScreen` reads active playback visuals from the dispatcher.
- Added minimal visual feedback in `CombatScreen`:
  - **Damage feedback** from real game events:
    - `PlayerStrikePlayed` -> damage number in enemy area.
    - `EnemyAttackPlayed` -> damage number in player panel area.
    - `StatusTriggered` with HP decrease -> damage number for player/enemy area.
  - **Card-play feedback** from real game events:
  - `PlayerStrikePlayed` and `CardDiscarded` -> temporary card highlight border.
  - If the card is no longer visible in hand, fallback highlight uses the last clicked card region.
- Added a **debug/playtest overlay** rendered by `ScreenManager` on top of all screens:
  - Current screen + game phase.
  - Draw pile count (count-only, no hidden draw-order exposure).
  - Discard pile count and a short discard preview list while in combat.
  - Rolling recent event history (latest first, short summaries).
- Extended event playback plumbing with rolling recent-event history (client-side, bounded list).
- Added debug stability controls:
  - `F5` restarts the run by creating a **fresh `GameSession` through the same startup/bootstrap path**.
  - `F6` writes a concise debug dump to console with screen, phase, combat/map summaries, pile counts, and recent events.
- Input behavior remains Phase 2 aligned:
  - Rendered hand card rectangles are exactly the clickable `PlayCardAction(index)` regions.
  - Rendered end-turn rectangle is exactly the clickable `EndTurnAction` region.
  - Rendered adjacent node rectangles are exactly the clickable `MoveToNodeAction(nodeId)` regions.
  - Rendered reward card rectangles are exactly the clickable `ChooseRewardCardAction(cardId)` regions.

## How to run

From the repository root:

```bash
dotnet run --project src/Game.Client/Game.Client.csproj
```

> Note: You need the .NET SDK and graphics/runtime dependencies required by MonoGame DesktopGL.

## How to interact

Once the game window is open:

- The startup bootstrap enters a run-ready map state using real actions.
- In **Map**:
  - Click highlighted adjacent nodes to dispatch `MoveToNodeAction`.
  - If movement enters combat in the real session state, the client auto-switches to **Combat**.
- In **Combat**:
  - Click hand cards to dispatch `PlayCardAction`.
  - Click `End Turn` (or press `E`) to dispatch `EndTurnAction`.
  - When combat ends in the real session state, the client auto-switches to **Reward**.
- In **Reward**:
  - Click a reward card to dispatch `ChooseRewardCardAction`.
  - Click `Skip` (or press `S`) to dispatch `SkipRewardAction`.
  - When reward is resolved and phase returns to map exploration, the client auto-switches to **Map**.
- `F1` can still be used as a manual debug switch to MainMenu.
- `F5` restarts the current run from a fresh session via normal client bootstrap.
- `F6` prints a debug dump to the console.

## Temporary debug assumptions

- Client startup applies existing run-setup actions to ensure the session is in a usable map state.
- No client-side combat bootstrap is used anymore; combat starts only when real map movement triggers it in `Game.Core`.

## Current limitations

- Rendering is intentionally simple (rectangles + debug text + flat colors) for debug visibility.
- Event playback is intentionally minimal (timed sequential overlays), not a full animation timeline system.
- Debug overlay intentionally avoids exposing hidden draw order; only draw pile count is shown.
- No drag/drop, tooltip, VFX, SFX, or polished layout system in this phase.
- Client still does not call `GameReducer` directly; it dispatches through `IGameSession.ApplyPlayerAction`.
- No gameplay logic is duplicated in the client.
