using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Pictomancer;
using OneTwoPunch.Core.Model;
using Xunit;
using A = OneTwoPunch.Core.Jobs.Pictomancer.PictomancerActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// The motifs are two thirds of Pictomancer: nothing else produces a muse, and without a
/// muse there is no hammer, no portrait, no Mog and no Star Prism. A recorded pull ran
/// sixty-two casts and painted nothing after the opener, so the paint cycle was the entire
/// job for two and a half minutes.
/// <para>
/// The cause was in the engine rather than here - see the note in ActionStateAdapter about
/// globals longer than the global - but nothing in this suite would have caught it either,
/// because nothing in this suite mentioned a motif. These tests say what the button owes.
/// </para>
/// </summary>
public sealed class PictomancerMotifTests
{
    private static RotationSession Session() => new(
        JobRotationBase.Create<PictomancerRotation>(),
        new RotationSettings { UseOpener = false, SuggestionHoldSeconds = 0f });

    private static SnapshotBuilder Pct() =>
        new SnapshotBuilder().Job(42).Level(100).Gcd(0.1f).Enemies(1);

    /// <summary>
    /// The canvases start empty in a fresh snapshot, which is the state right after the
    /// muses in the opener have spent them. Painting is the only way back.
    /// </summary>
    private static SnapshotBuilder Canvases(
        SnapshotBuilder b, bool creature = false, bool weapon = false, bool landscape = false) =>
        b.Gauge(s =>
        {
            s.Gauges.Pictomancer.CreatureMotifDrawn = creature;
            s.Gauges.Pictomancer.WeaponMotifDrawn = weapon;
            s.Gauges.Pictomancer.LandscapeMotifDrawn = landscape;
        });

    /// <summary>Scenic Muse parked, so a test about the other two is not answered by it.</summary>
    private static FakeActionState NoScenic() =>
        new FakeActionState().OnCooldown(A.ScenicMuse.Id, 90f);

    // ---- Painting ---------------------------------------------------------

    [Fact]
    public void AnEmptyWeaponCanvasIsPaintedRatherThanAttacked()
    {
        var suggestion = Session().Resolve(
            RotationMode.SingleTarget, Canvases(Pct()).Build(), NoScenic());

        Assert.Equal(A.WeaponMotif.Id, suggestion.Action.Id);
    }

