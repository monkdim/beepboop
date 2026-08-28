using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Model;
using Xunit;
using Drk = OneTwoPunch.Core.Jobs.DarkKnight.DarkKnightActions;
using Gnb = OneTwoPunch.Core.Jobs.Gunbreaker.GunbreakerActions;
using Pld = OneTwoPunch.Core.Jobs.Paladin.PaladinActions;
using War = OneTwoPunch.Core.Jobs.Warrior.WarriorActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// The four tanks. These are the branches that would be silently dead if they were wrong -
/// the chains read off a gauge, the follow-ups named by a status, and the one buff each job
/// cannot afford to drop.
/// </summary>
public sealed class TankRotationTests
{
    private static SnapshotBuilder At(uint jobId, byte level = 100) =>
        new SnapshotBuilder().Job(jobId).Level(level).NoCombo().Enemies(1);

    /// <summary>On the global, so the answer is the next cast.</summary>
    private static SnapshotBuilder Casting(uint jobId, byte level = 100) => At(jobId, level).Gcd(0f);

    /// <summary>Mid-global, so an off-global can be suggested.</summary>
    private static SnapshotBuilder Weaving(uint jobId, byte level = 100) => At(jobId, level).Gcd(2f);

    private static uint Suggest(
        uint jobId,
        SnapshotBuilder builder,
        FakeActionState? actions = null,
        RotationMode mode = RotationMode.SingleTarget)
    {
        var session = new RotationSession(JobRegistry.Create(jobId)!, new RotationSettings
        {
            UseOpener = false,
            SuggestionHoldSeconds = 0f,
        });

        return session.Resolve(mode, builder.Build(), actions ?? new FakeActionState()).Action.Id;
    }

    // ---- Paladin ----------------------------------------------------------

    [Fact]
    public void PaladinWalksItsCombo()
    {
        Assert.Equal(Pld.RiotBlade.Id, Suggest(19, Casting(19).Combo(Pld.FastBlade.Id)));
        Assert.Equal(Pld.RoyalAuthority.Id, Suggest(19, Casting(19).Combo(Pld.RiotBlade.Id)));
    }

    /// <summary>Royal Authority is level 60; below that the finisher is Rage of Halone.</summary>
    [Fact]
    public void PaladinFinishesOnRageOfHaloneBeforeRoyalAuthorityExists()
    {
        var suggestion = Suggest(19, Casting(19, 50).Combo(Pld.RiotBlade.Id));

        Assert.Equal(Pld.RageOfHalone.Id, suggestion);
    }

    /// <summary>Each step of the Atonement chain is named outright by the buff before it.</summary>
    [Theory]
    [InlineData(1902u, 16460u)] // Atonement Ready   -> Atonement
    [InlineData(3827u, 36918u)] // Supplication Ready -> Supplication
    [InlineData(3828u, 36919u)] // Sepulchre Ready    -> Sepulchre
    public void PaladinSpendsTheAtonementChain(uint status, uint expected)
    {
        var suggestion = Suggest(19, Casting(19).Combo(Pld.RoyalAuthority.Id).Buff(status, 25f));

        Assert.Equal(expected, suggestion);
    }

    [Fact]
    public void PaladinOpensTheBladesOnConfiteorReady()
    {
        var suggestion = Suggest(
            19,
            Casting(19).Buff(Pld.ConfiteorReady.Id, 25f).Buff(Pld.RequiescatBuff.Id, 25f, 4));

        Assert.Equal(Pld.Confiteor.Id, suggestion);
    }

    /// <summary>
    /// And walks them by the Requiescat count, which is what stands in for the gauge field
    /// Dalamud does not hand over.
    /// </summary>
    [Theory]
    [InlineData(3, 25748u)] // Blade of Faith
    [InlineData(2, 25749u)] // Blade of Truth
    [InlineData(1, 25750u)] // Blade of Valor
    public void PaladinWalksTheBladesByTheRequiescatCount(byte stacks, uint expected)
    {
        var suggestion = Suggest(19, Casting(19).Buff(Pld.RequiescatBuff.Id, 25f, stacks));

        Assert.Equal(expected, suggestion);
    }

    [Fact]
    public void PaladinSpendsDivineMightOnHolySpirit()
    {
        var suggestion = Suggest(19, Casting(19).Combo(Pld.RoyalAuthority.Id).Buff(Pld.DivineMight.Id, 25f));

        Assert.Equal(Pld.HolySpirit.Id, suggestion);
    }

