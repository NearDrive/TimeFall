using Game.Application;
using Game.Core.Cards;
using Game.Core.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Screens;

public sealed class SandboxDeckEditScreen : IScreen
{
    private readonly IGameSession _session;
    private readonly InputHandler _input;
    private readonly IClientActionDispatcher _dispatcher;

    private readonly List<(CardId CardId, Rectangle Region)> _cardRegions = new();
    private readonly Rectangle _clearRegion = new(960, 120, 260, 70);
    private readonly Rectangle _enemySelectRegion = new(960, 210, 260, 70);
    private readonly Rectangle _backDeckSelectRegion = new(960, 300, 260, 70);

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

        foreach (var (cardId, region) in _cardRegions)
        {
            if (_input.IsLeftClick(region))
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
        DebugTextRenderer.DrawText(spriteBatch, pixel, "CLICK A CARD TO TOGGLE EQUIP", new Vector2(20, 80), Color.White);

        foreach (var (cardId, region) in _cardRegions)
        {
            var equippedCard = equipped.Contains(cardId);
            spriteBatch.Draw(pixel, region, equippedCard ? Color.Gold : new Color(173, 184, 207));

            var definition = _session.State.CardDefinitions.TryGetValue(cardId, out var card) ? card : null;
            var name = definition?.Name ?? cardId.Value;
            var cost = definition?.Cost ?? 0;

            DebugTextRenderer.DrawText(spriteBatch, pixel, name, new Vector2(region.X + 8, region.Y + 10), Color.Black);
            DebugTextRenderer.DrawText(spriteBatch, pixel, $"ID: {cardId.Value}", new Vector2(region.X + 8, region.Y + 35), Color.Black);
            DebugTextRenderer.DrawText(spriteBatch, pixel, $"COST: {cost}", new Vector2(region.X + 8, region.Y + 60), Color.Black);
            DebugTextRenderer.DrawText(spriteBatch, pixel, equippedCard ? "EQUIPPED" : "NOT EQUIPPED", new Vector2(region.X + 8, region.Y + 85), Color.Black);
        }

        DrawButton(spriteBatch, pixel, _clearRegion, "CLEAR LOADOUT");
        DrawButton(spriteBatch, pixel, _enemySelectRegion, "NEXT: ENEMY");
        DrawButton(spriteBatch, pixel, _backDeckSelectRegion, "CHANGE DECK");

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
            var x = 20 + (index % 4) * 230;
            var y = 130 + (index / 4) * 120;
            _cardRegions.Add((allCardIds[index], new Rectangle(x, y, 210, 110)));
        }
    }

    private static void DrawButton(SpriteBatch spriteBatch, Texture2D pixel, Rectangle region, string label)
    {
        spriteBatch.Draw(pixel, region, new Color(146, 167, 213));
        DebugTextRenderer.DrawText(spriteBatch, pixel, label, new Vector2(region.X + 12, region.Y + 24), Color.Black);
    }
}
