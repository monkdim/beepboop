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
    [Fact]
    public void OpenerPotionIsPromptedBeforeThePull()
    {
        var opener = JobRotationBase.Create<GunbreakerRotation>().Opener!;
        Assert.Equal(0, opener.PotionBeforeStep);
    }

    [Theory]
    [InlineData(2.48f)]
    [InlineData(2.50f)]
    public void RecordedManualOpenerStartsBurstInFirstGapAndFinishesNoMercy(float gcd)
    {
        const float weaveDelay = 0.65f;
        const float effectDelay = 0.65f;
        var session = new RotationSession(JobRotationBase.Create<GunbreakerRotation>(),
            new RotationSettings { WeaveStyle = WeaveStyle.Single });
        var actions = new FakeActionState();
        var noMercyStarts = 2 * weaveDelay + effectDelay;
        var ammo = 0;

        void Press(ActionRef expected, int slot, StatusRef? proc = null, byte chain = 0, int weave = 0)
        {
            var delay = weave * weaveDelay;
            var time = slot * gcd + delay;
            var snapshot = new SnapshotBuilder().Job(37).At(time)
                .Gcd(weave > 0 ? gcd - delay : 0f, gcd)
                .Gauge(s =>
                {
                    s.InCombat = time > 0f;
                    s.CombatDuration = time;
                    s.Gauges.Gunbreaker.Ammo = (byte)ammo;
                    s.Gauges.Gunbreaker.AmmoComboStep = chain;
                });
            var noMercyLeft = noMercyStarts + 20f - time;
            if (time >= noMercyStarts && noMercyLeft > 0f)
                snapshot.Buff(G.NoMercyBuff.Id, noMercyLeft);
            if (proc is { } ready)
                snapshot.Buff(ready.Id);

            var suggestion = session.Resolve(RotationMode.SingleTarget, snapshot.Build(), actions);
            Assert.Equal(expected.Id, suggestion.Action.Id);
            Assert.StartsWith("opener step ", suggestion.Note);
            if (slot is >= 1 and <= 8)
                Assert.InRange(time + effectDelay, noMercyStarts, noMercyStarts + 20f);
            session.NotifyActionUsed(expected.Id);
        }

        Press(G.LightningShot, 0);
        Press(G.Bloodfest, 0, weave: 1);
        ammo += 3;
        Press(G.NoMercy, 0, weave: 2);
        Press(G.GnashingFang, 1);
        ammo -= 1;
        Press(G.JugularRip, 1, G.ReadyToRip, chain: 1, weave: 1);
        Press(G.BowShock, 1, chain: 1, weave: 2);
        Press(G.DoubleDown, 2, chain: 1);
        ammo -= 2;
        Press(G.BlastingZone, 2, chain: 1, weave: 1);
        Press(G.SonicBreak, 3, G.ReadyToBreak, chain: 1);
        Press(G.SavageClaw, 4, chain: 1);
        Press(G.AbdomenTear, 4, G.ReadyToTear, chain: 2, weave: 1);
        Press(G.WickedTalon, 5, chain: 2);
        Press(G.EyeGouge, 5, G.ReadyToGouge, weave: 1);
        Press(G.ReignOfBeasts, 6, G.ReadyToReign);
        Press(G.NobleBlood, 7, chain: 3);
        Press(G.LionHeart, 8, chain: 4);
        Press(G.KeenEdge, 9);
        Press(G.BrutalShell, 10);
        Press(G.SolidBarrel, 11);
        Assert.False(session.OpenerActive);
        Assert.Null(session.OpenerOutcome);
    }

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

    [Theory]
    [InlineData(RotationMode.SingleTarget, 2, true)]
    [InlineData(RotationMode.Aoe, 3, true)]
    [InlineData(RotationMode.SingleTarget, 4, false)]
    public void BloodfestRefillsDuringNoMercyWhenAllCartridgesFit(
        RotationMode mode, byte ammo, bool shouldRefill)
    {
        var actions = new FakeActionState().OnCooldown(G.NoMercy.Id, 56f);
        var suggestion = Session().Resolve(mode, InBurst(ammo).Gcd(1.6f).Build(), actions);
        Assert.Equal(shouldRefill, suggestion.Action.Id == G.Bloodfest.Id);
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(5, false)]
    [InlineData(6, true)]
    public void BloodfestCapacityPreventsPrematureOverflowSpends(byte ammo, bool atCapacity)
    {
        var snapshot = new SnapshotBuilder().Job(37).Level(100).Gcd(0f)
            .Combo(G.BrutalShell.Id).Buff(G.BloodfestBuff.Id, 12f)
            .Gauge(s => s.Gauges.Gunbreaker.Ammo = ammo).Build();
        var actions = new FakeActionState().OnCooldown(G.NoMercy.Id, 40f)
            .OnCooldown(G.GnashingFang.Id, 20f);
        var suggestion = Session().Resolve(RotationMode.SingleTarget, snapshot, actions);
        Assert.Equal(atCapacity ? G.BurstStrike.Id : G.SolidBarrel.Id, suggestion.Action.Id);
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
