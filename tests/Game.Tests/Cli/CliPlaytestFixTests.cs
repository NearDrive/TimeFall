using System.Collections.Immutable;
using System.IO;
using Game.Cli;
using Game.Core.Combat;
using Game.Core.Content;
using Game.Core.Rewards;
using Game.Core.Game;
using Game.Core.Map;
using Game.Core.TimeSystem;
using Game.Data.Content;

namespace Game.Tests.Cli;

public sealed class CliPlaytestFixTests
{
    private static readonly GameContentBundle Content = StaticGameContentProvider.LoadDefault();

    [Fact]
    public void OverflowDiscard_CommandPath_IsResolvable()
    {
        var parsed = CliCommandParser.TryParse("discard 0", out var command, out var error);

        Assert.True(parsed, error);
        var action = Assert.IsType<DiscardOverflowAction>(command.Action);
        Assert.Equal([0], action.Indexes);

        var state = CreateOverflowPendingCombatState(requiredDiscards: 1);
        var (nextState, events) = GameReducer.Reduce(state, action);

        Assert.False(nextState.Combat!.NeedsOverflowDiscard);
        Assert.Contains(events, e => e is CardDiscarded);
    }

    [Fact]
    public void OverflowDiscard_State_DoesNotSoftLockPlaytestFlow()
    {
        var state = CreateOverflowPendingCombatState(requiredDiscards: 1);

        var (afterDiscard, discardEvents) = GameReducer.Reduce(state, new DiscardOverflowAction([0]));
        Assert.False(afterDiscard.Combat!.NeedsOverflowDiscard);
        Assert.NotEmpty(discardEvents);

        var (afterPlay, playEvents) = GameReducer.Reduce(afterDiscard, new PlayCardAction(0));
        Assert.NotEmpty(playEvents);
        Assert.NotEqual(afterDiscard, afterPlay);
    }


    [Fact]
    public void CliRenderer_Combat_ShowsPileSizes()
    {
        var state = CreateOverflowPendingCombatState(requiredDiscards: 1);

        var output = CaptureConsole(() => CliRenderer.RenderState(state, [], Content.CardDefinitions));

        Assert.Contains("Player piles: draw", output);
        Assert.Contains("| hand", output);
        Assert.Contains("| discard", output);
        Assert.Contains("| burn", output);
        Assert.Contains("Enemy piles: draw", output);
    }

    [Fact]
    public void Renderer_UsesCorrectHpLabels()
    {
        var state = CreateOverflowPendingCombatState(requiredDiscards: 1) with { RunHp = 65, RunMaxHp = 80 };
        var withArmor = state with
        {
            Combat = state.Combat! with
            {
                Player = state.Combat.Player with { Armor = 9 },
            },
        };

        var output = CaptureConsole(() => CliRenderer.RenderState(withArmor, [], Content.CardDefinitions));

        Assert.Contains("Run HP: 65/80", output);
        Assert.DoesNotContain("Run HP/Armor", output);
        Assert.Contains("Combat Player Armor: 9", output);
    }

    [Fact]
    public void CliHelp_ClarifiesIndexing()
    {
        var output = CaptureConsole(() => CliRenderer.RenderHelp(GamePhase.MapExploration));

        Assert.Contains("0-based", output);
        Assert.Contains("discardpile", output);
    }



    [Fact]
    public void CliRenderer_ShowsTimeProgress()
    {
        var state = CreateMapExplorationState();

        var output = CaptureConsole(() => CliRenderer.RenderState(state, [], Content.CardDefinitions));

        Assert.Contains("Time step: 0 | Progress: 0/4 | Caught: False", output);
    }


    [Fact]
    public void CliRenderer_Hand_ShowsCardDescriptions()
    {
        var state = CreateOverflowPendingCombatState(requiredDiscards: 1);

        var output = CaptureConsole(() => CliRenderer.RenderHand(state, Content.CardDefinitions));

        Assert.Contains("Strike — Deal 4 damage", output);
        Assert.Contains("Guard — Gain 3 armor.", output);
    }

    [Fact]
    public void CliRenderer_RewardOptions_ShowDescriptions()
    {
        var state = CreateMapExplorationState() with
        {
            Reward = new RewardState(
                RewardType.CardChoice,
                ImmutableList.Create(PlaytestContent.StrikeCardId, PlaytestContent.QuickDrawCardId),
                false,
                new NodeId("combat-1"))
        };

        var output = CaptureConsole(() => CliRenderer.RenderState(state, [], Content.CardDefinitions));

        Assert.Contains("Strike — Deal 4 damage", output);
        Assert.Contains("Quick Draw — Draw 1 card.", output);
    }

