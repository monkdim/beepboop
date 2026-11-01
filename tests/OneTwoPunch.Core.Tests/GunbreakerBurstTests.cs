using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Gunbreaker;
using OneTwoPunch.Core.Model;
using Xunit;
using G = OneTwoPunch.Core.Jobs.Gunbreaker.GunbreakerActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// What goes inside No Mercy, and in what order. A recorded pull spent both Gnashing Fang
/// charges on two full chains - six of the eight globals in the first window - and neither
/// Double Down nor Sonic Break landed inside it.
/// </summary>
public sealed class GunbreakerBurstTests
{
    private static RotationSession Session() =>
        new(JobRotationBase.Create<GunbreakerRotation>(),
            new RotationSettings { WeaveStyle = WeaveStyle.Double, UseOpener = false, SuggestionHoldSeconds = 0f });

    private static SnapshotBuilder InBurst(byte ammo, byte chainStep = 0) =>
        new SnapshotBuilder()
            .Job(37).Level(100).Gcd(0f).Enemies(1)
            .Buff(G.NoMercyBuff.Id, 15f)
            .Gauge(s =>
            {
                s.Gauges.Gunbreaker.Ammo = ammo;
                s.Gauges.Gunbreaker.AmmoComboStep = chainStep;
            });

    /// <summary>
    /// Double Down is a thousand potency, the biggest global in the kit, and it outranks
    /// starting a fresh Gnashing Fang chain inside the window.
    /// </summary>
    [Fact]
    public void DoubleDownBeatsAFreshGnashingFangInTheWindow()
    {
        var session = Session();
        var suggestion = session.Resolve(
            RotationMode.SingleTarget, InBurst(ammo: 3).Build(), new FakeActionState());

        Assert.Equal(G.DoubleDown.Id, suggestion.Action.Id);
    }

    /// <summary>And so does Sonic Break, once Double Down has gone.</summary>
    [Fact]
    public void SonicBreakBeatsAFreshGnashingFangInTheWindow()
    {
        var session = Session();
        var actions = new FakeActionState().OnCooldown(G.DoubleDown.Id, 50f);

        var suggestion = session.Resolve(
            RotationMode.SingleTarget, InBurst(ammo: 3).Build(), actions);

        Assert.Equal(G.SonicBreak.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// Reign of Beasts leads: it opens a three global chain that has to finish inside the
    /// window, where the other two are single globals that cannot break a combo.
    /// </summary>
    [Fact]
    public void ReignOfBeastsLeadsTheWindowWhenItIsArmed()
    {
        var session = Session();
        var snapshot = InBurst(ammo: 3).Buff(G.ReadyToReign.Id, 25f).Build();

        var suggestion = session.Resolve(RotationMode.SingleTarget, snapshot, new FakeActionState());

        Assert.Equal(G.ReignOfBeasts.Id, suggestion.Action.Id);
    }

    /// <summary>A chain already running still finishes before anything new starts.</summary>
    [Fact]
    public void AnOpenChainIsFinishedBeforeDoubleDown()
    {
        var session = Session();
        var suggestion = session.Resolve(
            RotationMode.SingleTarget, InBurst(ammo: 3, chainStep: 1).Build(), new FakeActionState());

        Assert.Equal(G.SavageClaw.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// Out of the window Gnashing Fang waits for the window, a capped charge, or No Mercy
    /// within seven seconds. The Balance: "Keep Gnashing Fang below 2 charges."
    /// </summary>
    [Fact]
    public void GnashingFangIsHeldInFillerWithNoMercyFarOff()
    {
        var session = Session();
        var snapshot = new SnapshotBuilder()
            .Job(37).Level(100).Gcd(0f).Enemies(1)
            .Gauge(s => s.Gauges.Gunbreaker.Ammo = 3)
            .Build();
        var actions = new FakeActionState()
            .OnCooldown(G.NoMercy.Id, 40f)
            .WithCharges(G.GnashingFang.Id, 1, 2);

        var suggestion = session.Resolve(RotationMode.SingleTarget, snapshot, actions);

        Assert.NotEqual(G.GnashingFang.Id, suggestion.Action.Id);
    }

    /// <summary>But a charge about to cap is a Gnashing Fang that never happened.</summary>
    [Fact]
    public void GnashingFangIsSpentRatherThanCappingACharge()
    {
        var session = Session();
        var snapshot = new SnapshotBuilder()
            .Job(37).Level(100).Gcd(0f).Enemies(1)
            .Gauge(s => s.Gauges.Gunbreaker.Ammo = 3)
            .Build();
        var actions = new FakeActionState()
            .OnCooldown(G.NoMercy.Id, 40f)
            .WithCharges(G.GnashingFang.Id, 2, 2);

        var suggestion = session.Resolve(RotationMode.SingleTarget, snapshot, actions);

        Assert.Equal(G.GnashingFang.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// Trajectory has no potency line at all - it is the gap closer, and it drags the tank
    /// onto the boss and adds enmity. It must never be offered as damage.
    /// </summary>
    [Fact]
    public void TrajectoryIsNeverSuggested()
    {
        var session = Session();
        var snapshot = InBurst(ammo: 3).Gcd(1.2f).Build();

        for (var i = 0; i < 12; i++)
        {
            var suggestion = session.Resolve(RotationMode.SingleTarget, snapshot, new FakeActionState());
            Assert.NotEqual(G.Trajectory.Id, suggestion.Action.Id);
            session.NotifyActionUsed(suggestion.Action.Id);
        }
    }
}
