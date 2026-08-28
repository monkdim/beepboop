using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Model;
using Xunit;
using A = OneTwoPunch.Core.Jobs.Samurai.SamuraiActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// Hakaze becomes Gyofu at level 92 - a rename rather than a new button, the same way Fuga
/// becomes Fuko. Every rung in the single-target list named Hakaze, including the
/// unconditional one at the bottom, so at level 92 the whole list had nothing that could
/// answer once a combo finished: reported as the button working for a few globals and then
/// saying "nothing to suggest".
/// </summary>
public sealed class SamuraiComboTests
{
    private static RotationSession Session() =>
        new(JobRegistry.Create(34)!, new RotationSettings
        {
            UseOpener = false,
            SuggestionHoldSeconds = 0f,
        });

    private static SnapshotBuilder Standing(byte level) =>
        new SnapshotBuilder().Job(34).Level(level).Gcd(0f).Enemies(1).NoCombo();

    /// <summary>
    /// Kaeshi Namikiri is gated by the game rather than by a status, so in a test where
    /// everything is usable it wins every global. The rest of the list is what these are
    /// about.
    /// </summary>
    private static uint Suggest(SnapshotBuilder builder) =>
        Session().Resolve(
            RotationMode.SingleTarget,
            builder.Build(),
            new FakeActionState().Unusable(A.KaeshiNamikiri.Id)).Action.Id;

    /// <summary>The reported symptom: nothing left in the list that could answer.</summary>
    [Fact]
    public void TheStarterIsGyofuOnceItIsLearned()
    {
        Assert.Equal(A.Gyofu.Id, Suggest(Standing(100)));
    }

    /// <summary>And still Hakaze before that.</summary>
    [Fact]
    public void TheStarterIsStillHakazeBeforeNinetyTwo()
    {
        Assert.Equal(A.Hakaze.Id, Suggest(Standing(80)));
    }

    /// <summary>
    /// The combo has to continue from whichever form was cast. The game reports the action
    /// that actually went off, so a Samurai at full level is sitting on a Gyofu combo and
    /// every branch was asking about Hakaze.
    /// </summary>
    [Fact]
    public void TheComboContinuesFromGyofu()
    {
        var suggestion = Suggest(Standing(100).Combo(A.Gyofu.Id));

        Assert.Contains(suggestion, new[] { A.Jinpu.Id, A.Shifu.Id, A.Yukikaze.Id });
    }

    /// <summary>And from Hakaze, which is what the game reports below level 92.</summary>
    [Fact]
    public void TheComboContinuesFromHakaze()
    {
        var suggestion = Suggest(Standing(100).Combo(A.Hakaze.Id));

        Assert.Contains(suggestion, new[] { A.Jinpu.Id, A.Shifu.Id, A.Yukikaze.Id });
    }

    /// <summary>The finishers were never broken - they key on Jinpu and Shifu, which do not upgrade.</summary>
    [Fact]
    public void TheFinisherStillFollowsJinpu()
    {
        Assert.Equal(A.Gekko.Id, Suggest(Standing(100).Combo(A.Jinpu.Id)));
    }
}