    // ---- Warrior ----------------------------------------------------------

    /// <summary>
    /// Surging Tempest is a ten percent damage buff on everything, and only Storm's Eye
    /// brings it back. So a bar full enough for Fell Cleave does not get one.
    /// </summary>
    [Fact]
    public void WarriorRefreshesSurgingTempestBeforeSpendingTheGauge()
    {
        var suggestion = Suggest(
            21,
            Casting(21).Combo(War.Maim.Id)
                .Buff(War.SurgingTempest.Id, 5f)
                .Gauge(s => s.Gauges.Warrior.BeastGauge = 100));

        Assert.Equal(War.StormEye.Id, suggestion);
    }

    /// <summary>With the buff healthy, the same bar is a Fell Cleave.</summary>
    [Fact]
    public void WarriorSpendsTheGaugeWhileSurgingTempestIsHealthy()
    {
        var suggestion = Suggest(
            21,
            Casting(21).Combo(War.Maim.Id)
                .Buff(War.SurgingTempest.Id, 50f)
                .Gauge(s => s.Gauges.Warrior.BeastGauge = 100));

        Assert.Equal(War.FellCleave.Id, suggestion);
    }

    /// <summary>Inner Release's Fell Cleaves are free, so an empty gauge is no obstacle.</summary>
    [Fact]
    public void WarriorTakesTheFreeFellCleavesUnderInnerRelease()
    {
        var suggestion = Suggest(
            21,
            Casting(21).Combo(War.HeavySwing.Id)
                .Buff(War.SurgingTempest.Id, 50f)
                .Buff(War.InnerReleaseBuff.Id, 12f, 3));

        Assert.Equal(War.FellCleave.Id, suggestion);
    }

    [Fact]
    public void WarriorSpendsNascentChaosOnInnerChaos()
    {
        var suggestion = Suggest(
            21,
            Casting(21).Buff(War.SurgingTempest.Id, 50f).Buff(War.NascentChaos.Id, 25f));

        Assert.Equal(War.InnerChaos.Id, suggestion);
    }

    [Fact]
    public void WarriorFollowsPrimalRendWithItsRuination()
    {
        var suggestion = Suggest(
            21,
            Casting(21).Buff(War.SurgingTempest.Id, 50f).Buff(War.PrimalRuinationReady.Id, 20f));

        Assert.Equal(War.PrimalRuination.Id, suggestion);
    }

    /// <summary>Inner Release is level 70; below it the same press is Berserk.</summary>
    [Fact]
    public void WarriorBurstsWithBerserkBeforeInnerReleaseExists()
    {
        var suggestion = Suggest(21, Weaving(21, 60).Buff(War.SurgingTempest.Id, 50f));

        Assert.Equal(War.Berserk.Id, suggestion);
    }

    // ---- Dark Knight ------------------------------------------------------

    /// <summary>
    /// Darkside about to drop outranks the whole off-global list. Losing it is a bigger
    /// hole than any cooldown here could fill.
    /// </summary>
    [Fact]
    public void DarkKnightRescuesDarksideBeforeAnythingElse()
    {
        var suggestion = Suggest(
            32,
            Weaving(32).Mp(10000).Gauge(s => s.Gauges.DarkKnight.DarksideTimeRemaining = 4f));

        Assert.Equal(Drk.EdgeOfShadow.Id, suggestion);
    }

    /// <summary>A Darkside that is not going anywhere leaves the cooldowns to go first.</summary>
    [Fact]
    public void DarkKnightWithDarksideHealthyUsesItsCooldowns()
    {
        var suggestion = Suggest(
            32,
            Weaving(32).Mp(10000).Gauge(s => s.Gauges.DarkKnight.DarksideTimeRemaining = 50f));

        Assert.Equal(Drk.LivingShadow.Id, suggestion);
    }

    /// <summary>The Delirium chain comes off the gauge step, not the combo.</summary>
    [Theory]
    [InlineData(0, 36928u)] // Scarlet Delirium
    [InlineData(1, 36929u)] // Comeuppance
    [InlineData(2, 36930u)] // Torcleaver
    public void DarkKnightWalksTheDeliriumChainByTheGauge(byte step, uint expected)
    {
        var suggestion = Suggest(
            32,
            Casting(32).Buff(Drk.EnhancedDelirium.Id, 15f, 3)
                .Gauge(s =>
                {
                    s.Gauges.DarkKnight.DarksideTimeRemaining = 50f;
                    s.Gauges.DarkKnight.DeliriumStep = step;
                }));

        Assert.Equal(expected, suggestion);
    }