    [Fact]
    public void TheCreatureCanvasIsPaintedOnceTheWeaponOneIsFull()
    {
        var suggestion = Session().Resolve(
            RotationMode.SingleTarget, Canvases(Pct(), weapon: true).Build(), NoScenic());

        Assert.Equal(A.CreatureMotif.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// A full canvas cannot be painted over, so with both drawn the button is the paint
    /// cycle again - which is what the whole of the recorded pull looked like.
    /// </summary>
    [Fact]
    public void FullCanvasesFallThroughToThePaintCycle()
    {
        var suggestion = Session().Resolve(
            RotationMode.SingleTarget,
            Canvases(Pct(), creature: true, weapon: true).Build(),
            NoScenic());

        Assert.Equal(A.FireInRed.Id, suggestion.Action.Id);
    }

    [Fact]
    public void TheAoeButtonPaintsTheSameCanvases()
    {
        var suggestion = Session().Resolve(
            RotationMode.Aoe, Canvases(Pct()).Enemies(3).Build(), NoScenic());

        Assert.Equal(A.WeaponMotif.Id, suggestion.Action.Id);
    }

    // ---- When not to paint ------------------------------------------------

    /// <summary>
    /// A drawn canvas does nothing on its own - it is a muse waiting to happen - so painting
    /// one with no charge to spend it is a rooted three second global that deals no damage and
    /// buys nothing until the charge is back.
    /// <para>
    /// A recorded pull painted six of its eleven motifs that way, each between seventy-seven
    /// and a hundred and eighteen seconds from the next charge, and one stretch ran fifteen
    /// seconds in which every global was a motif. Scored against the game's own potencies that
    /// pull ran fourteen percent behind the one before it, which did none of this because it
    /// could not paint at all.
    /// </para>
    /// </summary>
    [Fact]
    public void ACanvasIsNotPaintedWithNoMuseToSpendIt()
    {
        var suggestion = Session().Resolve(
            RotationMode.SingleTarget,
            Canvases(Pct()).Build(),
            NoScenic().WithCharges(A.SteelMuse.Id, 0, 2).WithCharges(A.LivingMuse.Id, 0, 3));

        Assert.Equal(A.FireInRed.Id, suggestion.Action.Id);
    }

    /// <summary>One charge in hand is reason enough - that is what the canvas is for.</summary>
    [Fact]
    public void OneChargeIsEnoughToPaintFor()
    {
        var suggestion = Session().Resolve(
            RotationMode.SingleTarget,
            Canvases(Pct()).Build(),
            NoScenic().WithCharges(A.SteelMuse.Id, 1, 2).WithCharges(A.LivingMuse.Id, 0, 3));

        Assert.Equal(A.WeaponMotif.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// And the canvas is still banked ahead of the muse rather than only once it is up, or
    /// every muse would wait three seconds for its motif.
    /// </summary>
    [Fact]
    public void ACanvasIsPaintedAheadOfTheChargeComingBack()
    {
        var suggestion = Session().Resolve(
            RotationMode.SingleTarget,
            Canvases(Pct()).Build(),
            NoScenic()
                .WithCharges(A.LivingMuse.Id, 0, 3)
                .OnCooldown(A.SteelMuse.Id, 6f));

        Assert.Equal(A.WeaponMotif.Id, suggestion.Action.Id);
    }

    /// <summary>A motif roots you for three seconds, so it is never the answer while moving.</summary>
    [Fact]
    public void MovingPaintsNothing()
    {
        var suggestion = Session().Resolve(
            RotationMode.SingleTarget, Canvases(Pct()).Moving().Build(), NoScenic());

        Assert.NotEqual(A.WeaponMotif.Id, suggestion.Action.Id);
        Assert.NotEqual(A.CreatureMotif.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// Starry Muse is twenty seconds of increased damage and a motif deals none, so a motif
    /// painted inside it is a global of the raid buff spent on nothing.
    /// </summary>
    [Fact]
    public void TheBurstWindowIsNotPaintedThrough()
    {
        var suggestion = Session().Resolve(
            RotationMode.SingleTarget,
            Canvases(Pct()).Buff(A.StarryMuseBuff.Id).Build(),
            NoScenic());

        Assert.NotEqual(A.WeaponMotif.Id, suggestion.Action.Id);
        Assert.NotEqual(A.CreatureMotif.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// Hyperphantasia is five paint spells that each hit harder. A motif does not spend a
    /// stack, so painting one there throws the stack's worth of damage away.
    /// </summary>
    [Fact]
    public void HyperphantasiaIsSpentOnPaintNotMotifs()
    {
        var suggestion = Session().Resolve(
            RotationMode.SingleTarget,
            Canvases(Pct()).Buff(A.Hyperphantasia.Id).Build(),
            NoScenic());

        Assert.Equal(A.FireInRed.Id, suggestion.Action.Id);
    }

    // ---- Spending ---------------------------------------------------------

    /// <summary>
    /// A drawn canvas is a muse waiting to happen, and the muse is an off-global - so in a
    /// weave window it outranks everything the button could paint or cast.
    /// </summary>
    [Fact]
    public void ADrawnWeaponCanvasIsSpentOnStrikingMuse()
    {
        // Named through Steel Muse, which is the icon; in the fight that is Striking Muse.
        var suggestion = Session().Resolve(
            RotationMode.SingleTarget,
            Canvases(Pct(), weapon: true).Gcd(1.6f).Build(),
            NoScenic().Resolving(A.SteelMuse.Id, A.StrikingMuse.Id));

        Assert.Equal(A.StrikingMuse.Id, suggestion.Action.Id);
    }

    [Fact]
    public void ADrawnCreatureCanvasIsSpentOnLivingMuse()
    {
        var suggestion = Session().Resolve(
            RotationMode.SingleTarget,
            Canvases(Pct(), creature: true).Gcd(1.6f).Build(),
            NoScenic());

        Assert.Equal(A.LivingMuse.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// The landscape canvas is the only one painted against a clock: Scenic Muse is the raid
    /// buff, so the canvas has to be up before the cooldown is, and painting it any earlier
    /// is a global spent for nothing.
    /// </summary>
    [Fact]
    public void TheLandscapeCanvasIsPaintedOnlyAheadOfTheBuff()
    {
        var early = Session().Resolve(
            RotationMode.SingleTarget,
            Canvases(Pct(), creature: true, weapon: true).Build(),
            new FakeActionState().OnCooldown(A.ScenicMuse.Id, 90f));

        Assert.NotEqual(A.LandscapeMotif.Id, early.Action.Id);

        var soon = Session().Resolve(
            RotationMode.SingleTarget,
            Canvases(Pct(), creature: true, weapon: true).Build(),
            new FakeActionState().OnCooldown(A.ScenicMuse.Id, 10f));

        Assert.Equal(A.LandscapeMotif.Id, soon.Action.Id);
    }

    // ---- The free subtractive --------------------------------------------

    /// <summary>
    /// Fifty gauge is one way into the subtractive palette and Starry Muse's own Subtractive
    /// Spectrum is the other. The gauge condition alone let the free one expire unused inside
    /// the burst, which is where it is worth the most.
    /// </summary>
    [Fact]
    public void SubtractiveSpectrumIsSpentWithoutTheGauge()
    {
        var suggestion = Session().Resolve(
            RotationMode.SingleTarget,
            Canvases(Pct()).Buff(A.SubtractiveSpectrum.Id).Gcd(1.6f).Build(),
            NoScenic());

        Assert.Equal(A.SubtractivePalette.Id, suggestion.Action.Id);
    }

    // ---- The icon is not the action --------------------------------------

    /// <summary>
    /// Creature Motif is the icon on the bar and the game always replaces it. Asking the game
    /// about the icon's own id gets "cannot use yet" whatever the canvas holds, so the button
    /// has to name what the icon currently is.
    /// <para>
    /// A recorded pull shows the pair: Living Muse refused at three charges with the creature
    /// canvas drawn, while Striking Muse - named by its replaced id - went off in the same
    /// fight. Every action in that log reading a flat refusal has an ActionIndirection row.
    /// </para>
    /// </summary>
    [Fact]
    public void TheMotifSuggestedIsTheFormTheGameWouldCast()
    {
        var suggestion = Session().Resolve(
            RotationMode.SingleTarget,
            Canvases(Pct()).Build(),
            NoScenic().Resolving(A.WeaponMotif.Id, A.HammerMotif.Id));

        Assert.Equal(A.HammerMotif.Id, suggestion.Action.Id);
    }

    [Fact]
    public void TheMuseSpentIsTheFormTheGameWouldCast()
    {
        var suggestion = Session().Resolve(
            RotationMode.SingleTarget,
            Canvases(Pct(), creature: true).Gcd(1.6f).Build(),
            NoScenic().Resolving(A.LivingMuse.Id, A.WingedMuse.Id));

        Assert.Equal(A.WingedMuse.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// Both portraits are one button, so one rule covers them - the id it resolves to says
    /// which half is standing.
    /// </summary>
    [Fact]
    public void TheMadeenPortraitIsSpentThroughTheSameButton()
    {
        var suggestion = Session().Resolve(
            RotationMode.SingleTarget,
            Canvases(Pct()).Gcd(1.6f).Gauge(s => s.Gauges.Pictomancer.MadeenPortraitReady = true).Build(),
            NoScenic().Resolving(A.MogOfTheAges.Id, A.RetributionOfTheMadeen.Id));

        Assert.Equal(A.RetributionOfTheMadeen.Id, suggestion.Action.Id);
    }

    /// <summary>An id the job does not declare is left alone rather than suggested unchecked.</summary>
    [Fact]
    public void AnUndeclaredFormIsNotSuggested()
    {
        var suggestion = Session().Resolve(
            RotationMode.SingleTarget,
            Canvases(Pct()).Build(),
            NoScenic().Resolving(A.WeaponMotif.Id, 999999u));

        Assert.Equal(A.WeaponMotif.Id, suggestion.Action.Id);
    }

    // ---- The opener ------------------------------------------------------

    /// <summary>
    /// A step the game refuses for a missing prerequisite is stepped over, not thrown away.
    /// <para>
    /// A recorded pull lost its whole opener on "step 2 (Pom Muse) was not usable (#572)"
    /// because the creature canvas had not been painted before the pull. Every step behind it
    /// was fine, and the priority list had to improvise a burst the chart already had written
    /// down. 572 is "cannot use yet" - setup that was never done - which is not the player
    /// going off script.
    /// </para>
    /// </summary>
    [Fact]
    public void TheOpenerStepsOverAMuseWhoseCanvasWasNeverPainted()
    {
        var session = new RotationSession(
            JobRotationBase.Create<PictomancerRotation>(),
            new RotationSettings { SuggestionHoldSeconds = 0f });

        var actions = new FakeActionState().Refused(A.PomMuse.Id, 572);

        // A fresh pull - the opener will not start on a fight already underway.
        static SnapshotBuilder Fresh(SnapshotBuilder b) => b.Gauge(s => s.CombatDuration = 0.5f);

        // Step one, pressed, which starts the fight.
        session.Resolve(RotationMode.SingleTarget, Fresh(Pct()).Build(), actions);
        session.NotifyActionUsed(A.RainbowDrip.Id);

        var next = session.Resolve(
            RotationMode.SingleTarget, Fresh(Pct()).Gcd(1.6f).Build(), actions);

        Assert.Null(session.OpenerOutcome);
        Assert.NotEqual(A.PomMuse.Id, next.Action.Id);
    }

    // ---- Diagnostics ------------------------------------------------------

    /// <summary>
    /// Both lines exist because their absence cost a round: the log for the pull that
    /// started this could not say whether the canvases were full or the rule unreachable.
    /// </summary>
    [Fact]
    public void TheLogSaysWhatTheCanvasesHoldAndWhatMayBeOffered()
    {
        var job = JobRotationBase.Create<PictomancerRotation>();
        var snapshot = Canvases(Pct(), weapon: true).Build();

        var gauge = job.DescribeGauge(snapshot)!;
        Assert.Contains("canvas -/weapon/-", gauge);
        Assert.Contains("paint", gauge);
        Assert.Contains("palette", gauge);

        var readiness = job.DescribeReadiness(snapshot, new FakeActionState())!;
        foreach (var name in new[] { "Creature Motif", "Weapon Motif", "Landscape Motif", "Living Muse" })
            Assert.Contains(name, readiness);

        // And when the game has replaced one, the line names both - the icon that was asked
        // about and the action the answer is actually about.
        var replaced = job.DescribeReadiness(
            snapshot, new FakeActionState().Resolving(A.CreatureMotif.Id, A.WingMotif.Id))!;

        Assert.Contains("Creature Motif->Wing Motif=", replaced);
    }
}
