using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Beastmaster;
using OneTwoPunch.Core.Model;
using Xunit;
using A = OneTwoPunch.Core.Jobs.Beastmaster.BeastmasterActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// Beastmaster is registered deliberately incomplete - it drives the weaponskill combo so
/// that the recorder can run at all, and leaves the instinctual ring alone until the engine
/// has somewhere to put a third clock. These tests pin the part that is claimed, and pin the
/// omissions as omissions so that finishing the job is a visible change rather than a quiet
/// one.
/// </summary>
public sealed class BeastmasterTests
{
    private static RotationSession Session() => new(
        JobRotationBase.Create<BeastmasterRotation>(),
        new RotationSettings { UseOpener = false, SuggestionHoldSeconds = 0f });

    private static SnapshotBuilder Bst(byte level = 50) =>
        new SnapshotBuilder().Job(43).Level(level).Gcd(0.1f).Enemies(1);

    // ---- The combo --------------------------------------------------------

    [Fact]
    public void TheComboOpensOnSmashAxe()
    {
        var suggestion = Session().Resolve(
            RotationMode.SingleTarget, Bst().NoCombo().Build(), new FakeActionState());

        Assert.Equal(A.SmashAxe.Id, suggestion.Action.Id);
    }

    [Fact]
    public void SmashAxeLeadsToAxebladeBite()
    {
        var suggestion = Session().Resolve(
            RotationMode.SingleTarget, Bst().Combo(A.SmashAxe.Id).Build(), new FakeActionState());

        Assert.Equal(A.AxebladeBite.Id, suggestion.Action.Id);
    }

