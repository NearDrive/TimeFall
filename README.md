# Timefall Deck

Timefall Deck is structured as a layered .NET solution:

- `Game.Core`: deterministic domain state, rules, reducer, and combat systems.
- `Game.Application`: session/use-case orchestration over reducer actions.
- `Game.Cli` / `Game.Client`: adapters that consume the application session.

## Official game modes

The authoritative `GameState` now models two official modes:

- `GameMode.Run`: standard map/time/reward progression run loop.
- `GameMode.Sandbox`: combat testing loop with explicit sandbox phases and state.

Sandbox is implemented in `Game.Core` + `Game.Application` as a first-class mode. It does **not** generate map progression, apply rewards, advance time, or change meta/run progression state.

## Sandbox flow (core)

Sandbox phases are:

1. `SandboxDeckSelect`
2. `SandboxDeckEdit`
3. `SandboxEnemySelect`
4. `SandboxCombat`
5. `SandboxPostCombat`

Core actions support entering sandbox, selecting a deck, editing equipped sandbox loadout, selecting an enemy, starting combat, repeating combat, and leaving sandbox mode.

Sandbox combat reuses the same combat engine/reducer path as normal combat while routing combat end to sandbox post-combat state instead of run reward/map flow.

## Sandbox CLI flow

`Game.Cli` now exposes the official sandbox loop end-to-end:

1. `sandbox [seed]` from Main Menu.
2. `sandbox-decks` + `select-sandbox-deck <id|index>`.
3. `cards`, `equip <id|index>`, `unequip <id|index>`, `clear-loadout`.
4. `enemies` + `select-enemy <id|index>`.
5. `start` to enter real combat (`SandboxCombat` phase).
6. After combat (`SandboxPostCombat`): `repeat` (indefinite loop), `setup` (back to loadout), `change-enemy`, or `back` (leave sandbox).

CLI help is phase-aware for sandbox phases and keeps combat rendering on the same real combat renderer used by run mode.
