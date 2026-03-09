using Game.Cli;
using Game.Core.Game;

namespace Game.Tests.Cli;

public sealed class CliCommandParserSmokeTests
{
    [Theory]
    [InlineData("start 123", typeof(StartRunAction))]
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
        var ok = CliCommandParser.TryParse("map", out var parsed, out var error);

        Assert.True(ok, error);
        Assert.Equal(CliView.Map, parsed.View);
    }
}
