using Game.Application;
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
    private readonly Rectangle _endTurnRegion = new(40, 300, 150, 60);
    private Texture2D? _pixel;

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
        spriteBatch.GraphicsDevice.Clear(Color.DarkRed);
        EnsurePixel(spriteBatch.GraphicsDevice);

        spriteBatch.Begin();
        foreach (var cardRegion in _cardRegions)
        {
            spriteBatch.Draw(_pixel!, cardRegion, Color.LightGray);
        }

        spriteBatch.Draw(_pixel!, _endTurnRegion, Color.DarkOrange);
        spriteBatch.End();
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
            var x = 40 + (index * 110);
            _cardRegions.Add(new Rectangle(x, 420, 90, 130));
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

    private void EnsurePixel(GraphicsDevice graphicsDevice)
    {
        if (_pixel is not null)
        {
            return;
        }

        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }
}
