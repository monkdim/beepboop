using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Dragoon;
using OneTwoPunch.Core.Jobs.Machinist;
using OneTwoPunch.Core.Model;
using Xunit;
using Drg = OneTwoPunch.Core.Jobs.Dragoon.DragoonActions;
using Mch = OneTwoPunch.Core.Jobs.Machinist.MachinistActions;

namespace OneTwoPunch.Core.Tests;

public sealed class RotationBehaviourTests
{
    private static RotationSettings NoOpener() =>
        new() { UseOpener = false, SuggestionHoldSeconds = 0f };

    private static RotationSession DragoonSession(RotationSettings? settings = null) =>
        new(JobRotationBase.Create<DragoonRotation>(), settings ?? NoOpener());

    private static RotationSession MachinistSession(RotationSettings? settings = null) =>
        new(JobRotationBase.Create<MachinistRotation>(), settings ?? NoOpener());

    // ---- Combo chains ----------------------------------------------------

    [Theory]
    [InlineData(0u)]                    // no combo running
    [InlineData(1u)]                    // combo started
    [InlineData(2u)]                    // second step
    public void TheSingleTargetButtonWalksTheComboChain(uint step)
    {
        var session = DragoonSession();
        var actions = new FakeActionState();

        var builder = new SnapshotBuilder().Gcd(0.1f);
        var expected = step switch
        {
            0u => Drg.TrueThrust.Id,
            1u => Drg.LanceBarrage.Id,
            _ => Drg.HeavensThrust.Id,
        };

        builder = step switch
        {
            0u => builder.NoCombo(),
            1u => builder.Combo(Drg.TrueThrust).Buff(Drg.PowerSurge, 25f).Debuff(Drg.ChaoticSpringBuff, 25f),
            _ => builder.Combo(Drg.LanceBarrage),
        };

        var suggestion = session.Resolve(RotationMode.SingleTarget, builder.Build(), actions);

        Assert.Equal(expected, suggestion.Action.Id);
    }

    [Fact]
    public void TheComboForksToTheBuffChainWhenPowerSurgeIsRunningOut()
    {
        var session = DragoonSession();
        var snapshot = new SnapshotBuilder()
            .Gcd(0.1f)
            .Combo(Drg.TrueThrust)
            .Buff(Drg.PowerSurge, 4f)
            .Debuff(Drg.ChaoticSpringBuff, 25f)
            .Build();

        var suggestion = session.Resolve(RotationMode.SingleTarget, snapshot, new FakeActionState());

        Assert.Equal(Drg.SpiralBlow.Id, suggestion.Action.Id);
    }

    [Fact]
    public void TheComboForksToTheDamageChainWhenBuffsAreHealthy()
    {
        var session = DragoonSession();
        var snapshot = new SnapshotBuilder()
            .Gcd(0.1f)
            .Combo(Drg.TrueThrust)
            .Buff(Drg.PowerSurge, 25f)
            .Debuff(Drg.ChaoticSpringBuff, 25f)
            .Build();

        var suggestion = session.Resolve(RotationMode.SingleTarget, snapshot, new FakeActionState());

        Assert.Equal(Drg.LanceBarrage.Id, suggestion.Action.Id);
    }

    // ---- The AoE button --------------------------------------------------

    [Fact]
    public void TheAoeButtonFallsBackToSingleTargetOnALoneEnemy()
    {
        var session = DragoonSession();
        var snapshot = new SnapshotBuilder().Gcd(0.1f).Enemies(1).NoCombo().Build();

        var suggestion = session.Resolve(RotationMode.Aoe, snapshot, new FakeActionState());

        Assert.Equal(Drg.TrueThrust.Id, suggestion.Action.Id);
        Assert.Contains("single target", suggestion.Note);
    }

    [Fact]
    public void TheAoeButtonStaysAoeOnAPack()
    {
        var session = DragoonSession();
        var snapshot = new SnapshotBuilder().Gcd(0.1f).Enemies(4).NoCombo().Build();

        var suggestion = session.Resolve(RotationMode.Aoe, snapshot, new FakeActionState());

        Assert.Equal(Drg.DoomSpike.Id, suggestion.Action.Id);
    }

