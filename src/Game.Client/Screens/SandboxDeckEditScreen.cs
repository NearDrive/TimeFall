using Game.Application;
using Game.Core.Cards;
using Game.Core.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Screens;

public sealed class SandboxDeckEditScreen : IScreen
{
    private const int CardsPerRow = 4;
    private const int CardWidth = 210;
    private const int CardHeight = 110;
    private const int CardSpacingX = 230;
    private const int CardSpacingY = 120;
    private const int GridStartX = 20;
    private const int GridStartY = 130;
    private const int ScrollStepPixels = 45;

    private readonly IGameSession _session;
    private readonly InputHandler _input;
    private readonly IClientActionDispatcher _dispatcher;

    private readonly List<(CardId CardId, Rectangle Region)> _cardRegions = new();
    private readonly Rectangle _clearRegion = new(960, 120, 260, 70);
    private readonly Rectangle _enemySelectRegion = new(960, 210, 260, 70);
    private readonly Rectangle _backDeckSelectRegion = new(960, 300, 260, 70);
    private readonly Rectangle _hoverDetailsRegion = new(960, 390, 260, 300);
    private readonly Rectangle _scrollViewport = new(20, 130, 920, 570);
    private int _scrollOffset;

    public SandboxDeckEditScreen(IGameSession session, InputHandler input, IClientActionDispatcher dispatcher)
    {
        _session = session;
        _input = input;
        _dispatcher = dispatcher;
    }

    public void Update(GameTime time)
    {
        _ = time;
        BuildCardRegions();
        UpdateScrollOffset();

        foreach (var (cardId, region) in _cardRegions)
        {
            var shifted = ShiftForScroll(region);
            if (shifted.Intersects(_scrollViewport) && _input.IsLeftClick(shifted))
            {
                _dispatcher.Dispatch(new ToggleSandboxLoadoutCardAction(cardId));
                return;
            }
        }

        if (_input.IsLeftClick(_clearRegion))
        {
            _dispatcher.Dispatch(new ClearSandboxLoadoutAction());
            return;
        }

        if (_input.IsLeftClick(_enemySelectRegion))
        {
            _dispatcher.Dispatch(new OpenSandboxEnemySelectAction());
            return;
        }

        if (_input.IsLeftClick(_backDeckSelectRegion))
        {
            var seed = _session.State.Sandbox?.SessionSeed ?? 424242;
            _dispatcher.Dispatch(new LeaveSandboxModeAction());
            _dispatcher.Dispatch(new EnterSandboxModeAction(seed));
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        spriteBatch.GraphicsDevice.Clear(new Color(31, 28, 44));

        spriteBatch.Begin();
        var pixel = RenderPrimitives.Pixel;

        if (!TryGetContext(out var selectedDeck, out var equipped))
        {
            DebugTextRenderer.DrawText(spriteBatch, pixel, "SANDBOX DECK EDIT NOT READY", new Vector2(20, 20), Color.White);
            spriteBatch.End();
            return;
        }

        DebugTextRenderer.DrawText(spriteBatch, pixel, $"SANDBOX DECK EDIT: {selectedDeck.Name}", new Vector2(20, 20), Color.White);
        DebugTextRenderer.DrawText(spriteBatch, pixel, $"EQUIPPED: {equipped.Count}", new Vector2(20, 50), Color.White);
        var maxScroll = GetMaxScroll();
        var scrollHint = maxScroll > 0
            ? $"SCROLL: MOUSE WHEEL / UP / DOWN ({_scrollOffset}/{maxScroll})"
            : "SCROLL: ALL CARDS FIT";
        DebugTextRenderer.DrawText(spriteBatch, pixel, scrollHint, new Vector2(20, 80), Color.White);
        spriteBatch.Draw(pixel, _scrollViewport, new Color(255, 255, 255, 18));

        foreach (var (cardId, region) in _cardRegions)
        {
            var shifted = ShiftForScroll(region);
            if (!shifted.Intersects(_scrollViewport))
            {
                continue;
            }

            var equippedCard = equipped.Contains(cardId);
            spriteBatch.Draw(pixel, shifted, equippedCard ? Color.Gold : new Color(173, 184, 207));

            var definition = _session.State.CardDefinitions.TryGetValue(cardId, out var card) ? card : null;
            var name = definition?.Name ?? cardId.Value;
            var cost = definition?.Cost ?? 0;

            DebugTextRenderer.DrawText(spriteBatch, pixel, name, new Vector2(shifted.X + 8, shifted.Y + 10), Color.Black);
            DebugTextRenderer.DrawText(spriteBatch, pixel, $"ID: {cardId.Value}", new Vector2(shifted.X + 8, shifted.Y + 35), Color.Black);
            DebugTextRenderer.DrawText(spriteBatch, pixel, $"COST: {cost}", new Vector2(shifted.X + 8, shifted.Y + 60), Color.Black);
            DebugTextRenderer.DrawText(spriteBatch, pixel, equippedCard ? "EQUIPPED" : "NOT EQUIPPED", new Vector2(shifted.X + 8, shifted.Y + 85), Color.Black);
        }

        DrawButton(spriteBatch, pixel, _clearRegion, "CLEAR LOADOUT");
        DrawButton(spriteBatch, pixel, _enemySelectRegion, "NEXT: ENEMY");
        DrawButton(spriteBatch, pixel, _backDeckSelectRegion, "CHANGE DECK");
        DrawHoveredCardDetails(spriteBatch, pixel);

        spriteBatch.End();
    }

    private bool TryGetContext(out RunDeckDefinition selectedDeck, out IReadOnlyCollection<CardId> equipped)
    {
        selectedDeck = null!;
        equipped = Array.Empty<CardId>();

        var sandbox = _session.State.Sandbox;
        if (sandbox?.SelectedDeckId is null || !_session.State.DeckDefinitions.TryGetValue(sandbox.SelectedDeckId, out var deck))
        {
            return false;
        }

        selectedDeck = deck;
        equipped = sandbox.EquippedCardIds;
        return true;
    }

    private void BuildCardRegions()
    {
        _cardRegions.Clear();

        if (!TryGetContext(out var selectedDeck, out _))
        {
            return;
        }

        var allCardIds = selectedDeck.StartingCombatDeckCardIds
            .Concat(selectedDeck.RewardPoolCardIds)
            .Distinct()
            .OrderBy(id => id.Value, StringComparer.Ordinal)
            .ToArray();

        for (var index = 0; index < allCardIds.Length; index++)
        {
            var x = GridStartX + (index % CardsPerRow) * CardSpacingX;
            var y = GridStartY + (index / CardsPerRow) * CardSpacingY;
            _cardRegions.Add((allCardIds[index], new Rectangle(x, y, CardWidth, CardHeight)));
        }
    }

    private void UpdateScrollOffset()
    {
        var inputDelta = -_input.MouseWheelDelta;
        if (_input.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Down))
        {
            inputDelta += ScrollStepPixels;
        }

        if (_input.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Up))
        {
            inputDelta -= ScrollStepPixels;
        }