    [Fact]
    public void AxebladeBiteLeadsToShieldsplitter()
    {
        var suggestion = Session().Resolve(
            RotationMode.SingleTarget, Bst().Combo(A.AxebladeBite.Id).Build(), new FakeActionState());

        Assert.Equal(A.Shieldsplitter.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// Shieldsplitter is a level 12 action and Beastmaster starts at 1, so the low end of
    /// this job's range is somewhere people will actually be - unlike every other job here,
    /// which is levelled through content that outgrows it. A finisher that cannot be pressed
    /// must fall back rather than hang the combo.
    /// </summary>
    [Fact]
    public void BelowTwelveTheComboFallsBackInsteadOfHanging()
    {
        var suggestion = Session().Resolve(
            RotationMode.SingleTarget,
            Bst(level: 8).Combo(A.AxebladeBite.Id).Build(),
            new FakeActionState());

        Assert.NotEqual(A.Shieldsplitter.Id, suggestion.Action.Id);
        Assert.Equal(A.SmashAxe.Id, suggestion.Action.Id);
    }

    // ---- The area button --------------------------------------------------

    [Fact]
    public void ShieldChargeComesOutOnAGroup()
    {
        var suggestion = Session().Resolve(
            RotationMode.Aoe, Bst().Gcd(1.6f).Enemies(3).NoCombo().Build(), new FakeActionState());

        Assert.Equal(A.ShieldCharge.Id, suggestion.Action.Id);
    }

    [Fact]
    public void ShieldChargeIsHeldDuringDowntime()
    {
        var suggestion = Session().Resolve(
            RotationMode.Aoe,
            Bst().Gcd(1.6f).Enemies(3).NoCombo().Downtime().Build(),
            new FakeActionState());

        Assert.NotEqual(A.ShieldCharge.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// Beastmaster has no area weaponskill line at all, so the single-target combo is the
    /// correct answer on the area button once Shield Charge is spent. This is the job being
    /// unusual, not the list being unfinished.
    /// </summary>
    [Fact]
    public void WithShieldChargeSpentTheAreaButtonWalksTheOrdinaryCombo()
    {
        var actions = new FakeActionState().OnCooldown(A.ShieldCharge.Id, 60f);

        var suggestion = Session().Resolve(
            RotationMode.Aoe, Bst().Enemies(5).Combo(A.SmashAxe.Id).Build(), actions);

        Assert.Equal(A.AxebladeBite.Id, suggestion.Action.Id);
    }

    [Fact]
    public void TheTwoButtonsAreDifferentActions()
    {
        var job = JobRotationBase.Create<BeastmasterRotation>();

        // The plugin keys its hotbar forms by action id, so a shared host would collapse
        // the two buttons into one.
        Assert.NotEqual(job.SingleTargetButton.Id, job.AoeButton.Id);
    }

    // ---- The gauge line ---------------------------------------------------

    /// <summary>
    /// There is no Beastmaster gauge to read, so the recorder's line is assembled from the
    /// Hearts instead. Getting a readable one into the first log is the entire point of
    /// registering the job this early: Monk and Viper each cost several pulls precisely
    /// because their state never reached the log.
    /// </summary>
    [Fact]
    public void TheGaugeLineNamesThePositionOnTheRing()
    {
        var job = JobRotationBase.Create<BeastmasterRotation>();

        var line = job.DescribeGauge(Bst().Buff(A.RampantHeart.Id, 7f).Build());

        Assert.NotNull(line);
        Assert.Contains("rampant", line);

        // Rampant Heart makes a Durant skill combo, and Mistral Axe is the Durant one.
        Assert.Contains("mistral", line);
    }

    [Fact]
    public void TheGaugeLineWalksTheWholeRing()
    {
        var job = JobRotationBase.Create<BeastmasterRotation>();

        // Volant into Rampant into Durant into Eldritch and back, per the official guide's
        // Inner Compass read clockwise.
        Assert.Contains("avalanche", job.DescribeGauge(Bst().Buff(A.VolantHeart.Id, 7f).Build())!);
        Assert.Contains("mistral", job.DescribeGauge(Bst().Buff(A.RampantHeart.Id, 7f).Build())!);
        Assert.Contains("spinning", job.DescribeGauge(Bst().Buff(A.DurantHeart.Id, 7f).Build())!);
        Assert.Contains("gale", job.DescribeGauge(Bst().Buff(A.EldritchHeart.Id, 7f).Build())!);
    }

    [Fact]
    public void TheGaugeLineReportsAnEmptyRingRatherThanGuessing()
    {
        var job = JobRotationBase.Create<BeastmasterRotation>();

        var line = job.DescribeGauge(Bst().Build())!;

        Assert.Contains("heart none", line);
        Assert.DoesNotContain("->", line);
    }

    [Fact]
    public void TheGaugeLineShowsTheCompassAndTheFamiliarLockout()
    {
        var job = JobRotationBase.Create<BeastmasterRotation>();

        var line = job.DescribeGauge(Bst()
            .Buff(A.Sunstrider.Id, 7f)
            .Buff(A.OneWithNature.Id, 30f)
            .Buff(A.WaveringHeart.Id, 5f)
            .Build())!;

        Assert.Contains("sunstrider", line);
        Assert.Contains("one-with-nature", line);
        Assert.Contains("WAVERING", line);
    }

    // ---- The omissions, pinned -------------------------------------------

    // ---- The instinctual ring ---------------------------------------------

    /// <summary>
    /// Trick into an instinctual is an intentional combo and banks a stack of Mastered
    /// Instinct; the same instinctual pressed first banks nothing. So when both are
    /// available the button asks for Trick.
    /// </summary>
    [Fact]
    public void TrickComesBeforeTheInstinctual()
    {
        var suggestion = Session().Resolve(
            RotationMode.SingleTarget,
            Bst().Gcd(1.6f).NoCombo().Build(),
            new FakeActionState());

        Assert.Equal(A.Trick.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// Each Heart names the instinctual that combos off it, so the buff is the position on
    /// the ring and no state of ours is needed. This is the whole rotation.
    /// </summary>
    [Theory]
    [InlineData(4595u, 44884u)] // Volant   -> Avalanche Axe
    [InlineData(4596u, 44887u)] // Rampant  -> Mistral Axe
    [InlineData(4597u, 44888u)] // Durant   -> Spinning Axe
    [InlineData(4598u, 44889u)] // Eldritch -> Gale Axe
    public void TheHeldHeartNamesTheNextInstinctual(uint heart, uint expected)
    {
        var actions = new FakeActionState().OnCooldown(A.Trick.Id, 20f);

        var suggestion = Session().Resolve(
            RotationMode.SingleTarget,
            Bst().Gcd(1.6f).NoCombo().Buff(heart, 7f).Build(),
            actions);

        Assert.Equal(expected, suggestion.Action.Id);
    }

    /// <summary>
    /// With no Heart in hand any of the four is a legal start. Which is best depends on the
    /// familiar, which the engine cannot see - so the opener is ordered by level and must
    /// never offer one the player has not learned.
    /// </summary>
    [Fact]
    public void WithNoHeartTheRingIsOpenedWithSomethingLearned()
    {
        var actions = new FakeActionState().OnCooldown(A.Trick.Id, 20f);
        var instinctuals = new[] { A.GaleAxe.Id, A.AvalancheAxe.Id, A.MistralAxe.Id, A.SpinningAxe.Id };

        var suggestion = Session().Resolve(
            RotationMode.SingleTarget, Bst().Gcd(1.6f).NoCombo().Build(), actions);

        Assert.Contains(suggestion.Action.Id, instinctuals);
    }

    [Fact]
    public void BelowGaleTheRingOpensOnSomethingTheJobActuallyHas()
    {
        // Level 10: Avalanche (4) and Mistral (8) only.
        var actions = new FakeActionState().OnCooldown(A.Trick.Id, 20f);

        var suggestion = Session().Resolve(
            RotationMode.SingleTarget,
            Bst(level: 10).Gcd(1.6f).NoCombo().Build(),
            actions);

        Assert.NotEqual(A.GaleAxe.Id, suggestion.Action.Id);
        Assert.NotEqual(A.SpinningAxe.Id, suggestion.Action.Id);
        Assert.Contains(suggestion.Action.Id, new[] { A.AvalancheAxe.Id, A.MistralAxe.Id });
    }

    /// <summary>The ring is held while the boss is untargetable, like every other damage rule.</summary>
    [Fact]
    public void TheRingIsHeldDuringDowntime()
    {
        var actions = new FakeActionState().OnCooldown(A.Trick.Id, 20f);
        var instinctuals = new[] { A.GaleAxe.Id, A.AvalancheAxe.Id, A.MistralAxe.Id, A.SpinningAxe.Id };

        var suggestion = Session().Resolve(
            RotationMode.SingleTarget,
            Bst().Gcd(1.6f).NoCombo().Downtime().Build(),
            actions);

        Assert.DoesNotContain(suggestion.Action.Id, instinctuals);
    }

    /// <summary>
    /// The instinctuals are the damage at any number of targets - the job has no area
    /// weaponskill line - so the area button walks the same ring.
    /// </summary>
    [Fact]
    public void TheAreaButtonWalksTheRingToo()
    {
        var actions = new FakeActionState()
            .OnCooldown(A.Trick.Id, 20f)
            .OnCooldown(A.ShieldCharge.Id, 40f);

        var suggestion = Session().Resolve(
            RotationMode.Aoe,
            Bst().Gcd(1.6f).Enemies(4).NoCombo().Buff(A.RampantHeart.Id, 7f).Build(),
            actions);

        Assert.Equal(A.MistralAxe.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// One instinctual per two globals needs a second weave slot, so the job raises the
    /// floor the way Viper and Ninja do.
    /// </summary>
    [Fact]
    public void TheJobAsksForRoomToWeave()
    {
        Assert.Equal(
            WeaveStyle.Double,
            JobRotationBase.Create<BeastmasterRotation>().MinimumWeaveStyle);
    }

    /// <summary>
    /// Beastmaster cannot execute role actions - the official job guide says so outright -
    /// so there is no Second Wind, no Bloodbath and no True North. Every other melee list
    /// here hangs a self-heal rule on Second Wind; this one must not, and must not offer a
    /// positional rescue it does not have.
    /// </summary>
    [Fact]
    public void NoRoleActionsAreClaimed()
    {
        var job = JobRotationBase.Create<BeastmasterRotation>();

        Assert.Null(job.PositionalRescue);
        Assert.Null(job.PositionalRescueStatus);

        foreach (var action in job.AllActions)
        {
            Assert.False(
                action.Name is "Second Wind" or "Bloodbath" or "True North" or "Leg Sweep"
                    or "Feint" or "Arm's Length",
                $"{action.Name} is a role action, and Beastmaster cannot execute role actions.");
        }
    }

    /// <summary>
    /// Rally is declared as the burst marker because it is genuinely the cooldown damage
    /// aligns to - three stacks of Mastered Instinct is 40 + 210 = exactly 250 TP, a full
    /// bar, which is what upgrades an instinctual to its 1,200 potency form. It is a marker
    /// only: filling the bar is pointless while the ring that spends it is not driven, so
    /// nothing suggests it. The engine still needs it to know when a potion is worth
    /// prompting for, which is what AllJobsSmokeTests checks.
    /// </summary>
    [Fact]
    public void RallyIsTheBurstMarkerButIsNotSuggestedYet()
    {
        var job = JobRotationBase.Create<BeastmasterRotation>();

        Assert.Same(A.Rally, job.BurstAction);
        Assert.Null(job.BurstStatus);

        var session = Session();
        foreach (var gcd in new[] { 0.1f, 1.6f })
        {
            foreach (var mode in new[] { RotationMode.SingleTarget, RotationMode.Aoe })
            {
                var suggestion = session.Resolve(
                    mode, Bst().Gcd(gcd).NoCombo().Build(), new FakeActionState());

                Assert.NotEqual(A.Rally.Id, suggestion.Action.Id);
            }
        }
    }

    /// <summary>
    /// Nobody has published a Beastmaster opener. The guide for adding a job is explicit
    /// that a guessed one is worse than none, and a hurt player following a made-up chart is
    /// exactly the harm this plugin exists to avoid.
    /// </summary>
    [Fact]
    public void NoOpenerIsGuessed()
    {
        Assert.Null(JobRotationBase.Create<BeastmasterRotation>().Opener);
    }

    /// <summary>
    /// A self-heal rule cannot be written for this job, so being hurt must not change the
    /// answer. Pinned because the obvious reflex when porting another melee list across is
    /// to bring Second Wind with it.
    /// </summary>
    [Fact]
    public void BeingHurtDoesNotChangeTheAnswer()
    {
        var healthy = Session().Resolve(
            RotationMode.SingleTarget, Bst().NoCombo().Build(), new FakeActionState());

        var hurt = Session().Resolve(
            RotationMode.SingleTarget, Bst().NoCombo().Hp(0.2f).Build(), new FakeActionState());

        Assert.Equal(healthy.Action.Id, hurt.Action.Id);
    }
}
