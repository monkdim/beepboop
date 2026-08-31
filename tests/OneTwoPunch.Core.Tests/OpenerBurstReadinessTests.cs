using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Monk;
using OneTwoPunch.Core.Model;
using Xunit;
using A = OneTwoPunch.Core.Jobs.Monk.MonkActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// Whether an opener should start at all when the cooldowns it is written around are down.
/// <para>
/// A recorded alliance raid is the case. Combat ends ten times across nineteen minutes -
/// every trash pack, every boss boundary - and the opener re-arms each time. Twice it got
/// going with Brotherhood and Riddle of Fire both still turning, stepped over exactly those
/// two steps because they were not ready, and drove Perfect Balance straight into a Blitz
/// with nothing on it: a Phantom Rush at 07:34.0 and a Rising Phoenix at 14:41.8, the only
/// two naked Blitzes in twenty-two. The second spent the charge eighteen seconds after the
/// priority list had correctly saved the first for a damage window.
/// </para>
/// </summary>
public sealed class OpenerBurstReadinessTests
{
    private static RotationSession Session() =>
        new(JobRotationBase.Create<MonkRotation>(),
            new RotationSettings { SuggestionHoldSeconds = 0f });

    private static CombatSnapshot AtPull(bool inCombat = true) =>
        new SnapshotBuilder()
            .Job(20).Level(100).Gcd(0f).NoCombo().Enemies(1)
            .Gauge(s =>
            {
                s.InCombat = inCombat;
                s.CombatDuration = inCombat ? 0.5f : 0f;
            })
            .Build();

    /// <summary>The defect: the chart drives a burst it has no buffs for.</summary>
    [Fact]
    public void TheOpenerDoesNotStartWithItsBurstCooldownStillTurning()
    {
        var session = Session();

        // Brotherhood pressed on the trash pack a moment ago.
        var actions = new FakeActionState().OnCooldown(A.Brotherhood.Id, 80f);

        var suggestion = session.Resolve(RotationMode.SingleTarget, AtPull(), actions);

        Assert.False(session.OpenerActive, "the opener ran without the burst it is built around");
        Assert.NotEqual(A.PerfectBalance.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// Out of combat it waits instead of giving up. Standing in front of a boss waiting for a
    /// two minute cooldown is exactly when the opener is worth having.
    /// </summary>
    [Fact]
    public void BeforeThePullItWaitsForTheCooldownRatherThanGivingUp()
    {
        var session = Session();
        var actions = new FakeActionState().OnCooldown(A.Brotherhood.Id, 20f);

        session.Resolve(RotationMode.SingleTarget, AtPull(inCombat: false), actions);

        Assert.True(session.OpenerActive, "the opener gave up while there was still time to wait");
        Assert.Null(session.OpenerOutcome);
    }

    /// <summary>And picks up the moment the cooldown lands.</summary>
    [Fact]
    public void ItStartsOnceTheBurstIsBack()
    {
        var session = Session();
        var actions = new FakeActionState().OnCooldown(A.Brotherhood.Id, 20f);

        session.Resolve(RotationMode.SingleTarget, AtPull(inCombat: false), actions);

        var ready = new FakeActionState();
        var suggestion = session.Resolve(RotationMode.SingleTarget, AtPull(inCombat: false), ready);

        Assert.True(session.OpenerActive);
        Assert.Equal(A.DragonKick.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// A cooldown inside the next global is not "down" - the opener steps over weaves that
    /// are momentarily unavailable and the priority list picks them up a beat later.
    /// </summary>
    [Fact]
    public void ACooldownInsideTheNextGlobalIsNotAReasonToStandDown()
    {
        var session = Session();
        var actions = new FakeActionState().OnCooldown(A.Brotherhood.Id, 1f);

        var suggestion = session.Resolve(RotationMode.SingleTarget, AtPull(), actions);

        Assert.True(session.OpenerActive);
        Assert.Equal(A.DragonKick.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// And a burst the player has not learned is nothing to wait for. A rung that cannot be
    /// climbed must not hang the phase - the opener runs as it always did.
    /// </summary>
    [Fact]
    public void AnUnlearnedBurstIsNotWaitedFor()
    {
        var session = new RotationSession(
            JobRotationBase.Create<MonkRotation>(),
            new RotationSettings { SuggestionHoldSeconds = 0f });

        // Not learned, so it is never coming off cooldown and never coming back.
        var actions = new FakeActionState().Locked(A.Brotherhood.Id);

        var snapshot = new SnapshotBuilder()
            .Job(20).Level(100).Gcd(0f).NoCombo().Enemies(1)
            .Gauge(s => s.CombatDuration = 0.5f)
            .Build();

        session.Resolve(RotationMode.SingleTarget, snapshot, actions);

        Assert.True(session.OpenerActive, "an unlearned burst hung the opener for good");
    }
}
