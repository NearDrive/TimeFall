using Game.Application;
using Game.Core.Cards;
using Game.Core.Game;
using Game.Core.Rewards;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Game.Client.Screens;

public sealed class RewardScreen : IScreen
{
    private readonly IGameSession _session;
    private readonly InputHandler _input;
    private readonly IClientActionDispatcher _dispatcher;
    private readonly List<(CardId CardId, Rectangle Region)> _cardOptionRegions = new();
    private readonly Rectangle _skipRegion = new(980, 610, 220, 70);

    public RewardScreen(IGameSession session, InputHandler input, IClientActionDispatcher dispatcher)
    {
        _session = session;
        _input = input;
        _dispatcher = dispatcher;
    }

    public void Update(GameTime time)
    {
        _ = time;
        BuildRegions();

        if (_session.State.Phase != GamePhase.RewardSelection || _session.State.Reward is null)
        {
            return;
        }

        foreach (var (cardId, region) in _cardOptionRegions)
        {
            if (_input.IsLeftClick(region))
            {
                _dispatcher.Dispatch(new ChooseRewardCardAction(cardId));
                return;
            }
        }

        if (_input.IsLeftClick(_skipRegion) || _input.IsKeyPressed(Keys.S))
        {
            _dispatcher.Dispatch(new SkipRewardAction());
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        spriteBatch.GraphicsDevice.Clear(new Color(24, 24, 42));

        spriteBatch.Begin();
        DrawRewardState(spriteBatch);
        spriteBatch.End();
    }

    private void DrawRewardState(SpriteBatch spriteBatch)
    {
        var pixel = RenderPrimitives.Pixel;

        if (_session.State.Phase != GamePhase.RewardSelection || _session.State.Reward is null)
        {
            DebugTextRenderer.DrawText(spriteBatch, pixel, "NO ACTIVE REWARD", new Vector2(20, 20), Color.White);
            return;
        }

        var reward = _session.State.Reward;

        DebugTextRenderer.DrawText(spriteBatch, pixel, "REWARD", new Vector2(20, 20), Color.White);
        DebugTextRenderer.DrawText(spriteBatch, pixel, $"TYPE: {reward.RewardType}", new Vector2(20, 50), Color.White);

        if (reward.RewardType != RewardType.CardChoice)
        {
            DebugTextRenderer.DrawText(spriteBatch, pixel, "UNSUPPORTED REWARD TYPE", new Vector2(20, 85), Color.OrangeRed);
            return;
        }

        for (var index = 0; index < _cardOptionRegions.Count; index++)
        {
            var (cardId, region) = _cardOptionRegions[index];
            spriteBatch.Draw(pixel, region, new Color(190, 205, 230));

            var definition = TryGetCardDefinition(cardId);
            var title = definition?.Name ?? cardId.Value;
            var cost = definition?.Cost ?? 0;

            DebugTextRenderer.DrawText(spriteBatch, pixel, $"[{index + 1}] {title}", new Vector2(region.X + 8, region.Y + 10), Color.Black);
            DebugTextRenderer.DrawText(spriteBatch, pixel, $"COST: {cost}", new Vector2(region.X + 8, region.Y + 35), Color.Black);
            DebugTextRenderer.DrawText(spriteBatch, pixel, cardId.Value, new Vector2(region.X + 8, region.Y + 60), Color.Black);
        }

        spriteBatch.Draw(pixel, _skipRegion, new Color(170, 120, 80));
        DebugTextRenderer.DrawText(spriteBatch, pixel, "SKIP (S)", new Vector2(_skipRegion.X + 58, _skipRegion.Y + 22), Color.Black);
    }

    private CardDefinition? TryGetCardDefinition(CardId cardId)
    {
        return _session.State.CardDefinitions.TryGetValue(cardId, out var definition)
            ? definition
            : null;
    }

    private void BuildRegions()
    {
        _cardOptionRegions.Clear();

        if (_session.State.Phase != GamePhase.RewardSelection || _session.State.Reward is null)
        {
            return;
        }

        var reward = _session.State.Reward;
        for (var index = 0; index < reward.CardOptions.Count; index++)
        {
            var x = 20 + (index * 200);
            _cardOptionRegions.Add((reward.CardOptions[index], new Rectangle(x, 120, 180, 160)));
        }
    }
}
