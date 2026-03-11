# Reshuffle Fatigue

## Overview
Reshuffle Fatigue replaces burn-on-reshuffle. When a draw pile is empty and a reshuffle is required, the discard pile is shuffled back into draw, the hand is refilled toward the initial hand size (5), then a forced fatigue discard is applied.

## Why burn was removed
Burn-on-reshuffle permanently removed cards and made long combats overly punitive. Reshuffle Fatigue preserves deck identity and consistency while still adding scaling pressure as combats run long.

## Formal rule definition
On reshuffle:
1. Move discard pile into draw pile.
2. Shuffle draw pile using combat RNG.
3. Refill hand up to initial hand size.
4. Discard `X` cards immediately, where `X = min(reshuffleCount + 1, initialHandSize, currentHandSize)`.
5. Increment `reshuffleCount`.

`reshuffleCount` is tracked per combat entity (player and each enemy), starts at 0 on combat creation, and does not persist outside combat.

## Examples
Hand size = 5

1st reshuffle → discard 1  
2nd reshuffle → discard 2  
3rd reshuffle → discard 3  
4th reshuffle → discard 4  
5th reshuffle → discard 5

If hand is partially filled during reshuffle, refill happens first, then fatigue discard is applied.

## Edge cases
- Draw and discard empty: no-op.
- Refill cannot reach full hand: discard only up to cards currently in hand.
- Fatigue larger than hand: discard entire hand.
- Reshuffle while hand already full: refill does nothing, fatigue discard still applies.

## Enemy behavior
Enemy fatigue discard is deterministic: discard from the first card in stable hand order until the required discard count is reached.

## Relationship to discard mechanics
Player fatigue discard uses the same pending discard gate used for other required discards. The player cannot play cards while a required discard is pending.

## Design goals
- explosive early turns
- fatigue in long combats
- avoids permanent card loss
- creates discard strategy