        if (inputDelta == 0)
        {
            return;
        }

        _scrollOffset = Math.Clamp(_scrollOffset + inputDelta, 0, GetMaxScroll());
    }

    private int GetMaxScroll()
    {
        if (_cardRegions.Count == 0)
        {
            return 0;
        }

        var lastBottom = _cardRegions.Max(tuple => tuple.Region.Bottom);
        return Math.Max(0, lastBottom - _scrollViewport.Bottom);
    }

    private Rectangle ShiftForScroll(Rectangle region) => new(region.X, region.Y - _scrollOffset, region.Width, region.Height);

    private void DrawHoveredCardDetails(SpriteBatch spriteBatch, Texture2D pixel)
    {
        spriteBatch.Draw(pixel, _hoverDetailsRegion, new Color(24, 34, 52));

        var hoveredCardId = GetHoveredCardId();
        if (hoveredCardId is null || !_session.State.CardDefinitions.TryGetValue(hoveredCardId.Value, out var definition))
        {
            DebugTextRenderer.DrawText(spriteBatch, pixel, "HOVER A CARD FOR DETAILS", new Vector2(_hoverDetailsRegion.X + 10, _hoverDetailsRegion.Y + 12), Color.White, scale: 1);
            return;
        }

        var lines = new List<string>
        {
            $"NAME: {definition.Name}",
            $"ID: {definition.Id.Value}",
            $"COST: {definition.Cost}",
            $"PLAY COSTS: {FormatPlayCosts(definition)}",
            $"RARITY: {FormatField(definition.Rarity)}",
            $"AFFINITY: {FormatField(definition.DeckAffinity)}",
            $"LABELS: {FormatLabels(definition)}",
            "RULES:",
            CardRulesTextFormatter.GetReadableRulesText(definition),
        };

        var wrapped = WrapLines(lines, maxChars: 41);
        var maxLines = 30;
        for (var lineIndex = 0; lineIndex < wrapped.Count && lineIndex < maxLines; lineIndex++)
        {
            DebugTextRenderer.DrawText(
                spriteBatch,
                pixel,
                wrapped[lineIndex],
                new Vector2(_hoverDetailsRegion.X + 10, _hoverDetailsRegion.Y + 12 + (lineIndex * 12)),
                Color.White,
                scale: 1);
        }
    }

    private CardId? GetHoveredCardId()
    {
        foreach (var (cardId, region) in _cardRegions)
        {
            var shifted = ShiftForScroll(region);
            if (shifted.Intersects(_scrollViewport) && shifted.Contains(_input.MousePosition))
            {
                return cardId;
            }
        }

        return null;
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

    private static string FormatLabels(CardDefinition definition)
    {
        return definition.LabelsOrEmpty.Count == 0
            ? "-"
            : string.Join(", ", definition.LabelsOrEmpty.OrderBy(label => label, StringComparer.OrdinalIgnoreCase));
    }

    private static string FormatField(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

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

    private static void DrawButton(SpriteBatch spriteBatch, Texture2D pixel, Rectangle region, string label)
    {
        spriteBatch.Draw(pixel, region, new Color(146, 167, 213));
        DebugTextRenderer.DrawText(spriteBatch, pixel, label, new Vector2(region.X + 12, region.Y + 24), Color.Black);
    }
}
