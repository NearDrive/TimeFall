using Game.Core.Map;

namespace Game.Tests.Data;

[Trait("Lane", "unit")]
public class Zone1EnemyContentTests
{
    [Fact]
    public void Zone1EnemyCards_Json_LoadsSuccessfully()
    {
        var content = StaticGameContentProvider.LoadDefault();

        Assert.Contains(content.CardDefinitions.Keys, id => id.Value == "enemy-raider-slash");
        Assert.Contains(content.CardDefinitions.Keys, id => id.Value == "enemy-judgment");
    }

    [Fact]
    public void Zone1Enemies_Json_LoadsSuccessfully()
    {
        var content = StaticGameContentProvider.LoadDefault();

        Assert.True(content.EnemyDefinitions.Count >= 6);
        Assert.Contains("zone1-raider", content.EnemyDefinitions.Keys);
        Assert.Contains("zone1-warden-of-the-gate", content.EnemyDefinitions.Keys);
    }

    [Fact]
    public void Zone1SpawnTable_Json_LoadsSuccessfully()
    {
        var content = StaticGameContentProvider.LoadDefault();

        Assert.NotNull(content.Zone1SpawnTable);
        Assert.Equal(1, content.Zone1SpawnTable!.Zone);
    }

    [Fact]
    public void EnemyDeckReferences_ValidateAgainstEnemyCardRegistry()
    {
        var content = StaticGameContentProvider.LoadDefault();
        var cardIds = content.CardDefinitions.Keys.ToHashSet();

        foreach (var enemy in content.EnemyDefinitions.Values)
        {
            foreach (var cardId in enemy.Deck)
            {
                Assert.Contains(cardId, cardIds);
            }
        }
    }

    [Fact]
    public void SpawnTableReferences_ValidateAgainstEnemyDefinitions()
    {
        var content = StaticGameContentProvider.LoadDefault();
        var enemyIds = content.EnemyDefinitions.Keys.ToHashSet(StringComparer.Ordinal);
        var table = content.Zone1SpawnTable!;

        Assert.All(table.NormalEncounterPools.Values.SelectMany(x => x.Values).SelectMany(x => x), entry => Assert.Contains(entry.EnemyId, enemyIds));
        Assert.All(table.EliteEncounterPools.Values.SelectMany(x => x), entry => Assert.Contains(entry.EnemyId, enemyIds));
        Assert.Contains(table.BossEnemyId, enemyIds);
    }

    [Fact]
    public void TierI_NormalPool_ContainsExpectedEnemies()
    {
        var pool = Load().Zone1SpawnTable!.NormalEncounterPools["early"]["TierI"].Select(x => x.EnemyId).ToHashSet();

        Assert.Contains("zone1-raider", pool);
        Assert.Contains("zone1-bastion-guard", pool);
    }

    [Fact]
    public void TierII_NormalPool_ContainsBloodletter()
    {
        var table = Load().Zone1SpawnTable!;
        var midTierIi = table.NormalEncounterPools["mid"]["TierII"].Select(x => x.EnemyId).ToArray();
        var lateTierIi = table.NormalEncounterPools["late"]["TierII"].Select(x => x.EnemyId).ToArray();

        Assert.Contains("zone1-bloodletter", midTierIi);
        Assert.Contains("zone1-bloodletter", lateTierIi);
    }

    [Fact]
    public void EliteTierI_Pool_ContainsExpectedElites()
    {
        var pool = Load().Zone1SpawnTable!.EliteEncounterPools["EliteTierI"].Select(x => x.EnemyId).ToHashSet();

        Assert.Contains("zone1-iron-duelist", pool);
        Assert.Contains("zone1-flesh-reaper", pool);
    }

    [Fact]
    public void BossEncounter_UsesWardenOfTheGate()
    {
        Assert.Equal("zone1-warden-of-the-gate", Load().Zone1SpawnTable!.BossEnemyId);
    }

    [Fact]
    public void OneRepresentativeEnemyFromEachCategory_CanBeInstantiatedForCombat()
    {
        var content = Load();
        var selectedEnemies = new[]
        {
            content.EnemyDefinitions["zone1-raider"],
            content.EnemyDefinitions["zone1-bloodletter"],
            content.EnemyDefinitions["zone1-iron-duelist"],
            content.EnemyDefinitions["zone1-warden-of-the-gate"],
        };

        foreach (var enemy in selectedEnemies)
        {
            var blueprint = EnemyEncounterFactory.CreateBlueprint(enemy);
            Assert.Equal(enemy.Id, blueprint.Enemy.EntityId);
            Assert.Equal(enemy.Hp, blueprint.Enemy.HP);
            Assert.Equal(enemy.StartingArmor, blueprint.Enemy.Armor);
            Assert.NotEmpty(blueprint.Enemy.DrawPile);
        }
    }

    private static GameContentBundle Load() => StaticGameContentProvider.LoadDefault();
}
