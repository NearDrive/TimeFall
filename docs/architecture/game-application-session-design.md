# Game.Application Session Layer Design (Corrected)

## What was wrong in the previous version
The previous proposal modeled `Game.Application` as a thin `Dispatch(action)` wrapper over `GameReducer.Reduce`.

That was insufficient because it left shared orchestration in `Game.Cli`:
- pre-actions (e.g., continue availability synchronization),
- action chaining and sequencing,
- post-action state fix-up via extra reducer calls.

If kept there, a future graphical client would need to duplicate that flow logic.

## Corrected architectural intent
`Game.Application` is a **use-case/session orchestrator** above `Game.Core`.
It coordinates the reducer pipeline for user-facing actions while keeping all gameplay rules in `Game.Core`.

## Responsibility split
### Game.Core
- Pure deterministic gameplay rules.
- `GameReducer`, `GameState`, `GameAction`, `GameEvent`.
- No UI/file IO concerns.

### Game.Application
- Owns session lifecycle state.
- Owns player-action pipeline sequencing.
- Owns pre/post reducer chains shared by multiple clients.
- Resolves application-level context needed to execute a player action (e.g., continue using externally supplied saved-run snapshot).
- Remains UI-agnostic.

### Game.Cli
- Parse commands.
- Resolve CLI-only command aliases to typed `GameAction`.
- Call session application API.
- Render output and perform CLI persistence.

## Minimal corrected API
```csharp
public interface IGameSession
{
    GameState State { get; }
    IReadOnlyList<GameEvent> ApplyPlayerAction(GameAction action);
    IReadOnlyList<GameEvent> SetSavedRunState(GameState? savedRunState);
}
```

## Session orchestration pipeline
For each `ApplyPlayerAction(action)`:
1. Pre-action synchronization: apply `SetContinueAvailabilityAction(savedRunState != null)`.
2. Resolve application-context action (e.g., `ContinueRunAction` uses tracked saved state).
3. Apply resolved action through `GameReducer`.
4. Post-action synchronization: re-apply `SetContinueAvailabilityAction(savedRunState != null)`.
5. Return the complete event list for the full pipeline.

Notes:
- Pipeline uses reducer as source of truth for state transitions.
- No gameplay rules are moved into `Game.Application`.
- Determinism is preserved because ordering is explicit and stable.

## CLI refactor plan
1. Construct `GameSession` with initial state and optional saved run snapshot.
2. Replace direct `GameReducer.Reduce` calls with:
   - `session.ApplyPlayerAction(...)`
   - `session.SetSavedRunState(...)` after persistence changes.
3. Keep persistence transition policy in CLI (adapter-specific policy).
4. Keep rendering in CLI.

## Why this now solves the issue
- Shared orchestration no longer lives in CLI.
- CLI and future client can reuse the same player-action pipeline.
- CLI no longer calls `GameReducer` directly.
- `Game.Core` remains the single place for gameplay rules.
