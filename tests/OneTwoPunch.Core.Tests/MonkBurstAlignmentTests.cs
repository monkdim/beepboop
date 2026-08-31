using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Monk;
using OneTwoPunch.Core.Model;
using Xunit;
using A = OneTwoPunch.Core.Jobs.Monk.MonkActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// When a Perfect Balance window may be opened, which the list never asked either.
/// <para>
/// It was pressed the moment it came off cooldown, with no idea a damage window was coming.
/// A recorded pull shows what that costs: Phantom Rush - the hardest hitting button the job
/// has - at 00:51.0 with no Riddle of Fire and no Brotherhood on it at all, and Elixir Burst
/// at 01:29.6, three seconds after Riddle of Fire fell off. The windows behind them opened
/// at 00:44.2 with Riddle of Fire twenty-two seconds away, and at 01:22.9 with four seconds
/// of it left - and a window takes about seven seconds to pay out.
/// </para>
/// </summary>
public sealed class MonkBurstAlignmentTests
{
    private static uint Suggest(byte level, FakeActionState actions, float riddleOfFireLeft = 0f)
    {
        var builder = new SnapshotBuilder()
            .Job(20).Level(level).Gcd(1.3f).NoCombo().Enemies(1);

        if (riddleOfFireLeft > 0f)
            builder.Buff(A.RiddleOfFireBuff.Id, riddleOfFireLeft);

        return new RotationSession(JobRotationBase.Create<MonkRotation>(),
            new RotationSettings { UseOpener = false, SuggestionHoldSeconds = 0f })
            .Resolve(RotationMode.SingleTarget, builder.Build(), actions).Action.Id;
    }

    /// <summary>
    /// Everything else that could take the weave slot is turning, so each test is only ever
    /// about Perfect Balance. One charge banked and one still building: holding is free.
    /// </summary>
    private static FakeActionState OnlyPerfectBalanceIsUp(float riddleOfFireCooldown) =>
        new FakeActionState()
            .WithCharges(A.PerfectBalance.Id, 1, 2)
            .OnCooldown(A.SecondWind.Id, 120f)
            .OnCooldown(A.Brotherhood.Id, 90f)
            .OnCooldown(A.RiddleOfWind.Id, 45f)
            .OnCooldown(A.RiddleOfFire.Id, riddleOfFireCooldown);

    /// <summary>The first half of the defect: the window opened with the buff far away.</summary>
    [Fact]
    public void TheWindowIsHeldWhileRiddleOfFireIsAWayOff()
    {
        Assert.NotEqual(
            A.PerfectBalance.Id,
            Suggest(100, OnlyPerfectBalanceIsUp(riddleOfFireCooldown: 22f)));
    }

    /// <summary>
    /// The second half, and the subtler one: Riddle of Fire was running, but not for long
    /// enough. The Blitz is four globals out and lands after the buff has gone.
    /// </summary>
    [Fact]
    public void TheWindowIsHeldWhenRiddleOfFireWouldExpireBeforeTheBlitz()
    {
        Assert.NotEqual(
            A.PerfectBalance.Id,
            Suggest(100, OnlyPerfectBalanceIsUp(riddleOfFireCooldown: 56f), riddleOfFireLeft: 4f));
    }

    /// <summary>With enough of the buff left to cover the Blitz, it opens.</summary>
    [Fact]
    public void TheWindowOpensInsideRiddleOfFire()
    {
        Assert.Equal(
            A.PerfectBalance.Id,
            Suggest(100, OnlyPerfectBalanceIsUp(riddleOfFireCooldown: 45f), riddleOfFireLeft: 15f));
    }

    /// <summary>
    /// And just before it, because the list weaves Riddle of Fire the instant it comes up
    /// and the Blitz is still four globals behind that.
    /// </summary>
    [Fact]
    public void TheWindowOpensWithRiddleOfFireAboutToComeUp()
    {
        Assert.Equal(
            A.PerfectBalance.Id,
            Suggest(100, OnlyPerfectBalanceIsUp(riddleOfFireCooldown: 3f)));
    }

    /// <summary>
    /// The escape hatch, and the reason the hold cannot deadlock. A charge sitting at the cap
    /// is a Blitz that will never happen, which is worse than a Blitz with no buff on it.
    /// </summary>
    [Fact]
    public void AChargeAboutToBeLostIsSpentRegardless()
    {
        var actions = new FakeActionState()
            .WithCharges(A.PerfectBalance.Id, 2, 2)
            .OnCooldown(A.SecondWind.Id, 120f)
            .OnCooldown(A.Brotherhood.Id, 90f)
            .OnCooldown(A.RiddleOfWind.Id, 45f)
            .OnCooldown(A.RiddleOfFire.Id, 22f);

        Assert.Equal(A.PerfectBalance.Id, Suggest(100, actions));
    }

    /// <summary>
    /// Below the level that has Riddle of Fire there is no window to align to, and a rung
    /// that cannot be climbed must not hang the phase - so the window opens as it always did.
    /// </summary>
    [Fact]
    public void BelowRiddleOfFireTheWindowOpensAsItAlwaysDid()
    {
        var actions = new FakeActionState()
            .WithCharges(A.PerfectBalance.Id, 1, 2)
            .OnCooldown(A.SecondWind.Id, 120f);

        Assert.Equal(A.PerfectBalance.Id, Suggest(60, actions));
    }
}
