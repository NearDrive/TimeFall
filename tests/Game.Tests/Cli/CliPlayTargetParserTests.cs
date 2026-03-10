using Game.Cli;
using Game.Core.Game;

namespace Game.Tests.Cli;

public sealed class CliPlayTargetParserTests
{
    [Fact]
    public void CliOrParser_PlaySupportsOptionalTargetIn1v1()
    {
        var ok = CliCommandParser.TryParse("play 0", out var parsed, out var error);

        Assert.True(ok, error);
        var action = Assert.IsType<PlayCardAction>(parsed.Action);
        Assert.Equal(0, action.HandIndex);
        Assert.Null(action.TargetIndex);
    }

    [Fact]
    public void CliOrParser_PlaySupportsRequiredTargetInMultiEnemyCombat()
    {
        var ok = CliCommandParser.TryParse("play 0 2", out var parsed, out var error);

        Assert.True(ok, error);
        var action = Assert.IsType<PlayCardAction>(parsed.Action);
        Assert.Equal(0, action.HandIndex);
        Assert.Equal(2, action.TargetIndex);
    }
}
