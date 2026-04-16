using Game.Application;
using Game.Core.Game;
using Game.Core.Map;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Screens;

public sealed class SandboxEnemySelectScreen : IScreen
{
    private readonly IGameSession _session;
    private readonly InputHandler _input;
    private readonly IClientActionDispatcher _dispatcher;

    private readonly List<(string EnemyId, Rectangle Region)> _enemyRegions = new();
    private readonly Rectangle _startCombatRegion = new(950, 120, 280, 80);
    private readonly Rectangle _returnDeckEditRegion = new(950, 220, 280, 80);

    public SandboxEnemySelectScreen(IGameSession session, InputHandler input, IClientActionDispatcher dispatcher)
    {
        _session = session;
        _input = input;
        _dispatcher = dispatcher;
    }

    public void Update(GameTime time)
    {
        _ = time;
        BuildEnemyRegions();

        foreach (var (enemyId, region) in _enemyRegions)
        {
            if (_input.IsLeftClick(region))
            {
                _dispatcher.Dispatch(new SelectSandboxEnemyAction(enemyId));
                return;
            }
        }

        if (_input.IsLeftClick(_startCombatRegion))
        {
            _dispatcher.Dispatch(new StartSandboxCombatAction());
            return;
        }

        if (_input.IsLeftClick(_returnDeckEditRegion))
        {
            _dispatcher.Dispatch(new OpenSandboxDeckEditAction());
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        spriteBatch.GraphicsDevice.Clear(new Color(45, 24, 24));

        spriteBatch.Begin();
        var pixel = RenderPrimitives.Pixel;

        var selectedEnemyId = _session.State.Sandbox?.SelectedEnemyId;

        DebugTextRenderer.DrawText(spriteBatch, pixel, "SANDBOX: SELECT ENEMY", new Vector2(20, 20), Color.White);
        DebugTextRenderer.DrawText(spriteBatch, pixel, $"SELECTED: {selectedEnemyId ?? "NONE"}", new Vector2(20, 50), Color.White);

        foreach (var (enemyId, region) in _enemyRegions)
        {
            var selected = string.Equals(enemyId, selectedEnemyId, StringComparison.Ordinal);
            spriteBatch.Draw(pixel, region, selected ? Color.Gold : new Color(164, 137, 137));

            DrawEnemyCard(spriteBatch, pixel, ResolveEnemyDefinition(enemyId), region);
        }

        DrawButton(spriteBatch, pixel, _startCombatRegion, "START COMBAT");
        DrawButton(spriteBatch, pixel, _returnDeckEditRegion, "BACK TO LOADOUT");

        spriteBatch.End();
    }

    private void BuildEnemyRegions()
    {
        _enemyRegions.Clear();

        var enemyIds = SandboxEnemyCatalog.GetEnemyIds(_session.State.EnemyDefinitions);
        for (var index = 0; index < enemyIds.Count; index++)
        {
            var x = 20 + (index % 3) * 300;
            var y = 110 + (index / 3) * 130;
            _enemyRegions.Add((enemyIds[index], new Rectangle(x, y, 280, 110)));
        }
    }

    private static void DrawEnemyCard(SpriteBatch spriteBatch, Texture2D pixel, EnemyDefinition enemy, Rectangle region)
    {
        DebugTextRenderer.DrawText(spriteBatch, pixel, enemy.Name, new Vector2(region.X + 8, region.Y + 8), Color.Black);
        DebugTextRenderer.DrawText(spriteBatch, pixel, $"ID: {enemy.Id}", new Vector2(region.X + 8, region.Y + 33), Color.Black);
        var hpLabel = enemy.Hp == int.MaxValue ? "∞" : enemy.Hp.ToString();
        DebugTextRenderer.DrawText(spriteBatch, pixel, $"HP: {hpLabel}", new Vector2(region.X + 8, region.Y + 58), Color.Black);
        DebugTextRenderer.DrawText(spriteBatch, pixel, $"TIER: {enemy.Tier}", new Vector2(region.X + 8, region.Y + 83), Color.Black);
    }

    private EnemyDefinition ResolveEnemyDefinition(string enemyId)
    {
        if (_session.State.EnemyDefinitions.TryGetValue(enemyId, out var enemyDefinition))
        {
            return enemyDefinition;
        }

        if (string.Equals(enemyId, SandboxEnemyCatalog.InfiniteHpEnemyId, StringComparison.Ordinal))
        {
            return new EnemyDefinition(
                Id: SandboxEnemyCatalog.InfiniteHpEnemyId,
                Name: "Training Dummy (Infinite HP)",
                Zone: 0,
                Tier: "Sandbox",
                Category: "Special",
                Role: "Dummy",
                Hp: int.MaxValue,
                StartingArmor: 0,
                Deck: [],
                Tags: ["sandbox"],
                Notes: "Only available in sandbox mode.");
        }

        throw new InvalidOperationException($"Unknown sandbox enemy '{enemyId}'.");
    }

    private static void DrawButton(SpriteBatch spriteBatch, Texture2D pixel, Rectangle region, string label)
    {
        spriteBatch.Draw(pixel, region, new Color(200, 165, 112));
        DebugTextRenderer.DrawText(spriteBatch, pixel, label, new Vector2(region.X + 18, region.Y + 28), Color.Black);
    }
}