    [Fact]
    public void DarkKnightSpendsBloodOnBloodspiller()
    {
        var suggestion = Suggest(
            32,
            Casting(32).Gauge(s =>
            {
                s.Gauges.DarkKnight.DarksideTimeRemaining = 50f;
                s.Gauges.DarkKnight.Blood = 60;
            }));

        Assert.Equal(Drk.Bloodspiller.Id, suggestion);
    }

    [Fact]
    public void DarkKnightWalksItsCombo()
    {
        var standing = Casting(32).Gauge(s => s.Gauges.DarkKnight.DarksideTimeRemaining = 50f);

        Assert.Equal(Drk.SyphonStrike.Id, Suggest(32, standing.Combo(Drk.HardSlash.Id)));
    }

    // ---- Gunbreaker -------------------------------------------------------

    /// <summary>
    /// The Continuation follow-ups. Each is free, each expires in a couple of seconds, and
    /// each is named by a status - which is the only way to reach them, because
    /// Continuation itself is a placeholder the game will not accept.
    /// </summary>
    [Theory]
    [InlineData(1842u, 16156u)] // Ready to Rip   -> Jugular Rip
    [InlineData(1843u, 16157u)] // Ready to Tear  -> Abdomen Tear
    [InlineData(1844u, 16158u)] // Ready to Gouge -> Eye Gouge
    [InlineData(2686u, 25759u)] // Ready to Blast -> Hypervelocity
    public void GunbreakerTakesEveryContinuation(uint status, uint expected)
    {
        var suggestion = Suggest(37, Weaving(37).Buff(status, 8f));

        Assert.Equal(expected, suggestion);
    }

    /// <summary>The Gnashing Fang chain comes off the gauge step, not the combo.</summary>
    [Theory]
    [InlineData(1, 16147u)] // Savage Claw
    [InlineData(2, 16150u)] // Wicked Talon
    public void GunbreakerWalksGnashingFangByTheGauge(byte step, uint expected)
    {
        var suggestion = Suggest(
            37,
            Casting(37).Gauge(s => s.Gauges.Gunbreaker.AmmoComboStep = step));

        Assert.Equal(expected, suggestion);
    }

    [Fact]
    public void GunbreakerOpensGnashingFangWithACartridge()
    {
        var suggestion = Suggest(37, Casting(37).Gauge(s => s.Gauges.Gunbreaker.Ammo = 2));

        Assert.Equal(Gnb.GnashingFang.Id, suggestion);
    }

    /// <summary>Bloodfest is a full magazine, so it is worth nothing thrown at a full one.</summary>
    [Fact]
    public void GunbreakerHoldsBloodfestUntilTheMagazineIsEmpty()
    {
        var full = Suggest(37, Weaving(37).Gauge(s => s.Gauges.Gunbreaker.Ammo = 3));

        Assert.NotEqual(Gnb.Bloodfest.Id, full);
    }

    [Fact]
    public void GunbreakerWalksItsCombo()
    {
        Assert.Equal(Gnb.BrutalShell.Id, Suggest(37, Casting(37).Combo(Gnb.KeenEdge.Id)));
        Assert.Equal(Gnb.SolidBarrel.Id, Suggest(37, Casting(37).Combo(Gnb.BrutalShell.Id)));
    }

    /// <summary>
    /// A cartridge that would fall on the floor is spent. The magazine holds two below
    /// level 88 and three from it, so "full" is a different number at each.
    /// <para>
    /// Gnashing Fang is put on cooldown, because it spends a cartridge too and would
    /// otherwise be the better answer to the same problem - which is the state this rule
    /// exists for: a full magazine, a combo about to hand over another, and nothing else
    /// available to spend one.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData((byte)80, (byte)2)]
    [InlineData((byte)100, (byte)3)]
    public void GunbreakerSpendsACartridgeTheComboWouldWaste(byte level, byte ammo)
    {
        var actions = new FakeActionState().OnCooldown(Gnb.GnashingFang.Id, 20f);

        var suggestion = Suggest(
            37,
            Casting(37, level).Combo(Gnb.BrutalShell.Id)
                .Gauge(s => s.Gauges.Gunbreaker.Ammo = ammo),
            actions);

        Assert.Equal(Gnb.BurstStrike.Id, suggestion);
    }
}
