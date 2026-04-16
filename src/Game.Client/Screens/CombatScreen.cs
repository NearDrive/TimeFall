using Game.Application;
using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Game.Client.Screens;

public sealed class CombatScreen : IScreen
{
    private static readonly Rectangle PlayerPanelRegion = new(20, 20, 420, 180);
    private static readonly Rectangle CardInfoRegion = new(20, 220, 420, 180);
    private static readonly Rectangle EnemyAreaRegion = new(470, 20, 700, 130);
    private const int EnemyPanelWidth = 340;
    private const int EnemyPanelHeight = 130;
    private const int EnemyPanelGap = 16;

    private readonly IGameSession _session;
    private readonly InputHandler _input;
    private readonly IClientActionDispatcher _dispatcher;
    private readonly List<Rectangle> _cardRegions = new();
    private readonly List<Rectangle> _enemyRegions = new();
    private readonly HashSet<int> _selectedOverflowDiscardIndexes = new();
    private readonly Rectangle _endTurnRegion = new(950, 610, 220, 70);
    private Rectangle? _lastPlayedCardRegion;
    private int? _selectedEnemyIndex;

    public CombatScreen(IGameSession session, InputHandler input, IClientActionDispatcher dispatcher)
    {
        _session = session;
        _input = input;
        _dispatcher = dispatcher;
    }

