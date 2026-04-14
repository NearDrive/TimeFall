using Game.Application;
using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Game;
using Game.Core.Map;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Game.Client.Screens;

public sealed class CombatScreen : IScreen
{
    private readonly IGameSession _session;
    private readonly InputHandler _input;
    private readonly List<Rectangle> _cardRegions = new();
    private readonly Rectangle _endTurnRegion = new(950, 610, 220, 70);

    public CombatScreen(IGameSession session, InputHandler input)
    {
        _session = session;
        _input = input;
    }

    public void Update(GameTime time)
    {
        _ = time;
        EnsureDebugCombatState();
        BuildCardRegions();

        for (var index = 0; index < _cardRegions.Count; index++)
        {
            if (_input.IsLeftClick(_cardRegions[index]))
            {
                _session.ApplyPlayerAction(new PlayCardAction(index));
                BuildCardRegions();
                return;
            }
        }

        if (_input.IsLeftClick(_endTurnRegion) || _input.IsKeyPressed(Keys.E))
        {
            _session.ApplyPlayerAction(new EndTurnAction());
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
        var font = RenderPrimitives.Font;

        if (_session.State.Phase != GamePhase.Combat || _session.State.Combat is null)
        {
            spriteBatch.DrawString(font, "No active combat. Press F2 for Map or F3 to auto-start combat.", new Vector2(20, 20), Color.White);
            return;
        }

        var combat = _session.State.Combat;

        DrawPlayerPanel(spriteBatch, pixel, font, combat.Player);
        DrawEnemyPanels(spriteBatch, pixel, font, combat.Enemies);
        DrawHand(spriteBatch, pixel, font, combat.Player.Deck.Hand);

        spriteBatch.Draw(pixel, _endTurnRegion, Color.DarkOrange);
        spriteBatch.DrawString(font, "End Turn (E)", new Vector2(_endTurnRegion.X + 18, _endTurnRegion.Y + 22), Color.Black);
        spriteBatch.DrawString(font, $"Turn: {combat.TurnOwner}", new Vector2(950, 575), Color.White);
    }

    private static void DrawPlayerPanel(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, CombatEntity player)
    {
        var playerPanel = new Rectangle(20, 20, 380, 160);
        spriteBatch.Draw(pixel, playerPanel, new Color(35, 49, 64));

        spriteBatch.DrawString(font, "Player", new Vector2(playerPanel.X + 12, playerPanel.Y + 10), Color.White);
        spriteBatch.DrawString(font, $"HP: {player.HP}/{player.MaxHP}", new Vector2(playerPanel.X + 12, playerPanel.Y + 40), Color.White);
        spriteBatch.DrawString(font, $"Armor: {player.Armor}", new Vector2(playerPanel.X + 12, playerPanel.Y + 65), Color.White);
        spriteBatch.DrawString(font, $"Resources: {FormatResources(player.Resources)}", new Vector2(playerPanel.X + 12, playerPanel.Y + 90), Color.White);
        spriteBatch.DrawString(font, $"Draw/Discard/Burn: {player.Deck.DrawPile.Count}/{player.Deck.DiscardPile.Count}/{player.Deck.BurnPile.Count}", new Vector2(playerPanel.X + 12, playerPanel.Y + 115), Color.White);
    }

    private static void DrawEnemyPanels(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, IReadOnlyList<CombatEntity> enemies)
    {
        const int startX = 430;
        const int startY = 20;
        const int panelWidth = 340;
        const int panelHeight = 130;
        const int gap = 16;

        for (var index = 0; index < enemies.Count; index++)
        {
            var enemy = enemies[index];
            var panel = new Rectangle(startX + ((panelWidth + gap) * index), startY, panelWidth, panelHeight);
            spriteBatch.Draw(pixel, panel, new Color(77, 39, 39));

            var enemyTitle = _sessionEnemyLabel(enemy, index);
            spriteBatch.DrawString(font, enemyTitle, new Vector2(panel.X + 12, panel.Y + 10), Color.White);
            spriteBatch.DrawString(font, $"HP: {enemy.HP}/{enemy.MaxHP}", new Vector2(panel.X + 12, panel.Y + 40), Color.White);
            spriteBatch.DrawString(font, $"Armor: {enemy.Armor}", new Vector2(panel.X + 12, panel.Y + 65), Color.White);
            spriteBatch.DrawString(font, $"Resources: {FormatResources(enemy.Resources)}", new Vector2(panel.X + 12, panel.Y + 90), Color.White);
        }

        static string _sessionEnemyLabel(CombatEntity enemy, int index) => $"Enemy {index + 1}: {enemy.EntityId}";
    }

    private void DrawHand(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, IReadOnlyList<CardInstance> hand)
    {
        for (var index = 0; index < hand.Count && index < _cardRegions.Count; index++)
        {
            var cardRegion = _cardRegions[index];
            spriteBatch.Draw(pixel, cardRegion, new Color(186, 193, 204));

            var card = hand[index];
            var definition = TryGetCardDefinition(card.DefinitionId);
            var title = definition?.Name ?? card.DefinitionId.Value;
            var cost = definition?.Cost ?? 0;

            spriteBatch.DrawString(font, $"[{index}] {title}", new Vector2(cardRegion.X + 8, cardRegion.Y + 10), Color.Black);
            spriteBatch.DrawString(font, $"Cost: {cost}", new Vector2(cardRegion.X + 8, cardRegion.Y + 36), Color.Black);
            spriteBatch.DrawString(font, card.DefinitionId.Value, new Vector2(cardRegion.X + 8, cardRegion.Y + 62), Color.Black);
        }
    }

    private CardDefinition? TryGetCardDefinition(CardId cardId)
    {
        return _session.State.CardDefinitions.TryGetValue(cardId, out var definition)
            ? definition
            : null;
    }

    private void BuildCardRegions()
    {
        _cardRegions.Clear();

        if (_session.State.Phase != GamePhase.Combat || _session.State.Combat is null)
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

    private void EnsureDebugCombatState()
    {
        if (_session.State.Phase == GamePhase.Combat)
        {
            return;
        }

        if (_session.State.Phase != GamePhase.MapExploration)
        {
            return;
        }

        var firstEnemy = _session.State.EnemyDefinitions.Values.FirstOrDefault();
        if (firstEnemy is null)
        {
            return;
        }

        var blueprint = EnemyEncounterFactory.CreateBlueprint(firstEnemy);
        _session.ApplyPlayerAction(new BeginCombatAction(blueprint, _session.State.CardDefinitions, _session.State.EnabledRewardPoolCardIds));
    }

    private static string FormatResources(IReadOnlyDictionary<ResourceType, int> resources)
    {
        if (resources.Count == 0)
        {
            return "None";
        }

        return string.Join(", ", resources.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key}:{entry.Value}"));
    }
}
