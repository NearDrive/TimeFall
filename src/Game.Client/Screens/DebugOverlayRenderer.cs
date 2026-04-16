using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Screens;

public static class DebugOverlayRenderer
{
    private const int MaxDiscardPreview = 6;
    private const int MaxRecentEventPreview = 10;

    public static void Draw(SpriteBatch spriteBatch, GameState state, ScreenType currentScreenType, IReadOnlyList<GameEvent> recentEvents)
    {
        var pixel = RenderPrimitives.Pixel;
        var panel = new Rectangle(860, 12, 408, 696);
        spriteBatch.Draw(pixel, panel, new Color(0, 0, 0, 180));

        var line = 0;
        DrawLine("DEBUG HUD", Color.Gold);
        DrawLine($"SCREEN: {currentScreenType}", Color.White);
        DrawLine($"PHASE: {state.Phase}", Color.White);
        DrawLine(string.Empty, Color.White);

        if (state.Combat is { } combat)
        {
            DrawLine($"DRAW PILE: {combat.Player.Deck.DrawPile.Count}", Color.White);
            DrawLine($"DISCARD PILE: {combat.Player.Deck.DiscardPile.Count}", Color.White);
            DrawLine("DISCARD CARDS:", Color.White);
            foreach (var cardText in BuildDiscardPreviewLines(state, combat.Player.Deck))
            {
                DrawLine($"  - {cardText}", Color.LightGray);
            }
        }
        else
        {
            DrawLine("DRAW PILE: N/A (NOT IN COMBAT)", Color.Gray);
            DrawLine("DISCARD PILE: N/A (NOT IN COMBAT)", Color.Gray);
        }

        DrawLine(string.Empty, Color.White);
        DrawLine("KEYS (CURRENT SCREEN):", Color.White);
        foreach (var control in BuildControlHints(currentScreenType, state.Phase))
        {
            DrawLine($"  - {control}", Color.LightGray);
        }

        DrawLine(string.Empty, Color.White);
        DrawLine("RECENT EVENTS:", Color.White);

        var recent = recentEvents
            .TakeLast(MaxRecentEventPreview)
            .Reverse()
            .Select(DebugSummaryFormatter.FormatGameEvent)
            .ToArray();

        if (recent.Length == 0)
        {
            DrawLine("  - NONE", Color.Gray);
            return;
        }

        foreach (var summary in recent)
        {
            DrawLine($"  - {summary}", Color.LightGray);
        }

        return;

        void DrawLine(string text, Color color)
        {
            DebugTextRenderer.DrawText(spriteBatch, pixel, text, new Vector2(panel.X + 10, panel.Y + 8 + (line * 18)), color);
            line++;
        }
    }

    private static IReadOnlyList<string> BuildDiscardPreviewLines(GameState state, DeckState deck)
    {
        if (deck.DiscardPile.Count == 0)
        {
            return ["(EMPTY)"];
        }

        return deck.DiscardPile
            .TakeLast(MaxDiscardPreview)
            .Reverse()
            .Select(card => TryGetCardName(state, card.DefinitionId))
            .ToArray();
    }

    private static string TryGetCardName(GameState state, CardId cardId)
    {
        if (!state.CardDefinitions.TryGetValue(cardId, out var definition))
        {
            return cardId.Value;
        }

        return $"{definition.Name} ({cardId.Value})";
    }

    private static IReadOnlyList<string> BuildControlHints(ScreenType currentScreenType, GamePhase phase)
    {
        var controls = new List<string>
        {
            "F3 Toggle Debug HUD",
            "F5 Restart Session",
            "F6 Dump Debug State",
            "F1 Return to Main Menu",
            "Left Click Use highlighted option/card",
        };

        if (phase is GamePhase.SandboxDeckEdit)
        {
            controls.Add("Mouse Wheel / Up / Down Scroll card list");
        }

        if (currentScreenType is ScreenType.Map)
        {
            controls.Add("Left Click on adjacent node to move");
        }

        if (currentScreenType is ScreenType.Combat)
        {
            controls.Add("Left Click card to PlayCardAction(index)");
            controls.Add("Left Click enemy panel to set target for attack cards");
            controls.Add("Left Click END TURN button to pass turn");
        }

        if (currentScreenType is ScreenType.Reward)
        {
            controls.Add("Left Click reward card to pick");
            controls.Add("Left Click SKIP to skip reward");
        }

        if (currentScreenType is ScreenType.SandboxDeckSelect)
        {
            controls.Add("Left Click deck tile to select loadout base");
        }

        if (currentScreenType is ScreenType.SandboxEnemySelect)
        {
            controls.Add("Left Click enemy tile to pick opponent");
            controls.Add("Left Click START COMBAT to begin sandbox fight");
        }

        if (currentScreenType is ScreenType.SandboxPostCombat)
        {
            controls.Add("Left Click REMATCH for same setup");
            controls.Add("Left Click CHANGE ENEMY or CHANGE DECK");
        }

        return controls;
    }
}

public static class DebugSummaryFormatter
{
    public static string FormatGameEvent(GameEvent gameEvent)
    {
        return gameEvent switch
        {
            RunStarted started => $"RunStarted seed={started.Seed}",
            MovedToNode moved => $"MovedToNode {moved.NodeId.Value}",
            EnteredCombat entered => $"EnteredCombat node={entered.NodeId?.Value ?? "n/a"}",
            CombatEnded ended => $"CombatEnded won={ended.PlayerWon}",
            RewardOffered offered => $"RewardOffered {offered.RewardType}",
            RewardChosen chosen => $"RewardChosen {chosen.CardId.Value}",
            RewardSkipped skipped => $"RewardSkipped {skipped.RewardType}",
            CardDrawn drawn => $"CardDrawn {drawn.Card.DefinitionId.Value}",
            CardDiscarded discarded => $"CardDiscarded {discarded.Card.DefinitionId.Value}",
            PlayerStrikePlayed strike => $"PlayerStrike {strike.Card.DefinitionId.Value} dmg={strike.Damage}",
            EnemyAttackPlayed enemyAttack => $"EnemyAttack {enemyAttack.Card.DefinitionId.Value} dmg={enemyAttack.Damage}",
            TurnEnded turnEnded => $"TurnEnded next={turnEnded.NextTurnOwner}",
            PlayCardRejected rejected => $"PlayRejected {rejected.Reason}",
            TimeAdvanced advanced => $"TimeAdvanced step={advanced.Step}",
            _ => gameEvent.GetType().Name,
        };
    }
}
