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
            [StrikeCardId] = new(StrikeCardId, "Strike", 1, [new DamageCardEffect(4, CardTarget.Opponent)], RulesText: "Deal 4 damage."),
            [GuardCardId] = new(GuardCardId, "Guard", 1, [new GainArmorCardEffect(3, CardTarget.Self)], RulesText: "Gain 3 armor."),
            [QuickDrawCardId] = new(QuickDrawCardId, "Quick Draw", 1, [new DrawCardsCardEffect(1, CardTarget.Self)], RulesText: "Draw 1 card."),
            [HeavyAttackCardId] = new(HeavyAttackCardId, "Heavy Attack", 2, [new DamageCardEffect(8, CardTarget.Opponent)], RulesText: "Deal 8 damage."),
            [FeintCardId] = new(FeintCardId, "Feint", 1, [new DamageCardEffect(2, CardTarget.Opponent), new GainArmorCardEffect(2, CardTarget.Self)], RulesText: "Deal 2 damage. Gain 2 armor."),
            [EnemyAttackCardId] = new(EnemyAttackCardId, "Enemy Attack", 1, [new DamageCardEffect(5, CardTarget.Opponent)], RulesText: "Deal 5 damage."),
            [EnemyFortifyCardId] = new(EnemyFortifyCardId, "Enemy Fortify", 1, [new GainArmorCardEffect(4, CardTarget.Self)], RulesText: "Gain 4 armor."),
            [EnemyHeavyAttackCardId] = new(EnemyHeavyAttackCardId, "Enemy Heavy Attack", 2, [new DamageCardEffect(9, CardTarget.Opponent)], RulesText: "Deal 9 damage."),
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
            EnemyAttackCardId,
            EnemyAttackCardId,
            EnemyHeavyAttackCardId,
            EnemyHeavyAttackCardId,
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
