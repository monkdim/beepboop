using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Model;
using Xunit;
using A = OneTwoPunch.Core.Jobs.Reaper.ReaperActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// Soulsow loads Harvest Moon and Harvest Moon is what a Reaper presses when the boss is not
/// in reach. Neither was ever suggested - reported as "it doesn't use harvest moon at all".
/// <para>
/// The out-of-range Harpe rule had been written, and it was dead: it sat underneath
/// <c>p.Gcd(A.Slice)</c>, which has no condition and therefore always wins. Anything below an
/// unconditional rule is unreachable, so these tests exist as much to pin the ordering as the
/// abilities.
/// </para>
/// </summary>
public sealed class ReaperRangedTests
{
    private static RotationSession Session() =>
        new(JobRegistry.Create(39)!, new RotationSettings
        {
            UseOpener = false,
            SuggestionHoldSeconds = 0f,
        });

    /// <summary>
    /// Death's Design is kept up deliberately. Without it the refresh rule sits above every
    /// rule under test and wins them all, which says nothing about the ordering below it.
    /// </summary>
    private static SnapshotBuilder OnTheBoss() =>
        new SnapshotBuilder().Job(39).Gcd(0f).NoCombo().Enemies(1)
            .Debuff(A.DeathsDesign.Id, 25f);

    private static uint Suggest(SnapshotBuilder builder) =>
        Session().Resolve(RotationMode.SingleTarget, builder.Build(), new FakeActionState()).Action.Id;

    /// <summary>The bug, stated directly.</summary>
    [Fact]
    public void OutOfRangeWithSoulsowLoadedTheAnswerIsHarvestMoon()
    {
        Assert.Equal(
            A.HarvestMoon.Id,
            Suggest(OnTheBoss().OutOfRange().Buff(A.SoulsowBuff.Id)));
    }

    /// <summary>
    /// A blip is not a reason to leave melee. The range answer comes from the game's own
    /// check, which says no for line of sight and for not facing the target as well as for
    /// distance - so turning for a positional reads as out of range for a frame or two. The
    /// out-of-range rule sits above the whole filler combo, so one frame of it outranks the
    /// entire rotation beneath. Reported as the button swapping to Harpe while stood in melee.
    /// </summary>
    [Fact]
    public void AMomentaryLossOfReachDoesNotAbandonMelee()
    {
        var suggestion = Suggest(OnTheBoss().OutOfRange(forSeconds: 0.2f).Buff(A.SoulsowBuff.Id));

        Assert.NotEqual(A.Harpe.Id, suggestion);
        Assert.NotEqual(A.HarvestMoon.Id, suggestion);
    }

    /// <summary>But staying out of reach still switches, and inside one global.</summary>
    [Fact]
    public void StayingOutOfReachStillSwitches()
    {
        var suggestion = Suggest(OnTheBoss().OutOfRange(forSeconds: 1.5f).Buff(A.SoulsowBuff.Id));

        Assert.Equal(A.HarvestMoon.Id, suggestion);
    }

    /// <summary>Without it loaded there is still a ranged global, and it is not Slice.</summary>
    [Fact]
    public void OutOfRangeWithoutSoulsowTheAnswerIsHarpe()
    {
        Assert.Equal(A.Harpe.Id, Suggest(OnTheBoss().OutOfRange()));
    }

    /// <summary>
    /// In melee it stays on the player's own key. Harvest Moon is a whole GCD and it breaks
    /// the Slice combo, so spending one on the boss's face costs more than it gives.
    /// </summary>
    [Fact]
    public void InMeleeHarvestMoonIsNeverSuggested()
    {
        Assert.NotEqual(
            A.HarvestMoon.Id,
            Suggest(OnTheBoss().Buff(A.SoulsowBuff.Id)));
    }

    /// <summary>Moving is not a reason either - every Reaper global is instant.</summary>
    [Fact]
    public void MovingInMeleeIsNotAReasonForHarvestMoon()
    {
        Assert.NotEqual(
            A.HarvestMoon.Id,
            Suggest(OnTheBoss().Moving().Buff(A.SoulsowBuff.Id)));
    }

    /// <summary>Downtime is: there is nothing to hit and it is free damage when they return.</summary>
    [Fact]
    public void DowntimeSpendsHarvestMoon()
    {
        Assert.Equal(
            A.HarvestMoon.Id,
            Suggest(OnTheBoss().Downtime().Buff(A.SoulsowBuff.Id)));
    }

    /// <summary>And it is loaded before the pull, not during one.</summary>
    [Fact]
    public void SoulsowIsSuggestedOutOfCombat()
    {
        Assert.Equal(A.Soulsow.Id, Suggest(OnTheBoss().OutOfCombat()));
    }

    [Fact]
    public void SoulsowIsNotSuggestedOnceItIsLoaded()
    {
        Assert.NotEqual(
            A.Soulsow.Id,
            Suggest(OnTheBoss().OutOfCombat().Buff(A.SoulsowBuff.Id)));
    }

    /// <summary>Never mid-fight: five seconds of casting is five seconds of nothing.</summary>
    [Fact]
    public void SoulsowIsNeverSuggestedInCombat()
    {
        Assert.NotEqual(A.Soulsow.Id, Suggest(OnTheBoss()));
    }
}