    [Fact]
    public void TheAoeFallbackCanBeTurnedOff()
    {
        var settings = NoOpener();
        settings.AoeFallsBackToSingleTarget = false;
        var session = DragoonSession(settings);
        var snapshot = new SnapshotBuilder().Gcd(0.1f).Enemies(1).NoCombo().Build();

        var suggestion = session.Resolve(RotationMode.Aoe, snapshot, new FakeActionState());

        Assert.Equal(Drg.DoomSpike.Id, suggestion.Action.Id);
    }

    // ---- Downtime --------------------------------------------------------

    [Fact]
    public void BurstIsHeldWhileTheBossIsUntargetable()
    {
        var session = DragoonSession();
        var snapshot = new SnapshotBuilder().Gcd(2.2f).Combo(Drg.TrueThrust).Downtime().Build();

        var suggestion = session.Resolve(RotationMode.SingleTarget, snapshot, new FakeActionState());

        Assert.NotEqual(Drg.LanceCharge.Id, suggestion.Action.Id);
        Assert.NotEqual(Drg.BattleLitany.Id, suggestion.Action.Id);
    }

    [Fact]
    public void BurstGoesOutTheMomentTheBossIsBackUp()
    {
        var session = DragoonSession();
        var snapshot = new SnapshotBuilder().Gcd(2.2f).Combo(Drg.TrueThrust).Downtime(false).Build();

        var suggestion = session.Resolve(RotationMode.SingleTarget, snapshot, new FakeActionState());

        Assert.Equal(Drg.LanceCharge.Id, suggestion.Action.Id);
    }

    // ---- Look-ahead ------------------------------------------------------

    [Fact]
    public void ReassembleIsOnlySpentInFrontOfATool()
    {
        var session = MachinistSession();

        // Combo filler is next, every tool is on cooldown: Reassemble must not be offered.
        var actions = new FakeActionState()
            .OnCooldown(Mch.Drill.Id, 15f)
            .OnCooldown(Mch.AirAnchor.Id, 30f)
            .OnCooldown(Mch.ChainSaw.Id, 45f)
            .OnCooldown(Mch.Excavator.Id, 45f)
            .OnCooldown(Mch.Wildfire.Id, 60f)
            .OnCooldown(Mch.BarrelStabilizer.Id, 60f)
            .OnCooldown(Mch.Hypercharge.Id, 60f)
            .OnCooldown(Mch.AutomatonQueen.Id, 60f)
            .OnCooldown(Mch.DoubleCheck.Id, 20f)
            .OnCooldown(Mch.Checkmate.Id, 20f);

        var snapshot = new SnapshotBuilder().Gcd(0.9f).NoCombo().Build();

        var suggestion = session.Resolve(RotationMode.SingleTarget, snapshot, actions);

        Assert.NotEqual(Mch.Reassemble.Id, suggestion.Action.Id);
    }

    [Fact]
    public void ReassembleIsSpentWhenAToolIsTheNextGcd()
    {
        var session = MachinistSession();

        var actions = new FakeActionState()
            .OnCooldown(Mch.Wildfire.Id, 60f)
            .OnCooldown(Mch.BarrelStabilizer.Id, 60f)
            .OnCooldown(Mch.Hypercharge.Id, 60f)
            .OnCooldown(Mch.AutomatonQueen.Id, 60f);

        // Drill and friends are up, so the next GCD is a tool and the GCD is imminent.
        var snapshot = new SnapshotBuilder().Gcd(0.9f).NoCombo().Build();

        var suggestion = session.Resolve(RotationMode.SingleTarget, snapshot, actions);

        Assert.Equal(Mch.Reassemble.Id, suggestion.Action.Id);
    }

    [Fact]
    public void OverheatLocksTheButtonIntoBlasts()
    {
        var session = MachinistSession();
        var snapshot = new SnapshotBuilder().Gcd(0.1f).Buff(Mch.Overheated, 8f).NoCombo().Build();

        var suggestion = session.Resolve(RotationMode.SingleTarget, snapshot, new FakeActionState());

        Assert.Equal(Mch.BlazingShot.Id, suggestion.Action.Id);
    }

