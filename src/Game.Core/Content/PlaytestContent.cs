using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Game;
using Game.Core.Map;
using System.Collections.Immutable;

namespace Game.Core.Content;

public static class PlaytestContent
{
    public static readonly CardId StrikeCardId = new("strike");
    public static readonly CardId GuardCardId = new("guard");
    public static readonly CardId QuickDrawCardId = new("quick-draw");
    public static readonly CardId HeavyAttackCardId = new("heavy-attack");
    public static readonly CardId FeintCardId = new("feint");
    public static readonly CardId EnemyAttackCardId = new("enemy-attack");
    public static readonly CardId EnemyFortifyCardId = new("enemy-fortify");
    public static readonly CardId EnemyHeavyAttackCardId = new("enemy-heavy-attack");

    public static readonly ImmutableDictionary<CardId, CardDefinition> CardDefinitions =
        new Dictionary<CardId, CardDefinition>
        {
            [StrikeCardId] = new(StrikeCardId, "Strike", 1, [new DamageCardEffect(4, CardTarget.Opponent)]),
            [GuardCardId] = new(GuardCardId, "Guard", 1, [new GainArmorCardEffect(3, CardTarget.Self)]),
            [QuickDrawCardId] = new(QuickDrawCardId, "Quick Draw", 1, [new DrawCardsCardEffect(1, CardTarget.Self)]),
            [HeavyAttackCardId] = new(HeavyAttackCardId, "Heavy Attack", 2, [new DamageCardEffect(8, CardTarget.Opponent)]),
            [FeintCardId] = new(FeintCardId, "Feint", 1, [new DamageCardEffect(2, CardTarget.Opponent), new GainArmorCardEffect(2, CardTarget.Self)]),
            [EnemyAttackCardId] = new(EnemyAttackCardId, "Enemy Attack", 1, [new DamageCardEffect(5, CardTarget.Opponent)]),
            [EnemyFortifyCardId] = new(EnemyFortifyCardId, "Enemy Fortify", 1, [new GainArmorCardEffect(4, CardTarget.Self)]),
            [EnemyHeavyAttackCardId] = new(EnemyHeavyAttackCardId, "Enemy Heavy Attack", 2, [new DamageCardEffect(9, CardTarget.Opponent)]),
        }.ToImmutableDictionary();

    public static readonly ImmutableArray<CardId> StarterDeck =
    [
        StrikeCardId,
        StrikeCardId,
        StrikeCardId,
        StrikeCardId,
        StrikeCardId,
        GuardCardId,
        GuardCardId,
        GuardCardId,
        QuickDrawCardId,
        QuickDrawCardId,
    ];

    public static readonly ImmutableArray<CardId> RewardCardPool =
    [
        StrikeCardId,
        GuardCardId,
        QuickDrawCardId,
        HeavyAttackCardId,
        FeintCardId,
    ];

    private static readonly CombatantBlueprint StandardEnemy = new(
        EntityId: "enemy-standard-blade-raider",
        HP: 28,
        MaxHP: 28,
        Armor: 0,
        Resources: ImmutableDictionary<ResourceType, int>.Empty,
        DrawPile:
        [
            EnemyAttackCardId,
            EnemyAttackCardId,
            EnemyFortifyCardId,
        ]);

    private static readonly CombatantBlueprint EliteEnemy = new(
        EntityId: "enemy-elite-duelist-captain",
        HP: 44,
        MaxHP: 44,
        Armor: 2,
        Resources: ImmutableDictionary<ResourceType, int>.Empty,
        DrawPile:
        [
            EnemyAttackCardId,
            EnemyHeavyAttackCardId,
            EnemyFortifyCardId,
            EnemyAttackCardId,
        ]);

    private static readonly CombatantBlueprint BossEnemy = new(
        EntityId: "enemy-boss-iron-warden",
        HP: 72,
        MaxHP: 72,
        Armor: 4,
        Resources: ImmutableDictionary<ResourceType, int>.Empty,
        DrawPile:
        [
            EnemyHeavyAttackCardId,
            EnemyAttackCardId,
            EnemyFortifyCardId,
            EnemyHeavyAttackCardId,
            EnemyAttackCardId,
        ]);

    public static CombatBlueprint OpeningCombat => CreateEncounter(NodeType.Combat);

    public static bool TryCreateEncounter(NodeType nodeType, out CombatBlueprint blueprint)
    {
        if (nodeType is not (NodeType.Combat or NodeType.Elite or NodeType.Boss))
        {
            blueprint = default!;
            return false;
        }

        blueprint = CreateEncounter(nodeType);
        return true;
    }

    private static CombatBlueprint CreateEncounter(NodeType nodeType)
    {
        var enemy = nodeType switch
        {
            NodeType.Combat => StandardEnemy,
            NodeType.Elite => EliteEnemy,
            NodeType.Boss => BossEnemy,
            _ => throw new InvalidOperationException($"Unsupported node type for encounter mapping: {nodeType}"),
        };

        return new CombatBlueprint(CreatePlayerBlueprint(), enemy);
    }

    private static CombatantBlueprint CreatePlayerBlueprint()
    {
        return new CombatantBlueprint(
            EntityId: "player",
            HP: 80,
            MaxHP: 80,
            Armor: 0,
            Resources: new Dictionary<ResourceType, int> { [ResourceType.Energy] = 3 },
            DrawPile: StarterDeck);
    }
}
