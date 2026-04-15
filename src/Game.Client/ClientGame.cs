using Game.Application;
using Game.Client.Screens;
using Game.Core.Combat;
using Game.Core.Game;
using Game.Core.Map;
using Game.Data.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Text;

namespace Game.Client;

public sealed class ClientGame : Microsoft.Xna.Framework.Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly GameContentBundle _content;
    private IGameSession _session;
    private readonly ScreenManager _screenManager;
    private readonly InputHandler _input;

    private SpriteBatch _spriteBatch = null!;

    public ClientGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 720;
        _content = StaticGameContentProvider.LoadDefault();
        _session = CreateBootstrappedSession();

        _input = new InputHandler();
        _screenManager = new ScreenManager(ScreenType.MainMenu, _session, _input);

        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        RenderPrimitives.Initialize(GraphicsDevice);
    }

    protected override void Update(GameTime gameTime)
    {
        _ = _session.State;
        _input.Update();

        if (_input.IsKeyPressed(Keys.F1))
        {
            _screenManager.SwitchTo(ScreenType.MainMenu);
        }

        if (_input.IsKeyPressed(Keys.F5))
        {
            RestartRun();
        }

        if (_input.IsKeyPressed(Keys.F6))
        {
            WriteDebugDump();
        }

        _screenManager.Update(gameTime);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        _screenManager.Draw(_spriteBatch);
        base.Draw(gameTime);
    }

    private IGameSession CreateBootstrappedSession()
    {
        var initialState = GameState.CreateInitial(
            _content.CardDefinitions,
            _content.DeckDefinitions,
            _content.RewardCardPool,
            _content.EnemyDefinitions,
            _content.Zone1SpawnTable);

        return new GameSession(initialState);
    }

    private void RestartRun()
    {
        _session = CreateBootstrappedSession();
        _screenManager.ResetSession(_session);
        Console.WriteLine($"[DEBUG] Restarted run via F5 at {DateTime.UtcNow:O}");
    }

    private void WriteDebugDump()
    {
        var dump = BuildDebugDump();
        Console.WriteLine(dump);
    }

    private string BuildDebugDump()
    {
        var state = _session.State;
        var lines = new List<string>
        {
            $"[DEBUG DUMP] {DateTime.UtcNow:O}",
            $"Screen: {_screenManager.CurrentScreenType}",
            $"Phase: {state.Phase}",
        };

        AppendCombatSummary(lines, state.Combat);
        AppendMapSummary(lines, state.Map, state.Phase);

        if (state.Combat is { } combat)
        {
            lines.Add($"DrawPileCount: {combat.Player.Deck.DrawPile.Count}");
            lines.Add($"DiscardPileCount: {combat.Player.Deck.DiscardPile.Count}");
        }
        else
        {
            lines.Add("DrawPileCount: N/A (not in combat)");
            lines.Add("DiscardPileCount: N/A (not in combat)");
        }

        lines.Add("RecentEvents:");
        foreach (var gameEvent in _screenManager.RecentEvents.TakeLast(12))
        {
            lines.Add($"- {DebugSummaryFormatter.FormatGameEvent(gameEvent)}");
        }

        if (_screenManager.RecentEvents.Count == 0)
        {
            lines.Add("- (none)");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static void AppendCombatSummary(List<string> lines, CombatState? combat)
    {
        if (combat is null)
        {
            lines.Add("Combat: none");
            return;
        }

        lines.Add($"CombatTurnOwner: {combat.TurnOwner}");
        lines.Add($"Player: HP {combat.Player.HP}/{combat.Player.MaxHP}, Armor {combat.Player.Armor}, Resources {FormatResources(combat.Player.Resources)}");

        var enemies = combat.Enemies
            .Select(enemy => $"{enemy.EntityId} HP {enemy.HP}/{enemy.MaxHP} ARM {enemy.Armor}")
            .ToArray();
        lines.Add($"Enemies: {(enemies.Length == 0 ? "none" : string.Join(" | ", enemies))}");
    }

    private static void AppendMapSummary(List<string> lines, MapState map, GamePhase phase)
    {
        lines.Add($"MapCurrentNode: {map.CurrentNodeId.Value}");
        var neighbors = map.Graph.GetNeighbors(map.CurrentNodeId).Select(node => node.Value).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        lines.Add($"MapAdjacentNodes: {(neighbors.Length == 0 ? "none" : string.Join(", ", neighbors))}");
        lines.Add($"MapVisitedCount: {map.VisitedNodeIds.Count}");
        if (phase == GamePhase.MapExploration)
        {
            lines.Add("MapState: active");
        }
    }

    private static string FormatResources(IReadOnlyDictionary<ResourceType, int> resources)
    {
        if (resources.Count == 0)
        {
            return "none";
        }

        var builder = new StringBuilder();
        foreach (var pair in resources.OrderBy(pair => pair.Key))
        {
            if (builder.Length > 0)
            {
                builder.Append(", ");
            }

            builder.Append(pair.Key);
            builder.Append(':');
            builder.Append(pair.Value);
        }

        return builder.ToString();
    }
}