    [Fact]
    public void TheQueenIsNotResummonedWhileSheIsAlreadyOut()
    {
        var session = MachinistSession();
        var actions = new FakeActionState()
            .OnCooldown(Mch.Wildfire.Id, 60f)
            .OnCooldown(Mch.BarrelStabilizer.Id, 60f)
            .OnCooldown(Mch.Hypercharge.Id, 60f)
            .OnCooldown(Mch.Reassemble.Id, 60f)
            .OnCooldown(Mch.DoubleCheck.Id, 20f)
            .OnCooldown(Mch.Checkmate.Id, 20f);

        var snapshot = new SnapshotBuilder()
            .Gcd(2.2f)
            .NoCombo()
            .Gauge(s =>
            {
                s.Gauges.Machinist.Battery = 100;
                s.Gauges.Machinist.RobotActive = true;
            })
            .Build();

        var suggestion = session.Resolve(RotationMode.SingleTarget, snapshot, actions);

        Assert.NotEqual(Mch.AutomatonQueen.Id, suggestion.Action.Id);
    }

    // ---- Positionals -----------------------------------------------------

    [Fact]
    public void TrueNorthIsOfferedWhenStandingInTheWrongPlaceForAPositional()
    {
        var session = DragoonSession();

        // Chaotic Spring is next and wants the rear; we are standing in front of the boss.
        var snapshot = new SnapshotBuilder()
            .Gcd(0.9f)
            .Combo(Drg.SpiralBlow)
            .Position(RelativePosition.Front)
            .Build();

        var suggestion = session.Resolve(RotationMode.SingleTarget, snapshot, new FakeActionState());

        Assert.Equal(Drg.TrueNorth.Id, suggestion.Action.Id);
        Assert.Equal(PositionalHint.Rear, suggestion.Positional);
    }

    [Fact]
    public void TrueNorthIsNotOfferedWhenAlreadyStandingCorrectly()
    {
        var session = DragoonSession();
        var snapshot = new SnapshotBuilder()
            .Gcd(0.9f)
            .Combo(Drg.SpiralBlow)
            .Position(RelativePosition.Rear)
            .Build();

        var suggestion = session.Resolve(RotationMode.SingleTarget, snapshot, new FakeActionState());

        Assert.NotEqual(Drg.TrueNorth.Id, suggestion.Action.Id);
    }

    [Fact]
    public void TrueNorthIsNotDoublePressed()
    {
        var session = DragoonSession();
        var snapshot = new SnapshotBuilder()
            .Gcd(0.9f)
            .Combo(Drg.SpiralBlow)
            .Position(RelativePosition.Front)
            .Buff(Drg.TrueNorthBuff, 8f)
            .Build();

        var suggestion = session.Resolve(RotationMode.SingleTarget, snapshot, new FakeActionState());

        Assert.NotEqual(Drg.TrueNorth.Id, suggestion.Action.Id);
    }

    [Fact]
    public void PositionalRescueCanBeTurnedOff()
    {
        var settings = NoOpener();
        settings.SuggestPositionalRescue = false;
        var session = DragoonSession(settings);

        var snapshot = new SnapshotBuilder()
            .Gcd(0.9f)
            .Combo(Drg.SpiralBlow)
            .Position(RelativePosition.Front)
            .Build();

        var suggestion = session.Resolve(RotationMode.SingleTarget, snapshot, new FakeActionState());

        Assert.NotEqual(Drg.TrueNorth.Id, suggestion.Action.Id);
    }

    // ---- Level sync ------------------------------------------------------

    [Fact]
    public void SyncedDownTheButtonUsesTheActionsTheJobActuallyHas()
    {
        var session = DragoonSession();
        var rotation = JobRotationBase.Create<DragoonRotation>();

        var actions = new FakeActionState();
        foreach (var action in rotation.AllActions)
        {
            if (action.Level > 50)
                actions.Locked(action.Id);
        }

        var snapshot = new SnapshotBuilder().Level(50).Gcd(0.1f).Combo(Drg.SpiralBlow).Build();

        var suggestion = session.Resolve(RotationMode.SingleTarget, snapshot, actions);

        Assert.Equal(Drg.ChaosThrust.Id, suggestion.Action.Id);
    }
}