    public void Update(GameTime time)
    {
        _ = time;
        BuildCardRegions();
        BuildEnemyRegions();
        NormalizeSelectedEnemy();
        NormalizeOverflowDiscardSelection();

        if (TryHandleOverflowDiscardInput())
        {
            return;
        }

        for (var enemyIndex = 0; enemyIndex < _enemyRegions.Count; enemyIndex++)
        {
            if (_input.IsLeftClick(_enemyRegions[enemyIndex]))
            {
                _selectedEnemyIndex = enemyIndex;
                return;
            }
        }

        for (var index = 0; index < _cardRegions.Count; index++)
        {
            if (_input.IsLeftClick(_cardRegions[index]))
            {
                _lastPlayedCardRegion = _cardRegions[index];
                _dispatcher.Dispatch(new PlayCardAction(index, ResolveTargetIndex(index)));
                BuildCardRegions();
                BuildEnemyRegions();
                NormalizeSelectedEnemy();
                return;
            }
        }

        if (_input.IsLeftClick(_endTurnRegion) || _input.IsKeyPressed(Keys.E))
        {
            _dispatcher.Dispatch(new EndTurnAction());
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        spriteBatch.GraphicsDevice.Clear(new Color(28, 18, 20));

        spriteBatch.Begin();
        DrawCombatState(spriteBatch);
        spriteBatch.End();
    }

    private void DrawCombatState(SpriteBatch spriteBatch)
    {
        var pixel = RenderPrimitives.Pixel;

        if (!IsCombatPhase(_session.State.Phase) || _session.State.Combat is null)
        {
            DebugTextRenderer.DrawText(spriteBatch, pixel, "NO ACTIVE COMBAT", new Vector2(20, 20), Color.White);
            return;
        }

        var combat = _session.State.Combat;

        DrawPlayerPanel(spriteBatch, pixel, combat.Player);
        DrawEnemyPanels(spriteBatch, pixel, combat.Enemies);
        DrawHand(spriteBatch, pixel, combat.Player.Deck.Hand);
        DrawCardInfoPanel(spriteBatch, pixel, combat.Player.Deck.Hand);
        DrawPlaybackVisuals(spriteBatch, pixel, combat.Player.Deck.Hand);
        DrawTargetSelection(spriteBatch, pixel, combat.Enemies.Count);

        spriteBatch.Draw(pixel, _endTurnRegion, Color.DarkOrange);
        DebugTextRenderer.DrawText(spriteBatch, pixel, "END TURN (E)", new Vector2(_endTurnRegion.X + 14, _endTurnRegion.Y + 22), Color.Black);
        DebugTextRenderer.DrawText(spriteBatch, pixel, $"TURN: {combat.TurnOwner}", new Vector2(950, 575), Color.White);
        DebugTextRenderer.DrawText(spriteBatch, pixel, $"MODE: {_session.State.Mode}", new Vector2(950, 545), Color.White);
        DrawOverflowDiscardPrompt(spriteBatch, pixel, combat);
    }

    private static void DrawPlayerPanel(SpriteBatch spriteBatch, Texture2D pixel, CombatEntity player)
    {
        spriteBatch.Draw(pixel, PlayerPanelRegion, new Color(35, 49, 64));

        DebugTextRenderer.DrawText(spriteBatch, pixel, "PLAYER", new Vector2(PlayerPanelRegion.X + 12, PlayerPanelRegion.Y + 10), Color.White);
        DebugTextRenderer.DrawText(spriteBatch, pixel, $"HP: {player.HP}/{player.MaxHP}", new Vector2(PlayerPanelRegion.X + 12, PlayerPanelRegion.Y + 35), Color.White);
        DebugTextRenderer.DrawText(spriteBatch, pixel, $"ARMOR: {player.Armor}", new Vector2(PlayerPanelRegion.X + 12, PlayerPanelRegion.Y + 60), Color.White);
        DebugTextRenderer.DrawText(spriteBatch, pixel, $"RESOURCES: {FormatResources(player.Resources)}", new Vector2(PlayerPanelRegion.X + 12, PlayerPanelRegion.Y + 85), Color.White);
        DebugTextRenderer.DrawText(spriteBatch, pixel, $"DRAW/DISCARD/BURN: {player.Deck.DrawPile.Count}/{player.Deck.DiscardPile.Count}/{player.Deck.BurnPile.Count}", new Vector2(PlayerPanelRegion.X + 12, PlayerPanelRegion.Y + 110), Color.White);
    }

    private static void DrawEnemyPanels(SpriteBatch spriteBatch, Texture2D pixel, IReadOnlyList<CombatEntity> enemies)
    {
        const int startX = 470;
        const int startY = 20;

        for (var index = 0; index < enemies.Count; index++)
        {
            var enemy = enemies[index];
            var panel = new Rectangle(startX + ((EnemyPanelWidth + EnemyPanelGap) * index), startY, EnemyPanelWidth, EnemyPanelHeight);
            spriteBatch.Draw(pixel, panel, new Color(77, 39, 39));

            DebugTextRenderer.DrawText(spriteBatch, pixel, $"ENEMY {index + 1}: {enemy.EntityId}", new Vector2(panel.X + 12, panel.Y + 10), Color.White);
            DebugTextRenderer.DrawText(spriteBatch, pixel, $"HP: {enemy.HP}/{enemy.MaxHP}", new Vector2(panel.X + 12, panel.Y + 38), Color.White);
            DebugTextRenderer.DrawText(spriteBatch, pixel, $"ARMOR: {enemy.Armor}", new Vector2(panel.X + 12, panel.Y + 64), Color.White);
            DebugTextRenderer.DrawText(spriteBatch, pixel, $"RESOURCES: {FormatResources(enemy.Resources)}", new Vector2(panel.X + 12, panel.Y + 90), Color.White);
        }
    }

    private void DrawHand(SpriteBatch spriteBatch, Texture2D pixel, IReadOnlyList<CardInstance> hand)
    {
        for (var index = 0; index < hand.Count && index < _cardRegions.Count; index++)
        {
            var cardRegion = _cardRegions[index];
            var cardColor = new Color(186, 193, 204);
            if (IsOverflowDiscardActive() && _selectedOverflowDiscardIndexes.Contains(index))
            {
                cardColor = new Color(214, 145, 86);
            }

            spriteBatch.Draw(pixel, cardRegion, cardColor);

            var card = hand[index];
            var definition = TryGetCardDefinition(card.DefinitionId);
            var title = definition?.Name ?? card.DefinitionId.Value;
            var cost = definition?.Cost ?? 0;

            DebugTextRenderer.DrawText(spriteBatch, pixel, $"[{index}] {title}", new Vector2(cardRegion.X + 8, cardRegion.Y + 10), Color.Black);
            DebugTextRenderer.DrawText(spriteBatch, pixel, $"COST: {cost}", new Vector2(cardRegion.X + 8, cardRegion.Y + 35), Color.Black);
            DebugTextRenderer.DrawText(spriteBatch, pixel, card.DefinitionId.Value, new Vector2(cardRegion.X + 8, cardRegion.Y + 60), Color.Black);
        }
    }

    private CardDefinition? TryGetCardDefinition(CardId cardId)
    {
        return _session.State.CardDefinitions.TryGetValue(cardId, out var definition)
            ? definition
            : null;
    }

    private void DrawCardInfoPanel(SpriteBatch spriteBatch, Texture2D pixel, IReadOnlyList<CardInstance> hand)
    {
        spriteBatch.Draw(pixel, CardInfoRegion, new Color(24, 34, 52));

        var focusedCardIndex = GetFocusedCardIndex();
        if (focusedCardIndex is null || focusedCardIndex.Value < 0 || focusedCardIndex.Value >= hand.Count)
        {
            DebugTextRenderer.DrawText(spriteBatch, pixel, "HOVER A CARD FOR DETAILS", new Vector2(CardInfoRegion.X + 10, CardInfoRegion.Y + 12), Color.White, scale: 1);
            return;
        }

        var cardIndex = focusedCardIndex.Value;
        var card = hand[cardIndex];
        var definition = TryGetCardDefinition(card.DefinitionId);

        var lines = new List<string>
        {
            $"CARD [{cardIndex}]",
            $"NAME: {definition?.Name ?? card.DefinitionId.Value}",
            $"ID: {card.DefinitionId.Value}",
            $"COST: {definition?.Cost ?? 0}",
            $"PLAY COSTS: {(definition is null ? "UNKNOWN" : FormatPlayCosts(definition))}",
            "RULES:",
            definition is null ? "No definition available." : CardRulesTextFormatter.GetReadableRulesText(definition),
        };

        var wrapped = WrapLines(lines, maxChars: 41);
        var maxLines = 14;
        for (var lineIndex = 0; lineIndex < wrapped.Count && lineIndex < maxLines; lineIndex++)
        {
            DebugTextRenderer.DrawText(
                spriteBatch,
                pixel,
                wrapped[lineIndex],
                new Vector2(CardInfoRegion.X + 10, CardInfoRegion.Y + 12 + (lineIndex * 12)),
                Color.White,
                scale: 1);
        }
    }

    private int? GetFocusedCardIndex()
    {
        for (var index = 0; index < _cardRegions.Count; index++)
        {
            if (_cardRegions[index].Contains(_input.MousePosition))
            {
                return index;
            }
        }

        return null;
    }

    private void BuildCardRegions()
    {
        _cardRegions.Clear();

        if (!IsCombatPhase(_session.State.Phase) || _session.State.Combat is null)
        {
            return;
        }

        var handCount = _session.State.Combat.Player.Deck.Hand.Count;
        for (var index = 0; index < handCount; index++)
        {
            var x = 20 + (index * 150);
            _cardRegions.Add(new Rectangle(x, 440, 140, 150));
        }
    }

    private void BuildEnemyRegions()
    {
        _enemyRegions.Clear();

        if (!IsCombatPhase(_session.State.Phase) || _session.State.Combat is null)
        {
            return;
        }

        const int startX = 470;
        const int startY = 20;
        for (var index = 0; index < _session.State.Combat.Enemies.Count; index++)
        {
            var x = startX + ((EnemyPanelWidth + EnemyPanelGap) * index);
            _enemyRegions.Add(new Rectangle(x, startY, EnemyPanelWidth, EnemyPanelHeight));
        }
    }

    private void NormalizeSelectedEnemy()
    {
        if (!IsCombatPhase(_session.State.Phase) || _session.State.Combat is null)
        {
            _selectedEnemyIndex = null;
            return;
        }

        if (_selectedEnemyIndex is null)
        {
            return;
        }

        var selectedEnemy = _selectedEnemyIndex.Value;
        var enemies = _session.State.Combat.Enemies;
        if (selectedEnemy < 0 || selectedEnemy >= enemies.Count || enemies[selectedEnemy].HP <= 0)
        {
            _selectedEnemyIndex = FindFirstLivingEnemyIndex(enemies);
        }
    }

    private void NormalizeOverflowDiscardSelection()
    {
        if (_session.State.Combat is not { } combat || !combat.NeedsOverflowDiscard)
        {
            _selectedOverflowDiscardIndexes.Clear();
            return;
        }

        var handCount = combat.Player.Deck.Hand.Count;
        _selectedOverflowDiscardIndexes.RemoveWhere(index => index < 0 || index >= handCount);
    }

    private int? ResolveTargetIndex(int handIndex)
    {
        if (_session.State.Combat is not { } combat || handIndex < 0 || handIndex >= combat.Player.Deck.Hand.Count)
        {
            return null;
        }

        var card = combat.Player.Deck.Hand[handIndex];
        if (!_session.State.CardDefinitions.TryGetValue(card.DefinitionId, out var definition))
        {
            return null;
        }

        if (!CardEffectResolver.RequiresEnemyTarget(definition))
        {
            return null;
        }

        if (_selectedEnemyIndex is { } selected && selected >= 0 && selected < combat.Enemies.Count && combat.Enemies[selected].HP > 0)
        {
            return selected;
        }

        return FindFirstLivingEnemyIndex(combat.Enemies);
    }

    private void DrawTargetSelection(SpriteBatch spriteBatch, Texture2D pixel, int enemyCount)
    {
        if (enemyCount == 0)
        {
            return;
        }

        for (var index = 0; index < enemyCount && index < _enemyRegions.Count; index++)
        {
            if (_selectedEnemyIndex == index)
            {
                DrawBorder(spriteBatch, pixel, _enemyRegions[index], 3, Color.Gold);
            }
        }

        var targetText = _selectedEnemyIndex is { } selected
            ? $"TARGET: ENEMY {selected + 1}"
            : "TARGET: AUTO";
        DebugTextRenderer.DrawText(spriteBatch, pixel, targetText, new Vector2(470, 160), Color.Gold);
        DebugTextRenderer.DrawText(spriteBatch, pixel, "CLICK ENEMY PANEL TO SELECT TARGET", new Vector2(470, 184), Color.LightGray);
    }

    private static int? FindFirstLivingEnemyIndex(IReadOnlyList<CombatEntity> enemies)
    {
        for (var index = 0; index < enemies.Count; index++)
        {
            if (enemies[index].HP > 0)
            {
                return index;
            }
        }

        return null;
    }

    private static string FormatResources(IReadOnlyDictionary<ResourceType, int> resources)
    {
        if (resources.Count == 0)
        {
            return "NONE";
        }

        return string.Join(", ", resources.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key}:{entry.Value}"));
    }

    private static string FormatPlayCosts(CardDefinition definition)
    {
        var costs = definition.PlayCostsOrDefault;
        return string.Join(
            ", ",
            costs.Select(cost => cost switch
            {
                NoCost => "NONE",
                RequireMomentumCost require => $"REQ MOM >= {require.Minimum}",
                SpendMomentumCost spend => $"SPEND MOM {spend.Amount}",
                SpendAllMomentumCost => "SPEND ALL MOM",
                SpendUpToMomentumCost spendUpTo => $"SPEND UP TO {spendUpTo.Max}",
                _ => "SPECIAL",
            }));
    }

    private static List<string> WrapLines(IEnumerable<string> lines, int maxChars)
    {
        var output = new List<string>();
        foreach (var line in lines)
        {
            foreach (var wrapped in WrapSingleLine(line, maxChars))
            {
                output.Add(wrapped);
            }
        }

        return output;
    }

    private static IEnumerable<string> WrapSingleLine(string text, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield return string.Empty;
            yield break;
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var currentLine = string.Empty;
        foreach (var word in words)
        {
            if (word.Length > maxChars)
            {
                if (!string.IsNullOrEmpty(currentLine))
                {
                    yield return currentLine;
                    currentLine = string.Empty;
                }

                for (var index = 0; index < word.Length; index += maxChars)
                {
                    yield return word.Substring(index, Math.Min(maxChars, word.Length - index));
                }

                continue;
            }

            var candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
            if (candidate.Length <= maxChars)
            {
                currentLine = candidate;
            }
            else
            {
                yield return currentLine;
                currentLine = word;
            }
        }

        if (!string.IsNullOrEmpty(currentLine))
        {
            yield return currentLine;
        }
    }

    private static bool IsCombatPhase(GamePhase phase)
    {
        return phase is GamePhase.Combat or GamePhase.SandboxCombat;
    }

    private bool TryHandleOverflowDiscardInput()
    {
        if (_session.State.Combat is not { NeedsOverflowDiscard: true } combat)
        {
            return false;
        }

        for (var index = 0; index < _cardRegions.Count; index++)
        {
            if (!_input.IsLeftClick(_cardRegions[index]))
            {
                continue;
            }

            if (!_selectedOverflowDiscardIndexes.Add(index))
            {
                _selectedOverflowDiscardIndexes.Remove(index);
            }

            if (_selectedOverflowDiscardIndexes.Count == combat.RequiredOverflowDiscardCount)
            {
                var indexes = _selectedOverflowDiscardIndexes.OrderBy(i => i).ToArray();
                _dispatcher.Dispatch(new DiscardOverflowAction(indexes));
                _selectedOverflowDiscardIndexes.Clear();
            }

            return true;
        }

        return true;
    }

    private bool IsOverflowDiscardActive()
    {
        return _session.State.Combat is { NeedsOverflowDiscard: true };
    }

    private void DrawOverflowDiscardPrompt(SpriteBatch spriteBatch, Texture2D pixel, CombatState combat)
    {
        if (!combat.NeedsOverflowDiscard)
        {
            return;
        }

        var selected = _selectedOverflowDiscardIndexes.Count;
        var required = combat.RequiredOverflowDiscardCount;
        var remaining = Math.Max(0, required - selected);
        DebugTextRenderer.DrawText(spriteBatch, pixel, "DISCARD REQUIRED", new Vector2(20, 408), Color.OrangeRed);
        DebugTextRenderer.DrawText(spriteBatch, pixel, $"Select {required} card(s): {selected}/{required}", new Vector2(180, 408), Color.OrangeRed);
        DebugTextRenderer.DrawText(spriteBatch, pixel, $"Remaining: {remaining}", new Vector2(470, 408), Color.OrangeRed);
        DebugTextRenderer.DrawText(spriteBatch, pixel, "Click selected card again to unselect.", new Vector2(620, 408), Color.LightGray);
    }

    private void DrawPlaybackVisuals(SpriteBatch spriteBatch, Texture2D pixel, IReadOnlyList<CardInstance> hand)
    {
        var playback = _dispatcher.ActivePlayback;
        if (playback is null)
        {
            return;
        }

        if (playback.DamageFeedback is { } damageFeedback)
        {
            DrawDamageFeedback(spriteBatch, pixel, damageFeedback);
        }

        if (playback.CardHighlight is { } cardHighlight)
        {
            DrawCardHighlight(spriteBatch, pixel, hand, cardHighlight);
        }
    }

    private void DrawDamageFeedback(SpriteBatch spriteBatch, Texture2D pixel, DamageFeedbackVisual damageFeedback)
    {
        var text = $"-{damageFeedback.Amount}";
        var origin = damageFeedback.Target == DamageFeedbackTarget.Player
            ? new Vector2(PlayerPanelRegion.X + PlayerPanelRegion.Width - 120, PlayerPanelRegion.Y + 20)
            : new Vector2(EnemyAreaRegion.X + 20, EnemyAreaRegion.Y + 20);

        DebugTextRenderer.DrawText(spriteBatch, pixel, text, origin, Color.OrangeRed);
    }

    private void DrawCardHighlight(SpriteBatch spriteBatch, Texture2D pixel, IReadOnlyList<CardInstance> hand, CardHighlightVisual cardHighlight)
    {
        var activePlaybackEvent = _dispatcher.ActivePlayback?.Event;
        var highlightRegion = activePlaybackEvent is PlayerStrikePlayed or CardDiscarded
            ? _lastPlayedCardRegion
            : null;

        if (highlightRegion is null)
        {
            for (var index = 0; index < hand.Count && index < _cardRegions.Count; index++)
            {
                if (hand[index].DefinitionId == cardHighlight.CardId)
                {
                    highlightRegion = _cardRegions[index];
                    break;
                }
            }
        }

        if (highlightRegion is not { } region)
        {
            return;
        }

        DrawBorder(spriteBatch, pixel, region, 4, Color.Gold);
    }

    private static void DrawBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle region, int thickness, Color color)
    {
        spriteBatch.Draw(pixel, new Rectangle(region.X, region.Y, region.Width, thickness), color);
        spriteBatch.Draw(pixel, new Rectangle(region.X, region.Bottom - thickness, region.Width, thickness), color);
        spriteBatch.Draw(pixel, new Rectangle(region.X, region.Y, thickness, region.Height), color);
        spriteBatch.Draw(pixel, new Rectangle(region.Right - thickness, region.Y, thickness, region.Height), color);
    }
}
