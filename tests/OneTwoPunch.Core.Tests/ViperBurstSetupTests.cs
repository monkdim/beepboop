using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Viper;
using OneTwoPunch.Core.Model;
using Xunit;
using A = OneTwoPunch.Core.Jobs.Viper.ViperActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// The ten seconds before Serpent's Ire, when to spend a coil, and which off-global gets the
/// slot - the parts of the Viper review that were about setting the burst up rather than
/// the burst itself.
/// </summary>
public sealed class ViperBurstSetupTests
{
    private static RotationSession Session() =>
        new(JobRotationBase.Create<ViperRotation>(),
            new RotationSettings { UseOpener = false, SuggestionHoldSeconds = 0f });

    private static SnapshotBuilder Standing(byte coils = 1) =>
        new SnapshotBuilder()
            .Job(41).Level(100).Gcd(0f).NoCombo().Enemies(1)
            .Gauge(s => s.Gauges.Viper.RattlingCoils = coils);

    // ---- Vicewinder before the burst -------------------------------------

    /// <summary>
    /// "Around 10s left on Ire's cooldown, you should start to use only dual wield combos."
    /// </summary>
    [Fact]
    public void VicewinderIsHeldInsideTheLastTenSecondsBeforeSerpentsIre()
    {
        var actions = new FakeActionState().OnCooldown(A.SerpentsIre.Id, 8f);

        var suggestion = Session().Resolve(RotationMode.SingleTarget, Standing().Build(), actions);

        Assert.NotEqual(A.Vicewinder.Id, suggestion.Action.Id);
    }

    /// <summary>And not a moment earlier than it has to be.</summary>
    [Fact]
    public void VicewinderIsNotHeldWithSerpentsIreHalfAMinuteOut()
    {
        var actions = new FakeActionState().OnCooldown(A.SerpentsIre.Id, 30f);

        var suggestion = Session().Resolve(RotationMode.SingleTarget, Standing().Build(), actions);

        Assert.Equal(A.Vicewinder.Id, suggestion.Action.Id);
    }

    // ---- The reserve coil -----------------------------------------------

    /// <summary>
    /// "Spend them before using Serpent's Ire as it will grant another." Vicewinder is held
    /// here too, so the reserve is what comes back.
    /// </summary>
    [Fact]
    public void TheReserveCoilIsSpentJustBeforeSerpentsIre()
    {
        var actions = new FakeActionState().OnCooldown(A.SerpentsIre.Id, 8f);

        var suggestion = Session().Resolve(RotationMode.SingleTarget, Standing(coils: 1).Build(), actions);

        Assert.Equal(A.UncoiledFury.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// Moving in melee range is not a disengage. Every Viper global is instant, and a recorded
    /// raid spent the reserve on "you are moving" 32 times, then had nothing for the two real
    /// disconnects and threw Writhing Snap.
    /// </summary>
    [Fact]
    public void MovingInRangeDoesNotSpendTheReserve()
    {
        var actions = new FakeActionState()
            .OnCooldown(A.Vicewinder.Id, 30f)
            .OnCooldown(A.SerpentsIre.Id, 60f);

        var suggestion = Session().Resolve(
            RotationMode.SingleTarget, Standing(coils: 1).Moving().Build(), actions);

        Assert.NotEqual(A.UncoiledFury.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// Moving and out of reach this instant is the start of a real disengage, before the
    /// range reading has dwelled long enough to say so on its own.
    /// </summary>
    [Fact]
    public void MovingAndOutOfReachSpendsTheReserveAtOnce()
    {
        var actions = new FakeActionState()
            .OnCooldown(A.Vicewinder.Id, 30f)
            .OnCooldown(A.SerpentsIre.Id, 60f);

        var snapshot = Standing(coils: 1)
            .Moving()
            .Gauge(s =>
            {
                s.TargetInRange = false;
                s.OutOfRangeFor = 0.2f;
            })
            .Build();

        var suggestion = Session().Resolve(RotationMode.SingleTarget, snapshot, actions);

        Assert.Equal(A.UncoiledFury.Id, suggestion.Action.Id);
    }

    // ---- Who gets the weave slot ------------------------------------------

    /// <summary>A follow-up is woven before Serpent's Ire, not after it.</summary>
    private static SnapshotBuilder InACoilWindow() =>
        new SnapshotBuilder()
            .Job(41).Level(100).Gcd(2.0f).Enemies(1)
            .Buff(A.HuntersVenom.Id, 30f);

    /// <summary>
    /// A recorded raid lost four bites to Serpent's Ire taking the slot. Ire a global later
    /// costs nothing the guide does not already allow for; a bite not woven is gone.
    /// </summary>
    [Fact]
    public void ABiteIsWovenBeforeSerpentsIre()
    {
        var suggestion = Session().Resolve(
            RotationMode.SingleTarget, InACoilWindow().Build(), new FakeActionState());

        Assert.Equal(A.TwinfangBite.Id, suggestion.Action.Id);
    }

    /// <summary>And before Bloodbath, which is the weaker heal and can wait one global.</summary>
    [Fact]
    public void ABiteIsWovenBeforeBloodbath()
    {
        var actions = new FakeActionState()
            .OnCooldown(A.SerpentsIre.Id, 60f)
            .OnCooldown(A.SecondWind.Id, 60f);

        var suggestion = Session().Resolve(
            RotationMode.SingleTarget, InACoilWindow().Hp(0.4f).Build(), actions);

        Assert.Equal(A.TwinfangBite.Id, suggestion.Action.Id);
    }

    /// <summary>Second Wind still comes first: that trade is made on purpose.</summary>
    [Fact]
    public void SecondWindStillComesBeforeABite()
    {
        var actions = new FakeActionState().OnCooldown(A.SerpentsIre.Id, 60f);

        var suggestion = Session().Resolve(
            RotationMode.SingleTarget, InACoilWindow().Hp(0.4f).Build(), actions);

        Assert.Equal(A.SecondWind.Id, suggestion.Action.Id);
    }
}