    [Fact]
    public void CliRenderer_CombatLog_IsMoreDescriptive()
    {
        var events = new GameEvent[]
        {
            new TurnEnded(TurnOwner.Enemy),
            new EnemyAttackPlayed(new CardInstance(PlaytestContent.EnemyAttackCardId), 5, 80, 75, 2, 1, 2),
        };

        var output = CaptureConsole(() => CliRenderer.RenderState(CreateMapExplorationState(), events, Content.CardDefinitions));

        Assert.Contains("Enemy turn begins", output);
        Assert.Contains("[Enemy] Uses Enemy Attack", output);
        Assert.Contains("Player HP 80 -> 75", output);
        Assert.Contains("2 blocked", output);
    }

    [Fact]
    public void CliRenderer_TurnBoundaries_AreVisible()
    {
        var events = new GameEvent[]
        {
            new TurnEnded(TurnOwner.Enemy),
            new TurnEnded(TurnOwner.Player),
        };

        var output = CaptureConsole(() => CliRenderer.RenderState(CreateMapExplorationState(), events, Content.CardDefinitions));

        Assert.Contains("---------------- Enemy turn begins ----------------", output);
        Assert.Contains("---------------- Player turn begins ----------------", output);
    }

    [Fact]
    public void DamageEvent_Rendering_ShowsHpAndArmorImpact()
    {
        var evt = new EnemyAttackPlayed(new CardInstance(PlaytestContent.EnemyHeavyAttackCardId), 9, 72, 65, 2, 1, 2);

        var rendered = CliRenderer.FormatEvent(evt, Content.CardDefinitions);

        Assert.Contains("9 incoming", rendered);
        Assert.Contains("Player HP 72 -> 65", rendered);
        Assert.Contains("Armor 2 -> 1", rendered);
        Assert.Contains("2 blocked", rendered);
    }

    [Fact]
    public void CliMove_CanUseAdjacentIndex()
    {
        var state = CreateMapExplorationState();
        var parsedFirst = CliCommandParser.TryParse("move 0", out var firstCommand, out var firstError);
        Assert.True(parsedFirst, firstError);

        var resolvedFirst = CliLoop.ResolveContextualAction(firstCommand, state);
        var firstMove = Assert.IsType<MoveToNodeAction>(resolvedFirst);
        Assert.Equal(new NodeId("combat-1"), firstMove.NodeId);

        var parsed = CliCommandParser.TryParse("move 1", out var command, out var error);

        Assert.True(parsed, error);
        var resolved = CliLoop.ResolveContextualAction(command, state);

        var move = Assert.IsType<MoveToNodeAction>(resolved);
        Assert.Equal(new NodeId("shop-1"), move.NodeId);
    }

    [Fact]
    public void MapRender_ShowsDeterministicAdjacentIndexes()
    {
        var output = CaptureConsole(() => CliRenderer.RenderMap(CreateMapExplorationState()));

        Assert.Contains("[0] combat-1", output);
        Assert.Contains("[1] shop-1", output);
    }


    [Fact]
    public void CliTargetIndex_MatchesRenderedEnemyOrder()
    {
        var state = CreateDuplicateEnemyCombatState();

        var output = CaptureConsole(() => CliRenderer.RenderState(state, [], Content.CardDefinitions));
        Assert.Contains("[0] zone1-raider#1", output);
        Assert.Contains("[1] zone1-bastion-guard", output);
        Assert.Contains("[2] zone1-raider#2", output);

        var strikeIndex = state.Combat!.Player.Deck.Hand.FindIndex(card => card.DefinitionId.Value == "blades-strike");
        var targetBefore = state.Combat.Enemies[2].HP;
        var otherBefore = state.Combat.Enemies[0].HP;

        var result = GameReducer.Reduce(state, new PlayCardAction(strikeIndex, 2));

        Assert.Equal(targetBefore - 5, result.NewState.Combat!.Enemies[2].HP);
        Assert.Equal(otherBefore, result.NewState.Combat.Enemies[0].HP);
    }

    [Fact]
    public void CliMove_NodeIdPathStillWorks()
    {
        var state = CreateMapExplorationState();
        var parsed = CliCommandParser.TryParse("move combat-1", out var command, out var error);

        Assert.True(parsed, error);
        var resolved = CliLoop.ResolveContextualAction(command, state) ?? command.Action;

        var move = Assert.IsType<MoveToNodeAction>(resolved);
        Assert.Equal(new NodeId("combat-1"), move.NodeId);
    }

