using OneTwoPunch.Core.Model;
using Xunit;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// The positional banner is for players who need a beat to reposition, so two things have
/// to hold: it warns about the <em>next</em> global cooldown rather than the current
/// action, and it stops talking once you are standing correctly. A warning that is always
/// on is one nobody reads.
/// </summary>
public sealed class PositionalTests
{
    private static Suggestion With(PositionalHint wanted, RelativePosition standing)
    {
        var action = new ActionRef(1, "Test", ActionKind.Gcd, 1);
        return new Suggestion(action, action, null, wanted) { Position = standing };
    }

    [Theory]
    [InlineData(PositionalHint.Rear, RelativePosition.Front, true)]
    [InlineData(PositionalHint.Rear, RelativePosition.Flank, true)]
    [InlineData(PositionalHint.Rear, RelativePosition.Rear, false)]
    [InlineData(PositionalHint.Flank, RelativePosition.Rear, true)]
    [InlineData(PositionalHint.Flank, RelativePosition.Flank, false)]
    public void MoveIsAskedForOnlyWhenStandingInTheWrongPlace(
        PositionalHint wanted, RelativePosition standing, bool expected)
    {
        Assert.Equal(expected, With(wanted, standing).NeedsToMove);
    }

    [Fact]
    public void NoPositionalNeverAsksForAMove()
    {
        Assert.False(With(PositionalHint.None, RelativePosition.Front).NeedsToMove);
        Assert.False(With(PositionalHint.None, RelativePosition.Unknown).NeedsToMove);
    }

    /// <summary>
    /// Unknown means positional detection is off or the target's facing could not be read.
    /// It must still ask, because silently assuming you are in position is the failure that
    /// costs damage without ever telling you why.
    /// </summary>
    [Fact]
    public void AnUnknownPositionStillAsksYouToMove()
    {
        Assert.True(With(PositionalHint.Rear, RelativePosition.Unknown).NeedsToMove);
    }
}
