using Game.Cli;
using Game.Core.Game;

namespace Game.Tests.Cli;

public sealed class CliCommandParserSmokeTests
{
    [Theory]
    [InlineData("start 123", typeof(StartRunAction))]
    [InlineData("start", typeof(StartRunAction))]
    [InlineData("select deck-blades", typeof(SelectDeckAction))]
    [InlineData("discard 0", typeof(DiscardOverflowAction))]
    [InlineData("move n1", typeof(MoveToNodeAction))]
    [InlineData("play 0", typeof(PlayCardAction))]
    [InlineData("end", typeof(EndTurnAction))]
    [InlineData("skip", typeof(SkipRewardAction))]
    [InlineData("rest", typeof(UseRestAction))]
    public void ParsesCoreActionCommands(string input, Type expectedActionType)
    {
        var ok = CliCommandParser.TryParse(input, out var parsed, out var error);

        Assert.True(ok, error);
        Assert.NotNull(parsed.Action);
        Assert.IsType(expectedActionType, parsed.Action);
    }

    [Fact]
    public void ParsesViewCommandForMap()
    {
        var ok = CliCommandParser.TryParse("decks", out var parsed, out var error);

        Assert.True(ok, error);
        Assert.Equal(CliView.Decks, parsed.View);
    }

    [Fact]
    public void ParsesZoneViewCommand()
    {
        var ok = CliCommandParser.TryParse("zone", out var parsed, out var error);

        Assert.True(ok, error);
        Assert.Equal(CliView.Zone, parsed.View);
    }
}