    [Fact]
    public void StartCommand_UsesDeterministicDefaultSeed_WhenOmitted()
    {
        var ok = CliCommandParser.TryParse("start", out var command, out var error);

        Assert.True(ok, error);
        var start = Assert.IsType<StartRunAction>(command.Action);
        Assert.Equal(CliCommandParser.DefaultSeed, start.Seed);
    }

    [Fact]
    public void CliDecksCommand_ShowsAvailableDecks()
    {
        var state = GameState.CreateInitial(Content.CardDefinitions, Content.DeckDefinitions, Content.RewardCardPool, Content.EnemyDefinitions, Content.Zone1SpawnTable);

        var output = CaptureConsole(() => CliRenderer.RenderDecks(state));

        Assert.Contains("deck-blades", output);
        Assert.Contains("Resource: Momentum", output);
    }

    [Fact]
    public void CliSelectCommand_CanUseIndexOrDeckId()
    {
        var state = GameState.CreateInitial(Content.CardDefinitions, Content.DeckDefinitions, Content.RewardCardPool, Content.EnemyDefinitions, Content.Zone1SpawnTable);

        var byIndexParsed = CliCommandParser.TryParse("select 0", out var byIndex, out var byIndexError);
        Assert.True(byIndexParsed, byIndexError);
        var byIndexAction = CliLoop.ResolveContextualAction(byIndex, state);
        Assert.IsType<SelectDeckAction>(byIndexAction);
        Assert.Equal("deck-blades", ((SelectDeckAction)byIndexAction!).DeckId);

        var byIdParsed = CliCommandParser.TryParse("select deck-blades", out var byId, out var byIdError);
        Assert.True(byIdParsed, byIdError);
        var byIdAction = CliLoop.ResolveContextualAction(byId, state);
        Assert.IsType<SelectDeckAction>(byIdAction);
        Assert.Equal("deck-blades", ((SelectDeckAction)byIdAction!).DeckId);
    }


    private static GameState CreateMapExplorationState()
    {
        var map = SampleMapFactory.CreateDefaultState();
        return GameStateTestFactory.CreateStartedRun() with
        {
            Map = map,
            Time = TimeState.Create(map),
        };
    }

    private static GameState CreateOverflowPendingCombatState(int requiredDiscards)
    {
        var started = GameStateTestFactory.CreateStartedRun(123);
        var combat = GameReducer.Reduce(started, new BeginCombatAction(Content.OpeningCombat, Content.CardDefinitions)).NewState;

        return combat with
        {
            Combat = combat.Combat! with
            {
                NeedsOverflowDiscard = true,
                RequiredOverflowDiscardCount = requiredDiscards,
            },
        };
    }


    private static GameState CreateDuplicateEnemyCombatState()
    {
        var bladesDeck = Content.DeckDefinitions["deck-blades"];
        var player = new CombatantBlueprint(
            EntityId: "player",
            HP: bladesDeck.BaseMaxHp,
            MaxHP: bladesDeck.BaseMaxHp,
            Armor: 0,
            Resources: bladesDeck.StartingResources,
            DrawPile: bladesDeck.StartingDeck);

        var enemyIds = new[] { "zone1-raider", "zone1-bastion-guard", "zone1-raider" };
        var enemies = enemyIds
            .Select(enemyId => Content.EnemyDefinitions[enemyId])
            .Select(enemy => new CombatantBlueprint(
                EntityId: enemy.Id,
                HP: enemy.Hp,
                MaxHP: enemy.Hp,
                Armor: enemy.StartingArmor,
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                DrawPile: enemy.Deck))
            .ToArray();

        var state = GameState.CreateInitial(Content.CardDefinitions, Content.DeckDefinitions, Content.RewardCardPool, Content.EnemyDefinitions, Content.Zone1SpawnTable) with
        {
            SelectedDeckId = "deck-blades",
        };

        return GameReducer.Reduce(state, new BeginCombatAction(new CombatBlueprint(player, enemies), Content.CardDefinitions)).NewState;
    }

    private static string CaptureConsole(Action render)
    {
        var writer = new StringWriter();
        var previousOut = Console.Out;
        Console.SetOut(writer);
        try
        {
            render();
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(previousOut);
        }
    }
}
